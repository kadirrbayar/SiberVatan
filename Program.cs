using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using SiberVatan.Handlers;
using SiberVatan.Helpers;
using SiberVatan.Interfaces;
using SiberVatan.Services;

namespace SiberVatan
{
    class Program
    {
        public static IHost? host;
        private static System.Timers.Timer? _timer;
        internal static float MessagePxPerSecond, MessageRxPerSecond, MessageTxPerSecond;
        private static long _previousMessages, _previousMessagesTx, _previousMessagesRx;
        internal static List<long> MessagesReceived = [];
        internal static List<long> MessagesProcessed = [];
        internal static List<long> MessagesSent = [];

        public static void Load()
        {
            var solutionRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName;
            Console.WriteLine(solutionRoot);
            var envPath = Path.Combine(solutionRoot ?? ".", ".env");
            Env.Load(envPath);
        }

        static async Task Main(string[] args)
        {
            Load();
            ConfigureLogging();

            try
            {
                Console.Title = "Siber Vatan v1.0";
                Log.Information("Starting Bot v1.0");

                EnsureSingleInstance();

                var builder = Host.CreateDefaultBuilder(args)
                       .UseSerilog()
                       .ConfigureServices((_, services) =>
                       {
                           services.AddSingleton<IRedisHelper, RedisHelper>();
                       });

                host = builder.Build();

                //builder.Host.UseSerilog();
                //builder.Logging.ClearProviders();
                //builder.Services.AddDb();
                //builder.Services.AddServices();
                //builder.Services.AddScoped<IRedisHelper, RedisHelper>();
                //var app = builder.Build();
                //host = app;

                var telegramApiKey = Environment.GetEnvironmentVariable("HELP_API_KEY") ?? throw new Exception("API not found");
                Log.Information("Initializing Telegram Bot with POLLING");
                await Bot.Initialize(telegramApiKey);

                Log.Information("Starting polling mode...");

                _ = Task.Run(Bot.StartPolling);
                _ = Task.Run(UpdateHandler.SpamDetection);

                StartStatisticsTimer();
                Log.Warning("Bot started successfully.");

                await Task.Delay(Timeout.Infinite);
                //await app.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
                throw;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        private static void StartStatisticsTimer()
        {
            _timer = new System.Timers.Timer();
            _timer.Elapsed += TimerOnTick;
            _timer.Interval = 1000; // 1 second
            _timer.Enabled = true;
        }

        private static void TimerOnTick(object? sender, EventArgs? eventArgs)
        {
            try
            {
                var newMessages = Bot.MessagesProcessed - _previousMessages;
                _previousMessages = Bot.MessagesProcessed;
                MessagesProcessed.Insert(0, newMessages);
                if (MessagesProcessed.Count > 60)
                    MessagesProcessed.RemoveAt(60);
                MessagePxPerSecond = MessagesProcessed.Max();

                newMessages = Bot.MessagesSent - _previousMessagesTx;
                _previousMessagesTx = Bot.MessagesSent;
                MessagesSent.Insert(0, newMessages);
                if (MessagesSent.Count > 60)
                    MessagesSent.RemoveAt(60);
                MessageTxPerSecond = MessagesSent.Max();

                newMessages = Bot.MessagesReceived - _previousMessagesRx;
                _previousMessagesRx = Bot.MessagesReceived;
                MessagesReceived.Insert(0, newMessages);
                if (MessagesReceived.Count > 60)
                    MessagesReceived.RemoveAt(60);
                MessageRxPerSecond = MessagesReceived.Max();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in timer tick");
            }
        }

        public static void ConfigureLogging()
        {
            var logLevel = Enum.TryParse(Environment.GetEnvironmentVariable("LOG_LEVEL"), true, out LogEventLevel lvl)
                ? lvl
                : LogEventLevel.Warning;
            var retentionDays = 15;
            var projectName = "help";

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Error)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Async(a => a.Console(
                    restrictedToMinimumLevel: logLevel,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                ))
                .WriteTo.Async(a => a.File(
                    path: $"logs/{projectName}-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: retentionDays,
                    buffered: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                ));

            Log.Logger = loggerConfig.CreateLogger();
        }

        private static void EnsureSingleInstance()
        {
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                Log.Warning("Another instance is already running. Exiting.");
                Environment.Exit(2);
            }
        }
    }
}
