using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SiberVatan.Helpers;
using SiberVatan.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SiberVatan
{
    public static partial class Commands
    {
        public enum RegistrationStep
        {
            AwaitingName,
            AwaitingAttendance
        }

        public class RegistrationState
        {
            public long GroupId { get; set; }
            public RegistrationStep Step { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        // User state for registration flow
        public static readonly Dictionary<long, RegistrationState> UserStates = new();

        [Attributes.Command(Trigger = "start")]
        public static async Task Start(Update update, string[] args)
        {
            var message = update.Message!;

            // Check for deep link parameter
            if (args.Length > 1 && args[1].StartsWith("register_"))
            {
                try
                {
                    var groupIdStr = args[1].Replace("register_", "");
                    if (!long.TryParse(groupIdStr, out var groupId))
                    {
                        await Bot.Send(GetLocaleString("invalidFormat"), message.Chat.Id);
                        return;
                    }

                    using var scope = Program.host.Services.CreateScope();
                    var redis = ServiceHelper.GetService<IRedisHelper>(scope);

                    // Check if already registered
                    var existingName = await RegistrationHelper.GetRegistration(redis, groupId, message.From.Id);
                    if (!string.IsNullOrEmpty(existingName))
                    {
                        await Bot.Send(GetLocaleString("alreadyRegistered"), message.Chat.Id);
                        return;
                    }

                    // Check if user is member of the group
                    var isMember = await RegistrationHelper.IsMemberOfGroup(Bot.Api, groupId, message.From.Id);
                    if (!isMember)
                    {
                        await Bot.Send(GetLocaleString("notMember"), message.Chat.Id);
                        return;
                    }

                    var groupTitle = await RegistrationHelper.GetGroupTitle(redis, groupId);
                    UserStates.Remove(message.From.Id);
                    UserStates.Add(message.From.Id, new RegistrationState
                    {
                        GroupId = groupId,
                        Step = RegistrationStep.AwaitingName
                    });

                    await Bot.Send(GetLocaleString("welcomeUserMsg", groupTitle), message.Chat.Id, parseMode: ParseMode.Html);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to handle registration deep link");
                    await Bot.Send(GetLocaleString("extraError", ex.Message), message.Chat.Id);
                }
            }
            else
            {
                // Normal start message
                var msg = GetLocaleString("WelcomeMessage", message.From.FirstName);
                await Bot.Send(msg, message.Chat.Id, messageThreadId: message.MessageThreadId);
            }
        }

        [Attributes.Command(Trigger = "sendmsg", DevOnly = true)]
        public static async Task SendMsg(Update update, string[] args)
        {
            var message = update.Message!;
            var (targets, rawContent) = MessageHelper.ParseTargetsAndContent(args);
            if (targets.Count <= 0)
            {
                await Bot.Send("⚠️ No valid targets found.", message.Chat.Id);
                return;
            }

            var replyMsg = update.Message.ReplyToMessage;
            if (string.IsNullOrEmpty(rawContent) && replyMsg == null)
            {
                await Bot.Send("⚠️ Message content missing.", message.Chat.Id);
                return;
            }

            string? finalText = null;
            InlineKeyboardMarkup? replyMarkup = null;

            if (!string.IsNullOrEmpty(rawContent))
            {
                var fullMessageText = message.Text ?? message.Caption ?? string.Empty;

                var commandPart = $"/{args[0]}";
                var targetsPart = args.Length > 1 ? args[1].Split(' ')[0] : "";
                var contentStartOffset = commandPart.Length + 1 + targetsPart.Length + 1;

                if (fullMessageText.Length > contentStartOffset)
                {
                    var contentText = fullMessageText[contentStartOffset..];
                    (finalText, replyMarkup) = MessageHelper.ParseButtonsFromPlainText(contentText);
                }
            }

            var msgText = !string.IsNullOrEmpty(finalText) ? finalText : ".";
            var parseMode = ParseMode.Html;
            var successIds = new List<string>();
            var failedIds = new List<string>();

            async Task DeliverTo(long chatId)
            {
                if (!string.IsNullOrEmpty(finalText) || replyMarkup != null ||
                    (!string.IsNullOrEmpty(rawContent) && replyMsg == null))
                {
                    if (message.Photo is { Length: > 0 })
                    {
                        await Bot.Api.SendPhoto(chatId, message.Photo.Last().FileId, caption: finalText,
                            parseMode: parseMode, replyMarkup: replyMarkup);
                    }
                    else if (message.Video != null)
                    {
                        await Bot.Api.SendVideo(chatId, message.Video.FileId, caption: finalText, parseMode: parseMode,
                            replyMarkup: replyMarkup);
                    }
                    else if (message.Document != null)
                    {
                        await Bot.Api.SendDocument(chatId, message.Document.FileId, caption: finalText,
                            parseMode: parseMode, replyMarkup: replyMarkup);
                    }
                    else if (message.Voice != null)
                    {
                        await Bot.Api.SendVoice(chatId, message.Voice.FileId, caption: finalText, parseMode: parseMode,
                            replyMarkup: replyMarkup);
                    }
                    else if (message.Audio != null)
                    {
                        await Bot.Api.SendAudio(chatId, message.Audio.FileId, caption: finalText, parseMode: parseMode,
                            replyMarkup: replyMarkup);
                    }
                    else if (message.Sticker != null)
                    {
                        await Bot.Api.SendSticker(chatId, message.Sticker.FileId, replyMarkup: replyMarkup);
                    }
                    else
                    {
                        await Bot.Send(msgText, chatId, customMenu: replyMarkup);
                    }
                }
                else if (replyMsg != null)
                {
                    await Bot.Api.ForwardMessage(chatId, replyMsg.Chat.Id, replyMsg.MessageId);
                }
            }

            foreach (var target in targets)
            {
                if (!long.TryParse(target, out var chatId))
                {
                    failedIds.Add($"{target} (Geçersiz ID)");
                    continue;
                }

                try
                {
                    await DeliverTo(chatId);
                    successIds.Add(target);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{Target} adresine gönderilemedi", target);
                    failedIds.Add(target);
                }
            }

            if (successIds.Count > 0)
            {
                try
                {
                    await DeliverTo(message.Chat.Id);
                }
                catch
                {
                    /* Ignore failure to send copy to admin */
                }
            }

            var reportText =
                $"Mesaj Gönderme Raporu:\n✅ Gönderilen: {successIds.Count}\n❌ Gönderilemeyen: {failedIds.Count}\n";
            if (successIds.Count > 0)
                reportText += $"\nBaşarılı ID'ler:\n{string.Join(", ", successIds)}\n";
            if (failedIds.Count > 0)
                reportText += $"\nBaşarısız ID'ler:\n{string.Join(", ", failedIds)}";

            await Bot.Send(reportText, message.Chat.Id, messageThreadId: message.MessageThreadId);
        }

        [Attributes.Command(Trigger = "id")]
        public static Task Id(Update update, string[] args)
        {
            return Bot.Send($"🆔 <b>Chat ID:</b> <code>{update.Message.Chat.Id}</code>", update.Message.Chat.Id);
        }

        [Attributes.Command(Trigger = "broadcast", DevOnly = true)]
        public static async Task Broadcast(Update update, string[] args)
        {
            var message = update.Message!;

            var fullMessageText = message.Text ?? message.Caption ?? string.Empty;
            var splitText = fullMessageText.Split(' ', 2);
            var rawContent = splitText.Length > 1 ? splitText[1] : string.Empty;

            var replyMsg = message.ReplyToMessage;

            if (string.IsNullOrEmpty(rawContent) && replyMsg == null)
            {
                await Bot.Send(Commands.GetLocaleString("broadcastContentRequired"), message.Chat.Id);
                return;
            }

            string? finalText = null;
            InlineKeyboardMarkup? replyMarkup = null;

            if (!string.IsNullOrEmpty(rawContent))
            {
                (finalText, replyMarkup) = MessageHelper.ParseButtonsFromPlainText(rawContent);
            }

            var msgText = !string.IsNullOrEmpty(finalText) ? finalText : ".";
            var parseMode = ParseMode.Html;
            var successIds = new List<string>();
            var failedIds = new List<string>();

            async Task DeliverTo(long chatId)
            {
                if (!string.IsNullOrEmpty(finalText) || replyMarkup != null ||
                    (!string.IsNullOrEmpty(rawContent) && replyMsg == null))
                {
                    if (message.Photo is { Length: > 0 })
                    {
                        await Bot.Api.SendPhoto(chatId, message.Photo.Last().FileId, caption: finalText,
                            parseMode: parseMode, replyMarkup: replyMarkup);
                    }
                    else if (message.Video != null)
                    {
                        await Bot.Api.SendVideo(chatId, message.Video.FileId, caption: finalText, parseMode: parseMode,
                            replyMarkup: replyMarkup);
                    }
                    else if (message.Document != null)
                    {
                        await Bot.Api.SendDocument(chatId, message.Document.FileId, caption: finalText,
                            parseMode: parseMode, replyMarkup: replyMarkup);
                    }
                    else if (message.Voice != null)
                    {
                        await Bot.Api.SendVoice(chatId, message.Voice.FileId, caption: finalText, parseMode: parseMode,
                            replyMarkup: replyMarkup);
                    }
                    else if (message.Audio != null)
                    {
                        await Bot.Api.SendAudio(chatId, message.Audio.FileId, caption: finalText, parseMode: parseMode,
                            replyMarkup: replyMarkup);
                    }
                    else if (message.Sticker != null)
                    {
                        await Bot.Api.SendSticker(chatId, message.Sticker.FileId, replyMarkup: replyMarkup);
                    }
                    else
                    {
                        await Bot.Send(msgText, chatId, customMenu: replyMarkup);
                    }
                }
                else if (replyMsg != null)
                {
                    await Bot.Api.ForwardMessage(chatId, replyMsg.Chat.Id, replyMsg.MessageId);
                }
            }

            using var scope = Program.host.Services.CreateScope();
            var redis = ServiceHelper.GetService<IRedisHelper>(scope);
            var targets = await RegistrationHelper.GetAllGroups(redis);

            if (targets.Count == 0)
            {
                await Bot.Send(Commands.GetLocaleString("broadcastNoGroups"), message.Chat.Id);
                return;
            }

            await Bot.Send(Commands.GetLocaleString("broadcastSending", targets.Count), message.Chat.Id);

            foreach (var target in targets)
            {
                if (!long.TryParse(target, out var chatId))
                {
                    failedIds.Add($"{target} (Invalid)");
                    continue;
                }

                try
                {
                    await DeliverTo(chatId);
                    successIds.Add(target);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Broadcast failed for {Target}", target);
                    failedIds.Add(target);
                }
            }

            var reportText = Commands.GetLocaleString("broadcastReport", successIds.Count, failedIds.Count);

            if (failedIds.Count > 0)
                reportText += Commands.GetLocaleString("broadcastFailedIds", string.Join(", ", failedIds));

            await Bot.Send(reportText, message.Chat.Id, messageThreadId: message.MessageThreadId);
        }

        [Attributes.Command(Trigger = "register", InGroupOnly = true, GroupAdminOnly = true)]
        public static async Task Register(Update update, string[] args)
        {
            var message = update.Message!;

            try
            {
                using var scope = Program.host.Services.CreateScope();
                var redis = ServiceHelper.GetService<IRedisHelper>(scope);

                // Store group info
                await RegistrationHelper.StoreGroupInfo(redis, message.Chat.Id, message.Chat.Title ?? "Unknown Group");

                // Generate deep link
                var botUsername = Bot.Me?.Username ?? "bot";
                var deepLink = $"https://t.me/{botUsername}?start=register_{message.Chat.Id}";

                // Create button
                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    InlineKeyboardButton.WithUrl(GetLocaleString("registerButton"), deepLink)
                });

                // Send message
                await Bot.Send(
                    GetLocaleString("registerMessage"),
                    message.Chat.Id,
                    customMenu: keyboard,
                    parseMode: ParseMode.Html,
                    messageThreadId: message.MessageThreadId
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send registration message");
                await Bot.Send(GetLocaleString("extraError", ex.Message), message.Chat.Id);
            }
        }

        [Attributes.Command(Trigger = "extralist")]
        public static async Task ExtraList(Update update, string[] args)
        {
            var message = update.Message!;
            try
            {
                // In private chat, only devs can use this command
                if (message.Chat.Type == ChatType.Private)
                {
                    if (!UpdateHelper.Devs.Contains(message.From.Id))
                    {
                        await Bot.Send(GetLocaleString("DevOnly"), message.Chat.Id);
                        return;
                    }
                }
                // Check group settings if in a group
                else
                {
                    using var scope = Program.host.Services.CreateScope();
                    var redis = ServiceHelper.GetService<IRedisHelper>(scope);
                    var extralistSetting = await redis.HashGetAsync($"chat:{message.Chat.Id}:settings", "Extralist");

                    // If setting is "no" (admin only), check if user is admin
                    if (extralistSetting != "no" && (!UpdateHelper.IsGroupAdmin(update) ||
                                                     !UpdateHelper.Devs.Contains(message.From.Id)))
                    {
                        await Bot.Send(GetLocaleString("GroupAdminOnly"), message.Chat.Id);
                        return;
                    }
                }

                var extras = await ExtraHelper.GetAllExtras();
                if (extras.Count == 0)
                {
                    await Bot.Send(GetLocaleString("extralistNoExtras"), message.Chat.Id);
                    return;
                }

                var listText = GetLocaleString("extralistTitle", extras.Count.ToString());
                foreach (var extra in extras.OrderBy(x => x.Key))
                {
                    listText += $"#{extra.Key}\n";
                }

                await Bot.Send(listText, message.Chat.Id, parseMode: ParseMode.Html);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to list extras");
                await Bot.Send(GetLocaleString("extraError", ex.Message), message.Chat.Id);
            }
        }

        [Attributes.Command(Trigger = "extra", DevOnly = true)]
        public static async Task Extra(Update update, string[] args)
        {
            var message = update.Message!;

            // args[0] = "extra", args[1] = "#hashtag message content"
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                await Bot.Send(GetLocaleString("extraUsage"), message.Chat.Id);
                return;
            }

            var fullText = args[1].Trim();
            var spaceIndex = fullText.IndexOf(' ');

            if (spaceIndex <= 0)
            {
                await Bot.Send(GetLocaleString("extraHashtagAndContentRequired"), message.Chat.Id);
                return;
            }

            var hashtag = fullText[..spaceIndex].Trim();
            if (!hashtag.StartsWith('#'))
            {
                await Bot.Send(GetLocaleString("extraHashtagMustStartWithHash"), message.Chat.Id);
                return;
            }

            var content = fullText[(spaceIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                await Bot.Send(GetLocaleString("extraContentEmpty"), message.Chat.Id);
                return;
            }

            // Parse buttons from content
            var (finalText, replyMarkup) = MessageHelper.ParseButtonsFromPlainText(content);

            // Serialize the content with buttons (if any)
            var serializedContent = finalText;
            if (replyMarkup != null)
            {
                // Store buttons in a simple format that can be reconstructed
                var buttonRows = replyMarkup.InlineKeyboard.Select(row =>
                    "{" + string.Join(", ", row.Select(btn => $"[{btn.Text}]({btn.Url})")) + "}"
                );
                serializedContent = finalText + " " + string.Join(" ", buttonRows);
            }

            try
            {
                var saved = await ExtraHelper.SaveExtra(hashtag, serializedContent);
                if (saved)
                {
                    await Bot.Send(GetLocaleString("extraSaved", hashtag), message.Chat.Id);
                }
                else
                {
                    await Bot.Send(GetLocaleString("extraSaveFailed"), message.Chat.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save extra");
                await Bot.Send(GetLocaleString("extraError", ex.Message), message.Chat.Id);
            }
        }

        [Attributes.Command(Trigger = "extradel", DevOnly = true)]
        public static async Task ExtraDel(Update update, string[] args)
        {
            var message = update.Message!;

            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                await Bot.Send(GetLocaleString("extradelUsage"), message.Chat.Id);
                return;
            }

            var hashtag = args[1].Trim();
            if (!hashtag.StartsWith('#'))
            {
                await Bot.Send(GetLocaleString("extradelHashtagMustStartWithHash"), message.Chat.Id);
                return;
            }

            try
            {
                var exists = await ExtraHelper.ExtraExists(hashtag);
                if (!exists)
                {
                    await Bot.Send(GetLocaleString("extradelNotFound", hashtag), message.Chat.Id);
                    return;
                }

                var deleted = await ExtraHelper.DeleteExtra(hashtag);
                if (deleted)
                {
                    await Bot.Send(GetLocaleString("extradelSuccess", hashtag), message.Chat.Id);
                }
                else
                {
                    await Bot.Send(GetLocaleString("extradelFailed"), message.Chat.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete extra");
                await Bot.Send(GetLocaleString("extraError", ex.Message), message.Chat.Id);
            }
        }

        [Attributes.Command(Trigger = "setwelcome", GroupAdminOnly = true, InGroupOnly = true)]
        public static async Task SetWelcome(Update update, string[] args)
        {
            var message = update.Message!;

            try
            {
                string? welcomeText;
                string mediaType = "";
                string mediaId = "";

                // Check if replying to a message with media
                if (message.ReplyToMessage != null)
                {
                    var repliedMsg = message.ReplyToMessage;

                    // Check for GIF (Animation)
                    if (repliedMsg.Animation != null)
                    {
                        mediaType = "animation";
                        mediaId = repliedMsg.Animation.FileId;
                    }
                    // Check for Photo
                    else if (repliedMsg.Photo != null && repliedMsg.Photo.Length > 0)
                    {
                        mediaType = "photo";
                        mediaId = repliedMsg.Photo[^1].FileId; // Get largest photo
                    }
                    // Check for Video
                    else if (repliedMsg.Video != null)
                    {
                        mediaType = "video";
                        mediaId = repliedMsg.Video.FileId;
                    }

                    // Get text from replied message or command message
                    welcomeText = !string.IsNullOrWhiteSpace(repliedMsg.Text)
                        ? repliedMsg.Text
                        : !string.IsNullOrWhiteSpace(repliedMsg.Caption)
                            ? repliedMsg.Caption
                            : args.Length > 1
                                ? args[1]
                                : null;
                }
                else
                {
                    // No reply, just get text from command
                    welcomeText = args.Length > 1 ? args[1] : null;
                }

                // Validate we have at least text or media
                if (string.IsNullOrWhiteSpace(welcomeText) && string.IsNullOrWhiteSpace(mediaId))
                {
                    await Bot.Send(GetLocaleString("setwelcomeNoText"), message.Chat.Id);
                    return;
                }

                // Save to Redis
                using var scope = Program.host.Services.CreateScope();
                var redis = ServiceHelper.GetService<IRedisHelper>(scope);

                await redis.HashSetAsync($"chat:{message.Chat.Id}:welcome", "text", welcomeText ?? "");

                if (!string.IsNullOrEmpty(mediaType) && !string.IsNullOrEmpty(mediaId))
                {
                    await redis.HashSetAsync($"chat:{message.Chat.Id}:welcome", "mediaType", mediaType);
                    await redis.HashSetAsync($"chat:{message.Chat.Id}:welcome", "mediaId", mediaId);

                    var mediaName = mediaType switch
                    {
                        "animation" => "GIF",
                        "photo" => "fotoğraf",
                        "video" => "video",
                        _ => "medya"
                    };

                    await Bot.Send(GetLocaleString("setwelcomeSuccessWithMedia", mediaName), message.Chat.Id);
                }
                else
                {
                    // Clear media if only text is set
                    await redis.HashDeleteAsync($"chat:{message.Chat.Id}:welcome", "mediaType");
                    await redis.HashDeleteAsync($"chat:{message.Chat.Id}:welcome", "mediaId");

                    await Bot.Send(GetLocaleString("setwelcomeSuccess"), message.Chat.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set welcome message");
                await Bot.Send(GetLocaleString("extraError", ex.Message), message.Chat.Id);
            }
        }

        [Attributes.Command(Trigger = "welcome", GroupAdminOnly = true, InGroupOnly = true)]
        public static async Task Welcome(Update update, string[] args)
        {
            var message = update.Message!;

            try
            {
                using var scope = Program.host.Services.CreateScope();
                var redis = ServiceHelper.GetService<IRedisHelper>(scope);

                // Get welcome message
                var welcomeText = await redis.HashGetAsync($"chat:{message.Chat.Id}:welcome", "text");
                if (string.IsNullOrWhiteSpace(welcomeText))
                {
                    await Bot.Send(GetLocaleString("welcomeNotSet"), message.Chat.Id);
                    return;
                }

                var mediaType = await redis.HashGetAsync($"chat:{message.Chat.Id}:welcome", "mediaType");
                var mediaId = await redis.HashGetAsync($"chat:{message.Chat.Id}:welcome", "mediaId");

                // Format message with user's own info
                var formattedText = MessageHelper.FormatWelcomeMessage(welcomeText, message.From, message.Chat);

                // Parse buttons
                var (finalText, replyMarkup) = MessageHelper.ParseButtonsFromPlainText(formattedText);

                // Send preview message (no auto-delete, just preview)
                if (!string.IsNullOrEmpty(mediaType) && !string.IsNullOrEmpty(mediaId))
                {
                    _ = mediaType switch
                    {
                        "animation" => await Bot.Api.SendAnimation(
                            chatId: message.Chat.Id,
                            animation: new InputFileId(mediaId),
                            caption: finalText,
                            parseMode: ParseMode.Html,
                            replyMarkup: replyMarkup,
                            messageThreadId: message.MessageThreadId
                        ),
                        "photo" => await Bot.Api.SendPhoto(
                            chatId: message.Chat.Id,
                            photo: new InputFileId(mediaId),
                            caption: finalText,
                            parseMode: ParseMode.Html,
                            replyMarkup: replyMarkup,
                            messageThreadId: message.MessageThreadId
                        ),
                        "video" => await Bot.Api.SendVideo(
                            chatId: message.Chat.Id,
                            video: new InputFileId(mediaId),
                            caption: finalText,
                            parseMode: ParseMode.Html,
                            replyMarkup: replyMarkup,
                            messageThreadId: message.MessageThreadId
                        ),
                        _ => await Bot.Api.SendMessage(
                            chatId: message.Chat.Id,
                            text: finalText,
                            parseMode: ParseMode.Html,
                            replyMarkup: replyMarkup,
                            messageThreadId: message.MessageThreadId
                        )
                    };
                }
                else
                {
                    // Send text only preview
                    await Bot.Api.SendMessage(
                        chatId: message.Chat.Id,
                        text: finalText,
                        parseMode: ParseMode.Html,
                        replyMarkup: replyMarkup,
                        messageThreadId: message.MessageThreadId
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send welcome preview");
                await Bot.Send(GetLocaleString("extraError", ex.Message), message.Chat.Id);
            }
        }

        [Attributes.Command(Trigger = "users", DevOnly = true)]
        public static async Task Users(Update update, string[] args)
        {
            var message = update.Message!;

            try
            {
                using var scope = Program.host.Services.CreateScope();
                var redis = ServiceHelper.GetService<IRedisHelper>(scope);

                var groups = await RegistrationHelper.GetAllGroups(redis);

                if (groups.Count == 0)
                {
                    await Bot.Send(GetLocaleString("noGroups"), message.Chat.Id);
                    return;
                }

                // Send group list with pagination (page 0)
                await SendGroupList(message.Chat.Id, redis, groups, 0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to list groups");
                await Bot.Send(GetLocaleString("extraError", ex.Message), message.Chat.Id);
            }
        }

        private static async Task SendGroupList(long chatId, IRedisHelper redis, List<string> groups, int page)
        {
            const int perPage = 4;
            var start = page * perPage;
            var end = Math.Min(start + perPage, groups.Count);
            var currentGroups = groups.GetRange(start, end - start);

            var keyboard = new List<List<InlineKeyboardButton>>();

            foreach (var groupId in currentGroups)
            {
                var title = await RegistrationHelper.GetGroupTitle(redis, long.Parse(groupId));
                keyboard.Add([InlineKeyboardButton.WithCallbackData(title, $"grp_sel|{groupId}")]);
            }

            // Navigation buttons
            var navRow = new List<InlineKeyboardButton>();
            if (page > 0)
                navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"grp_page|{page - 1}"));
            if (end < groups.Count)
                navRow.Add(InlineKeyboardButton.WithCallbackData("➡️", $"grp_page|{page + 1}"));

            if (navRow.Count > 0)
                keyboard.Add(navRow);

            var replyMarkup = new InlineKeyboardMarkup(keyboard);
            await Bot.Send(GetLocaleString("selectGroup"), chatId, customMenu: replyMarkup);
        }

        public static async Task SendGroupListEdit(long chatId, int messageId, IRedisHelper redis, List<string> groups, int page)
        {
            const int perPage = 4;
            var start = page * perPage;
            var end = Math.Min(start + perPage, groups.Count);
            var currentGroups = groups.GetRange(start, end - start);

            var keyboard = new List<List<InlineKeyboardButton>>();

            foreach (var groupId in currentGroups)
            {
                var title = await RegistrationHelper.GetGroupTitle(redis, long.Parse(groupId));
                keyboard.Add([InlineKeyboardButton.WithCallbackData(title, $"grp_sel|{groupId}")]);
            }

            // Navigation buttons
            var navRow = new List<InlineKeyboardButton>();
            if (page > 0)
                navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"grp_page|{page - 1}"));
            if (end < groups.Count)
                navRow.Add(InlineKeyboardButton.WithCallbackData("➡️", $"grp_page|{page + 1}"));

            if (navRow.Count > 0)
                keyboard.Add(navRow);

            var replyMarkup = new InlineKeyboardMarkup(keyboard);
            await Bot.Api.EditMessageText(chatId, messageId, GetLocaleString("selectGroup"), replyMarkup: replyMarkup);
        }
    }
}
