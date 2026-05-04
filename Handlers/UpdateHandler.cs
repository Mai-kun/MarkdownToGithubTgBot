using System.Globalization;
using MdNoteToGithub.DataBase;
using MdNoteToGithub.Models;
using MdNoteToGithub.Resources;
using MdNoteToGithub.Services;
using Octokit;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MdNoteToGithub.Handlers;

public static class UpdateHandler
{
    public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        var updateFrom = update.CallbackQuery?.From ?? update.Message?.From;
        if (updateFrom is null)
        {
            return;
        }

        var languageCode = updateFrom.LanguageCode;
        var userId = updateFrom.Id;

        await using var dbContext = new BotDbContext();
        var user = await UserRegistrator.GetOrCreateUserAsync(userId, dbContext, languageCode,
            cancellationToken);

        if (update.CallbackQuery is not null)
        {
            await CallbackHandler.HandleCallbackQueryAsync(botClient, update.CallbackQuery, user, cancellationToken);
            if (dbContext.ChangeTracker.HasChanges())
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var message = update.Message;
        if (message is null)
        {
            return;
        }

        var text = message.Text?.Trim() ?? message.Caption?.Trim() ?? "";
        var chatId = message.Chat.Id;

        var culture = new CultureInfo(user.LanguageCode);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

        try
        {
            if (string.IsNullOrEmpty(text) && message.ForwardOrigin is null)
            {
                return;
            }

            if (text.StartsWith("/start"))
            {
                await botClient.SendMessage(chatId, Strings.Welcome, ParseMode.Markdown,
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: cancellationToken);
            }
            else if (text.StartsWith("/register"))
            {
                await UserRegistrator.SaveTokenAsync(botClient, message, text, user, cancellationToken);
            }
            else if (text == "/settings")
            {
                await CallbackHandler.ShowSettingsMenuAsync(botClient, message.Chat.Id, user, cancellationToken);
            }
            else
            {
                await SaveNoteAsync(botClient, message, text, user, cancellationToken);
            }

            if (dbContext.ChangeTracker.HasChanges())
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"Error with creating note: {ex.Message}");
            await botClient.SendMessage(chatId, Strings.ErrorSendMessage,
                cancellationToken: cancellationToken);
        }
    }

    private static async Task SaveNoteAsync(ITelegramBotClient botClient, Message message, string text,
        UserSettings user,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(user.GithubToken))
        {
            await botClient.SendMessage(message.Chat.Id, Strings.InfoRegistration,
                cancellationToken: cancellationToken);
            return;
        }

        var ghClient = new GitHubClient(new ProductHeaderValue("MdNoteToGithub"))
        {
            Credentials = new Credentials(user.GithubToken),
        };

        var sourceMetadata = "";

        if (message.ForwardOrigin != null)
        {
            sourceMetadata = message.ForwardOrigin switch
            {
                MessageOriginChannel channel =>
                    $"\n\n**{Strings.ForwardedFrom}:** [{channel.Chat.Title}](https://t.me/{channel.Chat.Username}/{channel.MessageId})",

                MessageOriginUser userOrigin =>
                    $"\n\n**{Strings.ForwardedFrom}:** {userOrigin.SenderUser.FirstName} {userOrigin.SenderUser.LastName}",

                MessageOriginChat chatOrigin =>
                    $"\n\n**{Strings.ForwardedFrom}:** {chatOrigin.SenderChat.Title}",

                _ => "",
            };
        }

        var markdownContent = $"""
                               {text}
                               {sourceMetadata}
                               """;

        var fileName = $"Inbox/{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.md";

        var createChangeSet = new CreateFileRequest(
            "Quick note added via TG bot",
            markdownContent,
            "main"
        );

        try
        {
            await ghClient.Repository.Content.CreateFile(user.RepoOwner, user.RepoName, fileName, createChangeSet);
            await botClient.SendMessage(message.Chat.Id, Strings.InfoNoteCreated,
                cancellationToken: cancellationToken);
        }
        catch (NotFoundException)
        {
            await botClient.SendMessage(message.Chat.Id, Strings.ErrorGithubNotFound,
                cancellationToken: cancellationToken);
        }
        catch (AuthorizationException)
        {
            await botClient.SendMessage(message.Chat.Id, Strings.ErrorInvalidToken,
                cancellationToken: cancellationToken);
        }
    }
}