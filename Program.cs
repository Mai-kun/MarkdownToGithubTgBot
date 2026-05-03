using Microsoft.Extensions.Configuration;
using ObsidianTgBot.DataBase;
using ObsidianTgBot.Handlers;
using Telegram.Bot;

var config = new ConfigurationBuilder()
             .AddUserSecrets<Program>()
             .AddEnvironmentVariables()
             .Build();

var botToken = config["BotToken"];
if (string.IsNullOrEmpty(botToken))
{
    Console.WriteLine(@"Error: Token not found!");
    return;
}

await using (var db = new BotDbContext())
{
    if (db.Database.EnsureCreated())
    {
        Console.WriteLine(@"Database connected.");
    }
}

var botClient = new TelegramBotClient(botToken);
using var cancellationTokenSource = new CancellationTokenSource();

botClient.StartReceiving(
    UpdateHandler.HandleUpdateAsync,
    ErrorHandler.HandleErrorAsync,
    cancellationToken: cancellationTokenSource.Token
);

var me = await botClient.GetMe();
Console.WriteLine($@"✅ Bot @{me.Username} started and ready to work.");
Console.WriteLine(@"Press Enter to stop...");
Console.ReadLine();

cancellationTokenSource.Cancel();