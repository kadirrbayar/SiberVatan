using Serilog;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using SiberVatan.Handlers;
using SiberVatan.Models;
using SiberVatan.Interfaces;

namespace SiberVatan.Helpers
{
    internal static class Bot
    {
        public static TelegramBotClient? Api;
        public static User? Me;
        public static DateTime StartTime = DateTime.UtcNow;
        public static bool Running = true;
        public static int MessagesSent = 0;
        public static long MessagesProcessed = 0;
        public static long MessagesReceived = 0;
        public static Random R = new();
        public static XDocument? English;
        private static readonly SemaphoreSlim _messageSemaphore = new(30, 30);
        private static DateTime _lastMessageTime = DateTime.UtcNow;
        public static long CommandsReceived = 0;

        internal static string RootDirectory
        {
            get
            {
#if DEBUG
                return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
#else
                return AppContext.BaseDirectory;
#endif
            }
        }

        internal static string LanguageDirectory => Path.Combine(RootDirectory, "Languages");

        internal static List<Command> Commands = [];
        internal static List<CallBack> CallBacks = [];

        public static int MessageOffset { get; set; }
        public static bool IsReceiving { get; set; }

        internal delegate Task ChatCommandMethodAsync(Update u, string[] args);

        internal delegate Task ChatCallbackMethodAsync(CallbackQuery u, string[] args);

        public static async Task Initialize(string telegramApi)
        {
            try
            {
                Api = new TelegramBotClient(telegramApi);
                English = LanguageHelper.Load(Path.Combine(LanguageDirectory, "tr.yaml"));
                Log.Information("Loading commands...");

                // Load commands
                foreach (var m in typeof(Commands).GetMethods())
                {
                    var c = new Command();
                    foreach (var a in m.GetCustomAttributes(true))
                    {
                        if (a is Attributes.Command ca)
                        {
                            c.InGroupOnly = ca.InGroupOnly;
                            c.GroupAdminOnly = ca.GroupAdminOnly;
                            c.DevOnly = ca.DevOnly;
                            c.Trigger = ca.Trigger ?? string.Empty;

                            var parameters = m.GetParameters();
                            var returnType = m.ReturnType;

                            if (returnType == typeof(Task))
                            {
                                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(Update) &&
                                    parameters[1].ParameterType == typeof(string[]))
                                {
                                    c.MethodAsync =
                                        (ChatCommandMethodAsync)Delegate.CreateDelegate(typeof(ChatCommandMethodAsync),
                                            m);
                                }
                                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Update))
                                {
                                    c.MethodAsync = async (u, _) =>
                                    {
                                        var result = m.Invoke(null, [u]);
                                        if (result is Task task)
                                            await task;
                                    };
                                }
                            }

                            Commands.Add(c);
                        }
                    }
                }

                Log.Information("Loaded {CommandCount} commands", Commands.Count);

                // Initialize ExtraHelper with Redis
                if (Program.host?.Services.GetService(typeof(IRedisHelper)) is IRedisHelper redis)
                {
                    ExtraHelper.Initialize(redis);
                    Log.Information("ExtraHelper initialized");
                }
                else
                {
                    Log.Warning("Redis not available, ExtraHelper not initialized");
                }

