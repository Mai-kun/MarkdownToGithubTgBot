using MdNoteToGithub.DataBase;
using MdNoteToGithub.Models;
using MdNoteToGithub.Resources;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MdNoteToGithub.Services;

public static class UserRegistrator
{
    public static async Task SaveTokenAsync(
        ITelegramBotClient botClient,
        Message message,
        string text,
        UserSettings user,
        CancellationToken cancellationToken
    )
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 3 || !parts[2].Contains('/'))
        {
            await botClient.SendMessage(
                message.Chat.Id,
                Strings.ErrorInvalidRegistrationFormat,
                ParseMode.Markdown,
                cancellationToken: cancellationToken
            );
            return;
        }

        var repoParts = parts[2].Split('/');

        user.GithubToken = parts[1];
        user.RepoOwner = repoParts[0];
        user.RepoName = repoParts[1];

        try
        {
            await botClient.DeleteMessage(message.Chat.Id, message.MessageId, cancellationToken);
        }
        catch
        {
            // Ignore if the bot was unable to delete it (e.g. lacks permissions)
        }

        await botClient.SendMessage(
            message.Chat.Id,
            Strings.InfoRegistration,
            cancellationToken: cancellationToken
        );
    }

    public static async Task<UserSettings> GetOrCreateUserAsync(
        long userId,
        BotDbContext dbContext,
        string? languageCode,
        CancellationToken cancellationToken
    )
    {
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user is null)
        {
            user = new UserSettings
            {
                TelegramId = userId,
                LanguageCode = languageCode ?? "ru",
            };
            await dbContext.Users.AddAsync(user, cancellationToken);
        }

        return user;
    }
}