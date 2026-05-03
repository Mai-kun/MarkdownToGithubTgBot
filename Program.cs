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
    Console.WriteLine("Ошибка: Токены не найдены!");
    return;
}

await using (var db = new BotDbContext())
{
    if (db.Database.EnsureCreated())
    {
        Console.WriteLine("База данных подключена");
    }
}

var botClient = new TelegramBotClient(botToken);
using var cts = new CancellationTokenSource();

botClient.StartReceiving(
    UpdateHandler.HandleUpdateAsync,
    ErrorHandler.HandleErrorAsync,
    cancellationToken: cts.Token
);

var me = await botClient.GetMe();
Console.WriteLine($"✅ Бот @{me.Username} запущен и готов к работе.");
Console.WriteLine("Нажми Enter для остановки...");
Console.ReadLine();

cts.Cancel();