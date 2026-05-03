using System.Globalization;
using ObsidianTgBot.DataBase;
using ObsidianTgBot.Models;
using ObsidianTgBot.Resources;
using ObsidianTgBot.Services;
using Octokit;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ObsidianTgBot.Handlers;

public static class UpdateHandler
{
    public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        if (update.Message == null)
        {
            return;
        }

        var message = update.Message;
        var text = message.Text?.Trim() ?? message.Caption?.Trim() ?? "";
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;

        await using var db = new BotDbContext();
        var user = await db.Users.FindAsync([userId], cancellationToken);
        var lang = user?.LanguageCode ?? message.From.LanguageCode ?? "ru";

        var culture = new CultureInfo(lang);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

        try
        {
            if (string.IsNullOrEmpty(text) && message.ForwardOrigin == null)
            {
                return;
            }

            if (text.StartsWith("/start"))
            {
                await botClient.SendMessage(chatId, Strings.Welcome, ParseMode.Markdown,
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: cancellationToken);
                return;
            }

            if (text.StartsWith("/register"))
            {
                await UserRegistrator.RegisterAsync(botClient, message, text, userId, cancellationToken);
                return;
            }

            if (text.StartsWith("/lang"))
            {
                await ChangeLanguageAsync(botClient, user, message, db, cancellationToken);
                return;
            }

            await SaveNoteAsync(botClient, message, text, userId, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"Ошибка при обработке сообщения: {ex.Message}");
            await botClient.SendMessage(chatId, Strings.ErrorSendMessage,
                cancellationToken: cancellationToken);
        }
    }

    private static async Task SaveNoteAsync(ITelegramBotClient botClient, Message message, string text, long userId,
        CancellationToken cancellationToken)
    {
        await using var db = new BotDbContext();
        var user = await db.Users.FindAsync([userId], cancellationToken);

        if (user == null || string.IsNullOrEmpty(user.GithubToken))
        {
            await botClient.SendMessage(message.Chat.Id, Strings.InfoRegistration,
                cancellationToken: cancellationToken);
            return;
        }

        var ghClient = new GitHubClient(new ProductHeaderValue("ObsidianTgBot"))
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

    private static async Task ChangeLanguageAsync(ITelegramBotClient botClient, UserSettings? user, Message message,
        BotDbContext db, CancellationToken ct)
    {
        if (user is null)
        {
            return;
        }

        user.LanguageCode = user.LanguageCode == "ru" ? "en" : "ru";
        await db.SaveChangesAsync(ct);

        CultureInfo.CurrentUICulture = new CultureInfo(user.LanguageCode);

        await botClient.SendMessage(message.Chat.Id, Strings.InfoLanguageChanged, cancellationToken: ct);
    }
}