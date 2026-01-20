using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using SiberVatan.Helpers;
using SiberVatan.Interfaces;
using SiberVatan.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace SiberVatan.Handlers
{
    public static class UpdateHandler
    {
        internal static ConcurrentDictionary<long, SpamDetector> UserMessages = [];

        public static Task UpdateReceived(Update update)
        {
            // Start CollectStats task first (fire and forget)
            if (update.Message != null)
            {
                _ = Task.Run(() => CollectStats(update.Message));
            }

            // Then handle the update
            return HandleUpdate(update);
        }

        public static Task CallbackReceived(CallbackQuery query)
        {
            return HandleCallback(query);
        }

        private static bool AddCount(long id, Message m)
        {
            try
            {
                var spamDetector = UserMessages.GetOrAdd(id, _ => new SpamDetector { Messages = [] });
                var lastReplied = spamDetector.Messages
                    .Where(x => x.Replied)
                    .MaxBy(x => x.Time)?.Time ?? DateTime.MinValue;

                var shouldReply = lastReplied < DateTime.UtcNow.AddSeconds(-4);
                spamDetector.Messages.Add(new UserMessage(m) { Replied = shouldReply });
                return !shouldReply;
            }
            catch
            {
                return false;
            }
        }

        private static async Task HandleUpdate(Update update)
        {
            if (update.Message == null || update.Message.From?.Id == 777000) return;
            if (update.Message.Date.ToUniversalTime() < Bot.StartTime.AddMinutes(-2))
            {
                Log.Debug("Ignoring old message from {UserId}", update.Message.From.Id);
                return;
            }

            if (update.Message.ReplyToMessage?.Type is MessageType.ForumTopicCreated or MessageType.ForumTopicReopened)
                update.Message.ReplyToMessage = null;
            if (update.Message.MessageThreadId.HasValue && !update.Message.Chat.IsForum)
            {
                Log.Debug("Clearing thread ID from non-forum chat: {ChatId}", update.Message.Chat.Id);
                update.Message.MessageThreadId = null;
            }

            var id = update.Message.Chat.Id;
            using var banScope = Program.host.Services.CreateScope();
            var banRedis = ServiceHelper.GetService<IRedisHelper>(banScope);
            var bannedGroup = await banRedis.SetContainsAsync("bot:bannedGroups", id.ToString());
            var bannedUser = await banRedis.SetContainsAsync("bot:bannedGroups", update.Message.From.Id.ToString());
            if (bannedGroup || bannedUser)
            {
                if (update.Message.Chat.Type != ChatType.Private && bannedGroup)
                {
                    await Bot.Api.LeaveChat(update.Message.Chat.Id);
                }

                return;
            }

            try
            {
                // Check registration for group messages (except admins and devs)
                if (update.Message.Chat.Type != ChatType.Private && !UpdateHelper.IsGroupAdmin(update) &&
                    !UpdateHelper.Devs.Contains(update.Message.From.Id))
                {
                    using var scope = Program.host.Services.CreateScope();
                    var redis = ServiceHelper.GetService<IRedisHelper>(scope);

                    // Check if registration requirement is enabled
                    var captchaSetting =
                        await redis.HashGetAsync($"chat:{update.Message.Chat.Id}:settings", "NewUsersCaptcha");

                    if (captchaSetting == "no") // Registration is required when NewUsersCaptcha is "no"
                    {
                        // Check if user is registered
                        var registration = await RegistrationHelper.GetRegistration(redis, update.Message.Chat.Id,
                            update.Message.From.Id);
                        if (string.IsNullOrEmpty(registration))
                        {
                            // User not registered, send registration button
                            var botUsername = Bot.Me?.Username ?? "bot";
                            var deepLink = $"https://t.me/{botUsername}?start=register_{update.Message.Chat.Id}";

                            var keyboard = new InlineKeyboardMarkup(new[]
                            {
                                InlineKeyboardButton.WithUrl(Commands.GetLocaleString("registerButton"), deepLink)
                            });

                            await Bot.Api.SendMessage(
                                chatId: update.Message.Chat.Id,
                                text: Commands.GetLocaleString("registrationRequired"),
                                replyParameters: update.Message.MessageId,
                                replyMarkup: keyboard,
                                parseMode: ParseMode.Html
                            );

                            return; // Don't process message further
                        }
                    }

                    // User is registered or registration not required, continue with content filters
                    await TelegramContentFilterHelper.CheckForwardAsync(update.Message, banRedis);
                    await TelegramContentFilterHelper.CheckMediaTypeAsync(update.Message, banRedis);
                    await TelegramContentFilterHelper.CheckTextLengthAsync(update.Message, banRedis);
                    await TelegramContentFilterHelper.CheckFloodAsync(update.Message, banRedis);
                }

                switch (update.Message.Type)
                {
                    case MessageType.Unknown:
                        break;
                    case MessageType.Text:
                        if (update.Message.Text.StartsWith('/') || update.Message.Text.StartsWith('!'))
                        {
                            var userId = update.Message.From.Id;
                            var isAnonymousSender = update.Message.SenderChat != null;

                            // Then check Redis for persistent spam ban
                            using var scope = Program.host.Services.CreateScope();
                            var redis = ServiceHelper.GetService<IRedisHelper>(scope);
                            var isSpammer = await redis.KeyExistsAsync($"spammer:{userId}");
                            if (isSpammer) return;

                            var args = GetParameters(update.Message.Text);
                            args[0] = args[0].ToLower().Replace("@" + Bot.Me.Username.ToLower(), "");

                            var command = Bot.Commands.FirstOrDefault(x =>
                                string.Equals(x.Trigger, args[0], StringComparison.InvariantCultureIgnoreCase));
                            if (command != null)
                            {
                                Bot.MessagesProcessed++;
                                if (isAnonymousSender) return;

                                // Group Only Check
                                if (command.InGroupOnly && update.Message.Chat.Type == ChatType.Private)
                                {
                                    await Bot.Send(Commands.GetLocaleString("GroupOnly"), update.Message.Chat.Id,
                                        messageThreadId: update.Message.MessageThreadId);
                                    return;
                                }

                                // Spam check
                                if (AddCount(update.Message.From.Id, update.Message)) return;

                                // Dev only check
                                if (command.DevOnly && !UpdateHelper.Devs.Contains(update.Message.From.Id))
                                {
                                    await Bot.Send(Commands.GetLocaleString("DevOnly"), update.Message.Chat.Id,
                                        messageThreadId: update.Message.MessageThreadId);
                                    return;
                                }

                                if (command.GroupAdminOnly && !UpdateHelper.IsGroupAdmin(update) &&
                                    !UpdateHelper.Devs.Contains(update.Message.From.Id))
                                {
                                    await Bot.Send(Commands.GetLocaleString("GroupAdminOnly"), update.Message.Chat.Id,
                                        messageThreadId: update.Message.MessageThreadId);
                                    return;
                                }

                                Bot.CommandsReceived++;
                                if (command.MethodAsync != null) await command.MethodAsync.Invoke(update, args);
                            }
                        }
                        else if (update.Message.Text.StartsWith('#') && update.Message.Chat.Type != ChatType.Private)
                        {
                            // Hashtag auto-reply (groups only, if enabled in settings)
                            try
                            {
                                // Check if Extra feature is enabled for this group
                                using var scope = Program.host.Services.CreateScope();
                                var redis = ServiceHelper.GetService<IRedisHelper>(scope);
                                var extraEnabled = await redis.HashGetAsync($"chat:{update.Message.Chat.Id}:settings",
                                    "Extra");

                                if (extraEnabled == "no" || UpdateHelper.IsGroupAdmin(update))
                                {
                                    var messageText = update.Message.Text.Trim();
                                    var firstWord = messageText.Split(' ')[0]; // Get first word (hashtag)

                                    if (firstWord.StartsWith('#'))
                                    {
                                        var extra = await ExtraHelper.GetExtra(firstWord);
                                        if (!string.IsNullOrEmpty(extra))
                                        {
                                            // Parse buttons from the stored content
                                            var (finalText, replyMarkup) =
                                                MessageHelper.ParseButtonsFromPlainText(extra);
                                            // Send the message with HTML parsing and buttons
                                            await Bot.Send(finalText, update.Message.Chat.Id,
                                                customMenu: replyMarkup,
                                                parseMode: ParseMode.Html,
                                                messageThreadId: update.Message.MessageThreadId);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to send hashtag auto-reply");
                            }
                        }
                        else if (update.Message.Chat.Type == ChatType.Private)
                        {
                            // Handle registration name input in private chat
                            if (Commands.UserStates.TryGetValue(update.Message.From.Id, out var state) &&
                                state.Step == Commands.RegistrationStep.AwaitingName)
                            {
                                try
                                {
                                    var nameSurname = update.Message.Text?.Trim();
                                    if (string.IsNullOrWhiteSpace(nameSurname))
                                    {
                                        await Bot.Send(Commands.GetLocaleString("invalidFormat"),
                                            update.Message.Chat.Id);
                                        return;
                                    }

                                    using var scope = Program.host.Services.CreateScope();
                                    var redis = ServiceHelper.GetService<IRedisHelper>(scope);

                                    // Double check if already registered
                                    var existing = await RegistrationHelper.GetRegistration(redis, state.GroupId,
                                        update.Message.From.Id);
                                    if (!string.IsNullOrEmpty(existing))
                                    {
                                        await Bot.Send(Commands.GetLocaleString("alreadyRegistered"),
                                            update.Message.Chat.Id);
                                        Commands.UserStates.Remove(update.Message.From.Id);
                                        return;
                                    }

                                    // Save name to state and move to next step
                                    state.Name = nameSurname;
                                    state.Step = Commands.RegistrationStep.AwaitingAttendance;

                                    // Ask for attendance
                                    var attendanceButtons = new InlineKeyboardMarkup(new[]
                                    {
                                        new[]
                                        {
                                            InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("btnYes"),
                                                "reg|yes"),
                                            InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("btnNo"),
                                                "reg|no")
                                        },
                                        new[]
                                        {
                                            InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("btnMaybe"),
                                                "reg|maybe")
                                        }
                                    });

                                    await Bot.Send(Commands.GetLocaleString("askAttendance"), update.Message.Chat.Id,
                                        customMenu: attendanceButtons, parseMode: ParseMode.Html);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Failed to process registration name");
                                    await Bot.Send(Commands.GetLocaleString("error"), update.Message.Chat.Id);
                                }
                            }
                        }

                        break;
                    case MessageType.NewChatMembers:
                        // Send welcome message to new members
                        if (update.Message.NewChatMembers is { Length: > 0 })
                        {
                            try
                            {
                                using var scope = Program.host.Services.CreateScope();
                                var redis = ServiceHelper.GetService<IRedisHelper>(scope);

                                // Check if welcome is enabled
                                var welcomeEnabled = await redis.HashGetAsync($"chat:{update.Message.Chat.Id}:settings",
                                    "Welcome");

                                if (welcomeEnabled == "no")
                                {
                                    // Get welcome message
                                    var welcomeText = await redis.HashGetAsync($"chat:{update.Message.Chat.Id}:welcome",
                                        "text");

                                    if (!string.IsNullOrWhiteSpace(welcomeText))
                                    {
                                        var mediaType =
                                            await redis.HashGetAsync($"chat:{update.Message.Chat.Id}:welcome",
                                                "mediaType");
                                        var mediaId = await redis.HashGetAsync($"chat:{update.Message.Chat.Id}:welcome",
                                            "mediaId");

                                        // Check DeleteLastWelcome setting
                                        var deleteLastWelcome =
                                            await redis.HashGetAsync($"chat:{update.Message.Chat.Id}:settings",
                                                "DeleteLastWelcome");

                                        // Delete old welcome messages if setting is enabled
                                        if (deleteLastWelcome == "no")
                                        {
                                            var oldMessageIds =
                                                await redis.ListRangeAsync($"chat:{update.Message.Chat.Id}:welcomeIds");
                                            foreach (var msgId in oldMessageIds)
                                            {
                                                try
                                                {
                                                    await Bot.Api.DeleteMessage(update.Message.Chat.Id,
                                                        int.Parse(msgId));
                                                }
                                                catch
                                                {
                                                    // Ignore if message already deleted or not found
                                                }
                                            }

                                            // Clear old IDs
                                            await redis.KeyDeleteAsync($"chat:{update.Message.Chat.Id}:welcomeIds");
                                        }

                                        // Send welcome message to each new member
                                        foreach (var newMember in update.Message.NewChatMembers)
                                        {
                                            // Format message with user placeholders
                                            var formattedText = MessageHelper.FormatWelcomeMessage(welcomeText,
                                                newMember, update.Message.Chat);

                                            // Parse buttons from formatted text
                                            var (finalText, replyMarkup) =
                                                MessageHelper.ParseButtonsFromPlainText(formattedText);

                                            Message sentMessage;

                                            // Send message with media if available
                                            if (!string.IsNullOrEmpty(mediaType) && !string.IsNullOrEmpty(mediaId))
                                            {
                                                sentMessage = mediaType switch
                                                {
                                                    "animation" => await Bot.Api.SendAnimation(
                                                        chatId: update.Message.Chat.Id,
                                                        animation: new InputFileId(mediaId),
                                                        caption: finalText,
                                                        parseMode: ParseMode.Html,
                                                        replyMarkup: replyMarkup,
                                                        messageThreadId: update.Message.MessageThreadId
                                                    ),
                                                    "photo" => await Bot.Api.SendPhoto(
                                                        chatId: update.Message.Chat.Id,
                                                        photo: new InputFileId(mediaId),
                                                        caption: finalText,
                                                        parseMode: ParseMode.Html,
                                                        replyMarkup: replyMarkup,
                                                        messageThreadId: update.Message.MessageThreadId
                                                    ),
                                                    "video" => await Bot.Api.SendVideo(
                                                        chatId: update.Message.Chat.Id,
                                                        video: new InputFileId(mediaId),
                                                        caption: finalText,
                                                        parseMode: ParseMode.Html,
                                                        replyMarkup: replyMarkup,
                                                        messageThreadId: update.Message.MessageThreadId
                                                    ),
                                                    _ => await Bot.Api.SendMessage(
                                                        chatId: update.Message.Chat.Id,
                                                        text: finalText,
                                                        parseMode: ParseMode.Html,
                                                        replyMarkup: replyMarkup,
                                                        messageThreadId: update.Message.MessageThreadId
                                                    )
                                                };
                                            }
                                            else
                                            {
                                                // Send text only
                                                sentMessage = await Bot.Api.SendMessage(
                                                    chatId: update.Message.Chat.Id,
                                                    text: finalText,
                                                    parseMode: ParseMode.Html,
                                                    replyMarkup: replyMarkup,
                                                    messageThreadId: update.Message.MessageThreadId);
                                            }

                                            // Store message ID if DeleteLastWelcome is enabled
                                            if (deleteLastWelcome == "no")
                                            {
                                                await redis.ListPushAsync($"chat:{update.Message.Chat.Id}:welcomeIds",
                                                    sentMessage.MessageId.ToString());
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to send welcome message");
                            }
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling update");
                try
                {
                    await Bot.Send($"Error: {ex.Message}", id);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private static async Task HandleCallback(CallbackQuery query)
        {
            Bot.MessagesProcessed++;
            var userId = query.From.Id;
            var username = query.From.Username ?? query.From.FirstName;
            var callbackData = query.Data ?? "N/A";
            Log.Debug("Callback received from @{Username} ({UserId}): {Data}", username, userId, callbackData);

            if (!string.IsNullOrEmpty(callbackData) && query.Data != null)
            {
                try
                {
                    var args = query.Data.Split('|');
                    var callback = Bot.CallBacks.FirstOrDefault(x =>
                        string.Equals(x.Trigger, args[0], StringComparison.CurrentCultureIgnoreCase));
                    if (callback != null)
                    {
                        if (callback.DevOnly && !UpdateHelper.Devs.Contains(query.From.Id))
                        {
                            await Bot.ReplyToCallback(query, "⛔ Admin only", showAlert: true);
                            return;
                        }

                        if (args.Length >= 2)
                        {
                            if (!string.IsNullOrEmpty(args[1]))
                            {
                                if (callback.GroupAdminOnly)
                                {
                                    //var groupId = long.Parse(args[1]);
                                    if (!UpdateHelper.IsGroupAdmin(query.From.Id, query.Message.Chat.Id) &&
                                        !UpdateHelper.Devs.Contains(query.From.Id))
                                    {
                                        await Bot.ReplyToCallback(query, Commands.GetLocaleString("GroupAdminOnly"),
                                            false, true);
                                        return;
                                    }
                                }
                                else if (callback.UserOnly)
                                {
                                    var userS = long.Parse(args[3]);
                                    if (!UpdateHelper.Devs.Contains(query.From.Id) && query.From.Id != userS)
                                    {
                                        await Bot.ReplyToCallback(query, "⛔️", false, true);
                                        return;
                                    }
                                }
                            }
                        }

                        Bot.CommandsReceived++;
                        if (callback.MethodAsync != null)
                        {
                            await callback.MethodAsync.Invoke(query, args);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in CallbackReceived");
                }
            }
        }


        internal static async Task SpamDetection()
        {
            while (true)
            {
                try
                {
                    using var scope = Program.host.Services.CreateScope();
                    var redis = ServiceHelper.GetService<IRedisHelper>(scope);

                    foreach (var kvp in UserMessages)
                    {
                        var key = kvp.Key;
                        var spamDetector = kvp.Value;

                        try
                        {
                            spamDetector.Messages.RemoveWhere(x => x.Time < DateTime.UtcNow.AddMinutes(-1));
                            if (spamDetector.Messages.Count >= 10)
                            {
                                spamDetector.Warns++;
                                if (spamDetector is { Warns: < 2, Messages.Count: < 20 })
                                {
                                    await Bot.Send("Please do not spam me. Next time is automated ban.", key);
                                    continue;
                                }

                                if ((spamDetector.Warns >= 3 || spamDetector.Messages.Count >= 20) &&
                                    !spamDetector.NotifiedAdmin)
                                {
                                    spamDetector.NotifiedAdmin = true;

                                    // Check if already banned
                                    var isAlreadyBanned = await redis.KeyExistsAsync($"spammer:{key}");
                                    if (isAlreadyBanned)
                                    {
                                        spamDetector.Messages.Clear();
                                        continue;
                                    }

                                    // Get user info and ban count from Redis
                                    var name = await redis.HashGetAsync($"user:{key}", "name") ?? "Unknown";
                                    var tempBanCountStr = await redis.HashGetAsync($"user:{key}", "temp_ban_count");
                                    var count = string.IsNullOrEmpty(tempBanCountStr) ? 0 : int.Parse(tempBanCountStr);
                                    count++;

                                    // Update temp ban count
                                    await redis.HashSetAsync($"user:{key}", "temp_ban_count", count.ToString());

                                    // Calculate ban duration
                                    var banDuration = count switch
                                    {
                                        1 => TimeSpan.FromHours(12),
                                        2 => TimeSpan.FromDays(1),
                                        3 => TimeSpan.FromDays(3),
                                        _ => TimeSpan.FromDays(36500), // ~100 years for permanent
                                    };

                                    // Ban user directly in Redis with TTL
                                    await redis.StringSetAsync($"spammer:{key}", $"Spam/Flood|{count}|{name}",
                                        banDuration);

                                    var unban = count switch
                                    {
                                        1 => "12 hours",
                                        2 => "24 hours",
                                        3 => "3 days",
                                        _ => "Permanent. You have reached the max limit of temp bans for spamming.",
                                    };

                                    await Bot.Send("You have been banned for spamming. Your ban period is: " + unban,
                                        key);
                                    Log.Information(
                                        "User {Key} ({Name}) banned for spam. Ban count: {Count}, Duration: {BanDuration}",
                                        key, name, count, banDuration);
                                }

                                spamDetector.Messages.Clear();
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
                catch
                {
                    // ignored
                }

                await Task.Delay(2000);
            }
        }

        private static async Task CollectStats(Message updateMessage)
        {
            try
            {
                using var scope = Program.host.Services.CreateScope();
                var redis = ServiceHelper.GetService<IRedisHelper>(scope);

                // Increment general message counter
                await redis.HashIncrementAsync("bot:general", "messages");
                // Store user information
                await redis.HashSetAsync($"user:{updateMessage.From.Id}", "name", updateMessage.From.FirstName);

                if (updateMessage.From?.Username != null)
                {
                    await redis.HashSetAsync("bot:usernames", $"@{updateMessage.From.Username.ToLower()}",
                        updateMessage.From.Id.ToString());
                    await redis.HashSetAsync($"user:{updateMessage.From.Id}", "username",
                        $"@{updateMessage.From.Username.ToLower()}");
                }

                if (updateMessage.ForwardFrom?.Username != null)
                {
                    await redis.HashSetAsync("bot:usernames", $"@{updateMessage.ForwardFrom.Username.ToLower()}",
                        updateMessage.ForwardFrom.Id.ToString());
                    await redis.HashSetAsync($"user:{updateMessage.ForwardFrom.Id}", "username",
                        $"@{updateMessage.ForwardFrom.Username.ToLower()}");
                }

                // Chat statistics (non-private chats only)
                if (updateMessage.Chat.Type != ChatType.Private)
                {
                    await redis.HashSetAsync($"chat:{updateMessage.Chat.Id}:details", "name", updateMessage.Chat.Title);
                    if (updateMessage.From != null)
                    {
                        // Use HashIncrement for atomic counter operations
                        await redis.HashIncrementAsync($"chat:{updateMessage.From.Id}", "msgs");
                        await redis.HashIncrementAsync($"{updateMessage.Chat.Id}:users:{updateMessage.From.Id}",
                            "msgs");

                        await redis.HashSetAsync($"chat:{updateMessage.Chat.Id}:userlast",
                            updateMessage.From.Id.ToString(), DateTime.Now.Ticks.ToString());
                        await redis.StringSetAsync($"chat:{updateMessage.Chat.Id}:chatlast",
                            DateTime.Now.Ticks.ToString());
                    }

                    // Migration tracking sets
                    var updated = await redis.SetContainsAsync("lenghtUpdate3", updateMessage.Chat.Id.ToString());
                    if (!updated)
                    {
                        await redis.SetAddAsync("lenghtUpdate3", updateMessage.Chat.Id.ToString());
                    }

                    updated = await redis.SetContainsAsync("lenghtUpdate", updateMessage.Chat.Id.ToString());
                    if (!updated)
                    {
                        await redis.SetAddAsync("lenghtUpdate", updateMessage.Chat.Id.ToString());
                    }

                    updated = await redis.SetContainsAsync("dbUpdate:lenghtUpdate", updateMessage.Chat.Id.ToString());
                    if (!updated)
                    {
                        await redis.SetAddAsync("dbUpdate:lenghtUpdate", updateMessage.Chat.Id.ToString());
                    }

                    updated = await redis.SetContainsAsync("dbUpdate:lenghtUpdat4",
                        $"{updateMessage.Chat.Id}:{updateMessage.From.Id}");
                    if (!updated)
                    {
                        await redis.SetAddAsync("dbUpdate:lenghtUpdat4",
                            $"{updateMessage.Chat.Id}:{updateMessage.From.Id}");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error in CollectStats");
            }
        }

        private static string[]? GetParameters(string? input)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= 1) return null;
            int spaceIndex = input.IndexOf(' ');
            if (spaceIndex > 0)
            {
                string firstPart = input[1..spaceIndex].Trim();
                string secondPart = input[(spaceIndex + 1)..].Trim();
                return [firstPart, secondPart];
            }

            return [input[1..].Trim(), string.Empty];
        }
    }
}
