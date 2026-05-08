using System.Collections.Concurrent;
using System.Globalization;
using MdNoteToGithub.DataBase;
using MdNoteToGithub.Resources;
using MdNoteToGithub.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MdNoteToGithub.Handlers;

public static class UpdateHandler
{
    private static readonly ConcurrentDictionary<string, List<Message>> _albumCache = new();

    public static async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken
    )
    {
        var updateFrom = update.CallbackQuery?.From ?? update.Message?.From;
        if (updateFrom is null)
        {
            return;
        }

        var languageCode = updateFrom.LanguageCode;
        var userId = updateFrom.Id;

        await using var dbContext = new BotDbContext();
        var user = await UserRegistrator.GetOrCreateUserAsync(
            userId,
            dbContext,
            languageCode,
            cancellationToken
        );

        var culture = new CultureInfo(user.LanguageCode);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

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

        try
        {
            if (string.IsNullOrEmpty(text) && message.ForwardOrigin is null)
            {
                return;
            }

            if (text.StartsWith("/start"))
            {
                await botClient.SendMessage(
                    chatId,
                    Strings.Welcome,
                    ParseMode.Markdown,
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: cancellationToken
                );
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
                if (message.MediaGroupId is not null)
                {
                    _albumCache.AddOrUpdate(
                        message.MediaGroupId,
                        [message],
                        (
                            k,
                            list
                        ) =>
                        {
                            list.Add(message);
                            return list;
                        }
                    );
                    await Task.Delay(2000, cancellationToken);
                    if (_albumCache.TryRemove(message.MediaGroupId, out var messagesToProcess))
                    {
                        await GitHubService.SaveNoteAsync(botClient, messagesToProcess, user, cancellationToken);
                    }

                    return;
                }

                await GitHubService.SaveNoteAsync(botClient, [message], user, cancellationToken);
            }

            if (dbContext.ChangeTracker.HasChanges())
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch
            (Exception ex)
        {
            Console.WriteLine($@"Error with creating note: {ex.Message}");
            await botClient.SendMessage(
                chatId,
                Strings.ErrorSendMessage,
                cancellationToken: cancellationToken
            );
        }
    }
}