using ObsidianTgBot.DataBase;
using ObsidianTgBot.Models;
using ObsidianTgBot.Resources;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ObsidianTgBot.Services;

public static class UserRegistrator
{
    public static async Task RegisterAsync(ITelegramBotClient botClient, Message message, string text, long userId,
        CancellationToken cancellationToken)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 3 || !parts[2].Contains('/'))
        {
            await botClient.SendMessage(message.Chat.Id, Strings.ErrorInvalidRegistrationFormat, ParseMode.Markdown,
                cancellationToken: cancellationToken);
            return;
        }

        var token = parts[1];
        var repoParts = parts[2].Split('/');
        var owner = repoParts[0];
        var repo = repoParts[1];

        await using var db = new BotDbContext();
        var user = await db.Users.FindAsync([userId], cancellationToken);

        if (user == null)
        {
            user = new UserSettings { TelegramId = userId };
            db.Users.Add(user);
        }

        user.GithubToken = token;
        user.RepoOwner = owner;
        user.RepoName = repo;

        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await botClient.DeleteMessage(message.Chat.Id, message.MessageId, cancellationToken);
        }
        catch
        {
            // Ignore if the bot was unable to delete it (e.g. lacks permissions)
        }

        await botClient.SendMessage(message.Chat.Id,
            "✅ Регистрация успешна! Твой токен в безопасности, а сообщение удалено.\nТеперь просто отправляй мне текст, и я буду создавать заметки.",
            cancellationToken: cancellationToken);
    }
}