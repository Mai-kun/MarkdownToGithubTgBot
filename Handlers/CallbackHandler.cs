using System.Globalization;
using MdNoteToGithub.Models;
using MdNoteToGithub.Resources;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace MdNoteToGithub.Handlers;

public static class CallbackHandler
{
    private const string ToggleImgData = "toggle_img";
    private const string ToggleLangData = "toggle_lang";

    public static async Task HandleCallbackQueryAsync(
        ITelegramBotClient botClient,
        CallbackQuery callbackQuery,
        UserSettings user,
        CancellationToken ct
    )
    {
        if (callbackQuery.Message is not { } message || string.IsNullOrEmpty(callbackQuery.Data))
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);
            return;
        }

        switch (callbackQuery.Data)
        {
            case ToggleImgData:
                user.NeedDownloadImages = !user.NeedDownloadImages;
                break;
            case ToggleLangData:
                SetLanguage(user);
                break;
            default:
                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);
                return;
        }

        var updateTask = ShowSettingsMenuAsync(botClient, message.Chat.Id, user, ct, message.MessageId);
        var answerTask = botClient.AnswerCallbackQuery(callbackQuery.Id, Strings.SettingsUpdated, cancellationToken: ct);

        await Task.WhenAll(updateTask, answerTask);
    }

    private static void SetLanguage(UserSettings user)
    {
        user.LanguageCode = user.LanguageCode is "ru" ? "en" : "ru";
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(user.LanguageCode);
    }

    public static async Task ShowSettingsMenuAsync(
        ITelegramBotClient botClient,
        long chatId,
        UserSettings user,
        CancellationToken ct,
        int? messageIdToEdit = null
    )
    {
        var imgStatus = user.NeedDownloadImages ? Strings.On : Strings.Off;
        var langStatus = user.LanguageCode is "ru" ? Strings.Russian : Strings.English;

        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                [InlineKeyboardButton.WithCallbackData($"{Strings.DownloadImages}: {imgStatus}", ToggleImgData)],
                [InlineKeyboardButton.WithCallbackData($"{Strings.InfoLanguage}: {langStatus}", ToggleLangData)],
            ]
        );

        var task = messageIdToEdit is { } messageId
            ? botClient.EditMessageText(
                chatId,
                messageId,
                Strings.BotSettings,
                replyMarkup: inlineKeyboard,
                cancellationToken: ct
            )
            : botClient.SendMessage(
                chatId,
                Strings.BotSettings,
                replyMarkup: inlineKeyboard,
                cancellationToken: ct
            );

        await task;
    }
}