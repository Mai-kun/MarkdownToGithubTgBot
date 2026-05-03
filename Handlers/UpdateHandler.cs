using ObsidianTgBot.DataBase;
using ObsidianTgBot.Services;
using Octokit;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ObsidianTgBot.Handlers;

public class UpdateHandler
{
    public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message || update.Message!.Type != MessageType.Text)
        {
            return;
        }

        var message = update.Message;
        var text = message.Text!.Trim();
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;

        try
        {
            if (text.StartsWith("/start"))
            {
                const string welcome = "Привет! Я бот для сохранения заметок в GitHub.\n\n" +
                                       "Чтобы начать, зарегистрируй свой репозиторий командой:\n" +
                                       "`/register ТВОЙ_GITHUB_TOKEN Владелец/Репозиторий`\n\n" +
                                       "Пример:\n" +
                                       "`/register github_pat_12345 MyUsername/MyObsidianVault`";

                await botClient.SendMessage(chatId, welcome, ParseMode.Markdown,
                    cancellationToken: cancellationToken);
                return;
            }

            if (text.StartsWith("/register"))
            {
                await UserRegistrator.RegisterAsync(botClient, message, text, userId, cancellationToken);
                return;
            }

            await SaveNoteAsync(botClient, message, text, userId, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке сообщения: {ex.Message}");
            await botClient.SendMessage(chatId, "❌ Произошла ошибка. Попробуй позже.",
                cancellationToken: cancellationToken);
        }
    }

    private static async Task SaveNoteAsync(ITelegramBotClient botClient, Message message, string text, long userId,
        CancellationToken ct)
    {
        await using var db = new BotDbContext();
        var user = await db.Users.FindAsync(new object[] { userId }, ct);

        if (user == null || string.IsNullOrEmpty(user.GithubToken))
        {
            await botClient.SendMessage(message.Chat.Id,
                "⚠️ Сначала зарегистрируйся с помощью команды `/register`.", cancellationToken: ct);
            return;
        }

        var ghClient = new GitHubClient(new ProductHeaderValue("ObsidianTgBot"))
        {
            Credentials = new Credentials(user.GithubToken),
        };

        var fileName = $"Inbox/{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.md";
        var markdownContent = $"---\ndate: {DateTime.Now:yyyy-MM-dd HH:mm}\ntags: [inbox, tg_bot]\n---\n\n{text}";

        var createChangeSet = new CreateFileRequest(
            "Quick note added via TG bot",
            markdownContent,
            "main" // Замени на master, если у пользователя ветка master
        );

        try
        {
            await ghClient.Repository.Content.CreateFile(user.RepoOwner, user.RepoName, fileName, createChangeSet);
            await botClient.SendMessage(message.Chat.Id, "✅ Заметка сохранена в папку Inbox!",
                cancellationToken: ct);
        }
        catch (NotFoundException)
        {
            await botClient.SendMessage(message.Chat.Id,
                "❌ Ошибка: Репозиторий не найден. Проверь имя владельца, название репозитория и права токена.",
                cancellationToken: ct);
        }
        catch (AuthorizationException)
        {
            await botClient.SendMessage(message.Chat.Id, "❌ Ошибка: Неверный или просроченный GitHub токен.",
                cancellationToken: ct);
        }
    }
}