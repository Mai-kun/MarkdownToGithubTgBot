using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace MdNoteToGithub.Handlers;

public static class ErrorHandler
{
    public static Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString(),
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }
}