                // Load callbacks
                foreach (var m in typeof(CallbackCommand).GetMethods())
                {
                    var c = new CallBack();
                    foreach (var a in m.GetCustomAttributes(true))
                    {
                        if (a is Attributes.CallBack ca)
                        {
                            c.DevOnly = ca.DevOnly;
                            c.UserOnly = ca.UserOnly;
                            c.Trigger = ca.Trigger;
                            c.GroupAdminOnly = ca.GroupAdminOnly;

                            var parameters = m.GetParameters();
                            var returnType = m.ReturnType;

                            if (returnType == typeof(Task))
                            {
                                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(CallbackQuery) &&
                                    parameters[1].ParameterType == typeof(string[]))
                                {
                                    c.MethodAsync =
                                        (ChatCallbackMethodAsync)Delegate.CreateDelegate(
                                            typeof(ChatCallbackMethodAsync), m);
                                }
                            }

                            CallBacks.Add(c);
                        }
                    }
                }

                Log.Information("Loaded {CallbackCount} callbacks", CallBacks.Count);

                Me = await Api.GetMe();
                Log.Information("Bot authenticated as @{BotUsername} (ID: {BotId})", Me.Username, Me.Id);

                StartTime = DateTime.UtcNow;
                Log.Information("Bot initialization completed");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize bot");
                throw;
            }
        }

        public static async Task StartPolling()
        {
            IsReceiving = true;
            Log.Information("Starting to receive updates via polling...");

            var cts = new CancellationTokenSource();
            while (Running && !cts.Token.IsCancellationRequested)
            {
                try
                {
                    var updates = await Api.GetUpdates(
                        MessageOffset,
                        timeout: 30,
                        limit: 100,
                        allowedUpdates: [UpdateType.Message, UpdateType.CallbackQuery],
                        cancellationToken: cts.Token
                    );

                    MessagesReceived += updates.Length;
                    if (updates.Length > 0)
                    {
                        Log.Debug("Received {UpdateCount} updates", updates.Length);
                        await Task.WhenAll(updates.Select(async update =>
                        {
                            try
                            {
                                await UpdateReceived(update);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Error processing update {UpdateId}", update.Id);
                            }
                        }));
                        MessageOffset = updates[^1].Id + 1;
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Information("Polling cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in polling loop");
                    await Task.Delay(5000, cts.Token);
                }
            }

            IsReceiving = false;
            Log.Information("Stopped receiving updates");
        }

        public static async Task UpdateReceived(Update update)
        {
            try
            {
                if (update.Type == UpdateType.EditedMessage) return;
                MessagesProcessed++;
                switch (update.Type)
                {
                    case UpdateType.Message:
                        await UpdateHandler.UpdateReceived(update);
                        break;
                    case UpdateType.CallbackQuery:
                        await UpdateHandler.CallbackReceived(update.CallbackQuery);
                        break;
                    default:
                        Log.Debug("Unhandled update type: {UpdateType}", update.Type);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing update {UpdateId}", update.Id);
            }
        }

        internal static async Task<Message?> Send(string message, long id, bool clearKeyboard = false,
            InlineKeyboardMarkup? customMenu = null, ParseMode parseMode = ParseMode.Html, int? messageThreadId = null)
        {
            await _messageSemaphore.WaitAsync();
            try
            {
                var timeSinceLastMessage = DateTime.UtcNow - _lastMessageTime;
                if (timeSinceLastMessage.TotalMilliseconds < 33)
                    await Task.Delay(33 - (int)timeSinceLastMessage.TotalMilliseconds);

                _lastMessageTime = DateTime.UtcNow;
                try
                {
                    Message sentMessage;
                    if (clearKeyboard || customMenu != null)
                    {
                        sentMessage = await Api.SendMessage(id, message, replyMarkup: customMenu,
                            linkPreviewOptions: true, parseMode: parseMode, messageThreadId: messageThreadId);
                    }
                    else
                    {
                        sentMessage = await Api.SendMessage(id, message, linkPreviewOptions: true, parseMode: parseMode,
                            messageThreadId: messageThreadId);
                    }

                    Log.Debug("Message sent to {ChatId}", id);
                    return sentMessage;
                }
                catch (ApiRequestException apiEx)
                {
                    Log.Warning(apiEx, "Failed to send message to {ChatId}: {ErrorCode} - {Description}",
                        id, apiEx.ErrorCode, apiEx.Message);
                    return null;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unexpected error sending message to {ChatId}", id);
                    return null;
                }
            }
            finally
            {
                _messageSemaphore.Release();
            }
        }

        internal static async Task<Message?> Edit(long chatId, int messageId, string text,
            InlineKeyboardMarkup? replyMarkup = null, bool disableWebPagePreview = false)
        {
            try
            {
                var sentMessage = await Api.EditMessageText(chatId, messageId, text, ParseMode.Html,
                    replyMarkup: replyMarkup, linkPreviewOptions: disableWebPagePreview);

                Log.Debug("Message edited in {ChatId}", chatId);
                return sentMessage;
            }
            catch (ApiRequestException apiEx)
            {
                Log.Warning(apiEx, "Failed to edit message in {ChatId}: {ErrorCode}", chatId, apiEx.ErrorCode);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error editing message in {ChatId}", chatId);
                return null;
            }
        }

        internal static async Task ReplyToCallback(CallbackQuery query, string? text = null, bool edit = true,
            bool showAlert = false, InlineKeyboardMarkup? replyMarkup = null, bool disableWebPagePreview = false)
        {
            try
            {
                await Api.AnswerCallbackQuery(query.Id, edit ? null : text, showAlert);
                if (edit && text != null)
                    await Edit(query, text, replyMarkup, disableWebPagePreview);
            }
            catch (ApiRequestException apiEx)
            {
                Log.Warning(apiEx, "Failed to answer callback query: {ErrorCode}", apiEx.ErrorCode);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error answering callback query");
            }
        }

        internal static async Task<Message?> Edit(CallbackQuery query, string text,
            InlineKeyboardMarkup? replyMarkup = null, bool disableWebPagePreview = false)
        {
            try
            {
                Message sentMessage;
                sentMessage = await Api.EditMessageText(query.Message.Chat.Id, query.Message.Id, text, ParseMode.Html,
                    replyMarkup: replyMarkup, linkPreviewOptions: disableWebPagePreview);
                return sentMessage;
            }
            catch
            {
                return null;
            }
        }

        internal static Task<Message>? EditReplyMarkup(CallbackQuery query, InlineKeyboardMarkup? replyMarkup = null)
        {
            try
            {
                return Api.EditMessageReplyMarkup(query.Message.Chat.Id, query.Message.Id, replyMarkup: replyMarkup);
            }
            catch
            {
                //ignored
            }

            return null;
        }
    }
}
