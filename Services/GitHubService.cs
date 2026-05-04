using MdNoteToGithub.Models;
using MdNoteToGithub.Resources;
using Octokit;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace MdNoteToGithub.Services;

public static class GitHubService
{
    public static async Task SaveNoteAsync(ITelegramBotClient botClient, List<Message> messages, UserSettings user,
        CancellationToken ct)
    {

        var ghClient = user.GetGitHubClient();
        if (ghClient is null)
        {
            await botClient.SendMessage(messages.First().Chat.Id, Strings.ErrorInvalidRegistrationFormat,
                cancellationToken: ct);
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

        var mainText = messages.FirstOrDefault(m => !string.IsNullOrEmpty(m.Caption))?.Caption
                       ?? messages.FirstOrDefault(m => !string.IsNullOrEmpty(m.Text))?.Text
                       ?? "";

        var imageContentMarkdown = "";
        var newTreeItems = new List<NewTreeItem>();

        var imgIndex = 0;
        foreach (var msg in messages.Where(m => m.Photo is { Length: > 0 }))
        {
            if (!user.NeedDownloadImages)
            {
                break;
            }

            try
            {
                var photo = msg.Photo!.Last();
                var file = await botClient.GetFile(photo.FileId, ct);

                using var ms = new MemoryStream();
                await botClient.DownloadFile(file.FilePath!, ms, ct);

                var imgName = $"img_{timestamp}_{imgIndex++}.jpg";
                var imagePath = $"Archive/Images/{imgName}";

                var blobReference = await ghClient.Git.Blob.Create(user.RepoOwner, user.RepoName, new NewBlob
                {
                    Encoding = EncodingType.Base64,
                    Content = Convert.ToBase64String(ms.ToArray()),
                });

                newTreeItems.Add(new NewTreeItem
                {
                    Path = imagePath,
                    Mode = "100644",
                    Type = TreeType.Blob,
                    Sha = blobReference.Sha,
                });

                imageContentMarkdown += $"\n![[{imgName}||600x600]]";
            }
            catch (Exception ex)
            {
                imageContentMarkdown += $"\n> [!ERROR] Ошибка фото: {ex.Message}";
            }
        }

        var sourceLink = "";
        var firstMsg = messages.First();
        if (firstMsg.ForwardOrigin is MessageOriginChannel channel && !string.IsNullOrEmpty(channel.Chat.Username))
        {
            sourceLink =
                $"\n\n**{Strings.ForwardedFrom}:** [Перейти к посту](https://t.me/{channel.Chat.Username}/{channel.MessageId})";
        }

        var markdown = $"""
                        {imageContentMarkdown}
                        {mainText}
                        {sourceLink}
                        """;

        var fileName = $"Temporary/Note_{timestamp}.md";

        try
        {
            await CommitNoteToGithub(user, ghClient, markdown, newTreeItems, fileName, imgIndex);

            await botClient.SendMessage(firstMsg.Chat.Id, Strings.InfoNoteCreated, cancellationToken: ct);
        }
        catch (NotFoundException)
        {
            await botClient.SendMessage(firstMsg.Chat.Id, Strings.ErrorGithubNotFound, cancellationToken: ct);
        }
        catch (AuthorizationException)
        {
            await botClient.SendMessage(firstMsg.Chat.Id, Strings.ErrorInvalidToken, cancellationToken: ct);
        }
    }

    private static async Task CommitNoteToGithub(UserSettings user, GitHubClient ghClient, string markdown,
        List<NewTreeItem> newTreeItems, string fileName, int imgIndex)
    {

        var branchRef = await ghClient.Git.Reference.Get(user.RepoOwner, user.RepoName, "heads/main");
        var latestCommit = await ghClient.Git.Commit.Get(user.RepoOwner, user.RepoName, branchRef.Object.Sha);

        var mdBlob = await ghClient.Git.Blob.Create(user.RepoOwner, user.RepoName, new NewBlob
        {
            Encoding = EncodingType.Utf8,
            Content = markdown,
        });

        newTreeItems.Add(new NewTreeItem
        {
            Path = fileName,
            Mode = "100644",
            Type = TreeType.Blob,
            Sha = mdBlob.Sha,
        });

        var newTree = new NewTree { BaseTree = latestCommit.Tree.Sha };
        foreach (var item in newTreeItems)
        {
            newTree.Tree.Add(item);
        }

        var createdTree = await ghClient.Git.Tree.Create(user.RepoOwner, user.RepoName, newTree);

        var commitMessage = $"Added Note {(imgIndex > 0 ? $"with {imgIndex} images " : "")}via TG Bot";
        var newCommit = new NewCommit(commitMessage, createdTree.Sha, latestCommit.Sha);
        var createdCommit = await ghClient.Git.Commit.Create(user.RepoOwner, user.RepoName, newCommit);

        await ghClient.Git.Reference.Update(user.RepoOwner, user.RepoName, "heads/main",
            new ReferenceUpdate(createdCommit.Sha));
    }
}