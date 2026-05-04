using MdNoteToGithub.Handlers;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;

var config = new ConfigurationBuilder()
             .AddUserSecrets<Program>()
             .AddEnvironmentVariables()
             .Build();

var botToken = config["BotToken"] ?? (args.Length > 0 ? args[0] : null);
if (string.IsNullOrEmpty(botToken))
{
    Console.WriteLine(@"Error: Token not found!");
    return;
}

var botClient = new TelegramBotClient(botToken);
using var cancellationTokenSource = new CancellationTokenSource();

botClient.StartReceiving(
    UpdateHandler.HandleUpdateAsync,
    ErrorHandler.HandleErrorAsync,
    cancellationToken: cancellationTokenSource.Token
);

var me = await botClient.GetMe();
Console.WriteLine($@"Bot @{me.Username} started and ready to work.");
Console.WriteLine(@"Press Enter to stop...");
Console.ReadLine();

cancellationTokenSource.Cancel();