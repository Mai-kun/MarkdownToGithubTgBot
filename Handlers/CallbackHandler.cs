using MdNoteToGithub.Models;
using MdNoteToGithub.Resources;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace MdNoteToGithub.Handlers;

public static class CallbackHandler
{
    public static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        UserSettings user,
        CancellationToken ct)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var messageId = callbackQuery.Message.MessageId;

        switch (callbackQuery.Data)
        {
            case "toggle_img":
                user.NeedDownloadImages = !user.NeedDownloadImages;
                break;
            case "toggle_lang":
                user.LanguageCode = user.LanguageCode == "ru" ? "en" : "ru";
                break;
        }

        await ShowSettingsMenuAsync(botClient, chatId, user, ct, messageId);

        await botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            Strings.SettingsUpdated,
            cancellationToken: ct
        );
    }

    public static async Task ShowSettingsMenuAsync(ITelegramBotClient botClient, long chatId, UserSettings user,
        CancellationToken ct, int? messageIdToEdit = null)
    {
        var imgStatus = user.NeedDownloadImages ? $"{Strings.On}" : $"{Strings.Off}";
        var langStatus = user.LanguageCode == "ru" ? Strings.Russian : Strings.English;

        var inlineKeyboard = new InlineKeyboardMarkup([
            [InlineKeyboardButton.WithCallbackData($"{Strings.DownloadImages}: {imgStatus}", "toggle_img")],
            [InlineKeyboardButton.WithCallbackData($"{Strings.InfoLanguage}: {langStatus}", "toggle_lang")],
        ]);

        if (messageIdToEdit.HasValue)
        {
            await botClient.EditMessageText(
                chatId,
                messageIdToEdit.Value,
                $"⚙{Strings.BotSettings}",
                replyMarkup: inlineKeyboard,
                cancellationToken: ct
            );
        }
        else
        {
            await botClient.SendMessage(
                chatId,
                $"{Strings.BotSettings}",
                replyMarkup: inlineKeyboard,
                cancellationToken: ct
            );
        }
    }
}