using Serilog;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using SiberVatan.Interfaces;
using SiberVatan.Models;
using SiberVatan;

namespace SiberVatan.Helpers;

/// <summary>
/// Helper class for Telegram content filtering and anti-spam operations
/// </summary>
public static class TelegramContentFilterHelper
{
    // ==================== FLOOD DETECTION ====================
    /// <summary>
    /// Check if a message violates flood (spam) rules
    /// </summary>
    public static async Task<FilterResult> CheckFloodAsync(Message message, IRedisHelper redis)
    {
        try
        {
            var chatId = message.Chat.Id;
            var userId = message.From.Id;

            // Ensure default settings exist
            await GroupSettingsHelper.InitializeDefaultSettingsAsync(chatId, redis);

            // Check if flood protection is disabled
            var floodDisabled = await redis.HashGetAsync($"chat:{chatId}:settings", "Flood");
            if (floodDisabled == "yes")
            {
                return FilterResult.NoViolation();
            }

            // Check if user is in watch list (ignored)
            var isIgnored = await redis.SetContainsAsync($"chat:{chatId}:watch", userId.ToString());
            if (isIgnored)
            {
                return FilterResult.NoViolation();
            }

            // Get flood settings
            var maxFloodStr = await redis.HashGetAsync($"chat:{chatId}:flood", "MaxFlood");
            var actionFlood = await redis.HashGetAsync($"chat:{chatId}:flood", "ActionFlood");
            var maxFlood = string.IsNullOrEmpty(maxFloodStr) ? 8 : int.Parse(maxFloodStr);
            var action = string.IsNullOrEmpty(actionFlood) ? "kick" : actionFlood;

            // Get current message count
            var spamKey = $"spam:{chatId}:{userId}";
            var currentCountStr = await redis.StringGetAsync(spamKey);
            var currentCount = string.IsNullOrEmpty(currentCountStr) ? 0 : int.Parse(currentCountStr);
            currentCount++;

            // Set counter with 6 second TTL
            await redis.StringSetAsync(spamKey, currentCount.ToString(), TimeSpan.FromSeconds(6));

            // Check if limit exceeded
            if (currentCount <= maxFlood) return FilterResult.NoViolation();

            var result = FilterResult.Violation("flood", action, $"Message flood detected ({currentCount}/{maxFlood})");
            result.Context["count"] = currentCount;
            result.Context["max"] = maxFlood;

            var name = message.From.FirstName;
            var reason = Commands.GetLocaleString("reasonFlood", currentCount, maxFlood);

            // Execute action
            if (action == "warn")
            {
                // Get max warnings
                var maxWarnsStr = await redis.HashGetAsync($"chat:{chatId}:warnsettings", "mediamax");
                var maxWarns = int.TryParse(maxWarnsStr, out var mw) ? mw : 5;

                var warnCount = await redis.HashIncrementAsync($"chat:{chatId}:warns", userId.ToString());
                if (warnCount >= maxWarns)
                {
                    var fallbackAction = await redis.HashGetAsync($"chat:{chatId}:warnsettings", "action") ?? "kick";
                    await ExecuteActionAsync(chatId, userId, fallbackAction, redis);
                    await redis.HashDeleteAsync($"chat:{chatId}:warns", userId.ToString());

                    var prefix = Commands.GetLocaleString("warnLimitReached", warnCount, maxWarns);
                    var msg = fallbackAction switch
                    {
                        "kick" => prefix + Commands.GetLocaleString("warnKick", name, reason),
                        "ban" => prefix + Commands.GetLocaleString("warnBan", name, reason),
                        "tempban" => prefix + Commands.GetLocaleString("warnTempBan", name, reason),
                        "mute" => prefix + Commands.GetLocaleString("warnMute", name, reason),
                        _ => string.Empty
                    };

                    if (!string.IsNullOrEmpty(msg))
                    {
                        InlineKeyboardButton[]? buttons = fallbackAction switch
                        {
                            "ban" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                    $"mod|unban|{userId}")
                            ],
                            "tempban" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                    $"mod|unban|{userId}")
                            ],
                            "mute" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("unmuteButton"),
                                    $"mod|unmute|{userId}")
                            ],
                            _ => null
                        };

                        if (buttons != null)
                            await Bot.Send(msg, chatId, customMenu: new InlineKeyboardMarkup(buttons));
                        else
                            await Bot.Send(msg, chatId);
                    }
                }
                else
                {
                    var msg = Commands.GetLocaleString("warnUser", name, warnCount, maxWarns, reason);
                    var button = InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeWarnButton"),
                        $"mod|remwarn|{userId}");
                    await Bot.Send(msg, chatId, customMenu: new InlineKeyboardMarkup(button));
                }

                result.ActionExecuted = true;
            }
            else
            {
                var success = await ExecuteActionAsync(chatId, userId, action, redis);
                result.ActionExecuted = success;
                if (!success)
                {
                    result.Message += " | Action failed";
                }
                else
                {
                    var msg = action switch
                    {
                        "kick" => Commands.GetLocaleString("warnKick", name, reason),
                        "ban" => Commands.GetLocaleString("warnBan", name, reason),
                        "tempban" => Commands.GetLocaleString("warnTempBan", name, reason),
                        "mute" => Commands.GetLocaleString("warnMute", name, reason),
                        _ => string.Empty
                    };

                    if (!string.IsNullOrEmpty(msg))
                    {
                        InlineKeyboardButton[]? buttons = action switch
                        {
                            "ban" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                    $"mod|unban|{userId}")
                            ],
                            "tempban" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                    $"mod|unban|{userId}")
                            ],
                            "mute" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("unmuteButton"),
                                    $"mod|unmute|{userId}")
                            ],
                            _ => null
                        };

                        if (buttons != null)
                            await Bot.Send(msg, chatId, customMenu: new InlineKeyboardMarkup(buttons));
                        else
                            await Bot.Send(msg, chatId);
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking flood for user {FromId} in chat {ChatId}", message.From.Id, message.Chat.Id);
            return FilterResult.NoViolation();
        }
    }

    // ==================== FORWARD VALIDATION ====================
    /// <summary>
    /// Check if message is a forward and if forwarding is allowed
    /// </summary>
    public static async Task<FilterResult> CheckForwardAsync(Message message, IRedisHelper redis)
    {
        try
        {
            var chatId = message.Chat.Id;
            var userId = message.From.Id;

            // Ensure default settings exist
            await GroupSettingsHelper.InitializeDefaultSettingsAsync(chatId, redis);

            // Check if message is a forward
            if (message.ForwardFrom == null && message.ForwardFromChat == null && message.ForwardDate == null &&
                message.ForwardOrigin == null)
            {
                return FilterResult.NoViolation();
            }

            // Check if user is ignored
            var isIgnored = await redis.SetContainsAsync($"chat:{chatId}:watch", userId.ToString());
            if (isIgnored) return FilterResult.NoViolation();

            // Get settings
            var allowed = await redis.HashGetAsync($"chat:{chatId}:settings", "AllowChannelForward");
            if (allowed == "yes") return FilterResult.NoViolation();

            // Configured Action
            var action = await redis.HashGetAsync($"chat:{chatId}:char", "Forward");
            action = string.IsNullOrEmpty(action) ? "warn" : action;

            // Delete message
            try
            {
                await Bot.Api.DeleteMessage(chatId, message.MessageId);
            }
            catch
            {
                /* ignored */
            }

            var name = message.From.FirstName;
            var reason = Commands.GetLocaleString("reasonForward");

            var result = FilterResult.Violation("forward", action, "Forwarding not allowed");

            // Execute action
            if (action == "warn")
            {
                // Get max warnings
                var maxWarnsStr = await redis.HashGetAsync($"chat:{chatId}:warnsettings", "mediamax");
                var maxWarns = int.TryParse(maxWarnsStr, out var mw) ? mw : 5;

                var warnCount = await redis.HashIncrementAsync($"chat:{chatId}:warns", userId.ToString());
                if (warnCount >= maxWarns)
                {
                    var fallbackAction = await redis.HashGetAsync($"chat:{chatId}:warnsettings", "action") ?? "kick";
                    await ExecuteActionAsync(chatId, userId, fallbackAction, redis);
                    await redis.HashDeleteAsync($"chat:{chatId}:warns", userId.ToString());

                    var prefix = Commands.GetLocaleString("warnLimitReached", warnCount, maxWarns);
                    var msg = fallbackAction switch
                    {
                        "kick" => prefix + Commands.GetLocaleString("warnKick", name, reason),
                        "ban" => prefix + Commands.GetLocaleString("warnBan", name, reason),
                        "tempban" => prefix + Commands.GetLocaleString("warnTempBan", name, reason),
                        "mute" => prefix + Commands.GetLocaleString("warnMute", name, reason),
                        _ => string.Empty
                    };

                    if (!string.IsNullOrEmpty(msg))
                    {
                        InlineKeyboardButton[]? buttons = fallbackAction switch
                        {
                            "ban" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                    $"mod|unban|{userId}")
                            ],
                            "tempban" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                    $"mod|unban|{userId}")
                            ],
                            "mute" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("unmuteButton"),
                                    $"mod|unmute|{userId}")
                            ],
                            _ => null
                        };

                        if (buttons != null)
                            await Bot.Send(msg, chatId, customMenu: new InlineKeyboardMarkup(buttons));
                        else
                            await Bot.Send(msg, chatId);
                    }
                }
                else
                {
                    var msg = Commands.GetLocaleString("warnUser", name, warnCount, maxWarns, reason);
                    var button = InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeWarnButton"),
                        $"mod|remwarn|{userId}");
                    await Bot.Send(msg, chatId, customMenu: new InlineKeyboardMarkup(button));
                }

                result.ActionExecuted = true;
            }
            else
            {
                var success = await ExecuteActionAsync(chatId, userId, action, redis);
                result.ActionExecuted = success;
                if (success)
                {
                    var msg = action switch
                    {
                        "kick" => Commands.GetLocaleString("warnKick", name, reason),
                        "ban" => Commands.GetLocaleString("warnBan", name, reason),
                        "tempban" => Commands.GetLocaleString("warnTempBan", name, reason),
                        "mute" => Commands.GetLocaleString("warnMute", name, reason),
                        _ => string.Empty
                    };

                    if (!string.IsNullOrEmpty(msg))
                    {
                        InlineKeyboardButton[]? buttons = action switch
                        {
                            "ban" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                    $"mod|unban|{userId}")
                            ],
                            "tempban" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                    $"mod|unban|{userId}")
                            ],
                            "mute" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("unmuteButton"),
                                    $"mod|unmute|{userId}")
                            ],
                            _ => null
                        };

                        if (buttons != null)
                            await Bot.Send(msg, chatId, customMenu: new InlineKeyboardMarkup(buttons));
                        else
                            await Bot.Send(msg, chatId);
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error checking forward for user {message.From.Id}");
            return FilterResult.NoViolation();
        }
    }

    // ==================== LENGTH VALIDATION ====================
    /// <summary>
    /// Check if message text violates length rules
    /// </summary>
    public static async Task<FilterResult> CheckTextLengthAsync(Message message, IRedisHelper redis)
    {
        try
        {
            var chatId = message.Chat.Id;
            var userId = message.From.Id;

            // Ensure default settings exist
            await GroupSettingsHelper.InitializeDefaultSettingsAsync(chatId, redis);
            if (message.Type != MessageType.Text || string.IsNullOrEmpty(message.Text))
            {
                return FilterResult.NoViolation();
            }

            // Check if user is ignored
            var isIgnored = await redis.SetContainsAsync($"chat:{chatId}:watch", userId.ToString());
            if (isIgnored)
            {
                return FilterResult.NoViolation();
            }

            // Get settings
            var enabled = await redis.HashGetAsync($"chat:{chatId}:antitextlength", "enabled");
            if (enabled == "yes")
            {
                return FilterResult.NoViolation();
            }

            var maxLengthStr = await redis.HashGetAsync($"chat:{chatId}:antitextlength", "maxlength");
            var maxLinesStr = await redis.HashGetAsync($"chat:{chatId}:antitextlength", "maxlines");
            var action = await redis.HashGetAsync($"chat:{chatId}:antitextlength", "action");

            var maxLength = string.IsNullOrEmpty(maxLengthStr) ? 4000 : int.Parse(maxLengthStr);
            var maxLines = string.IsNullOrEmpty(maxLinesStr) ? 50 : int.Parse(maxLinesStr);
            action = string.IsNullOrEmpty(action) ? "kick" : action;

            var text = message.Text;
            var lines = text.Split('\n').Length;

            if (text.Length >= maxLength || lines >= maxLines)
            {
                // Try to delete the message
                try
                {
                    await Bot.Api.DeleteMessage(chatId, message.MessageId);
                }
                catch
                {
                    /* Ignore if can't delete */
                }

                var result = FilterResult.Violation("textlength", action,
                    $"Text too long: {text.Length}/{maxLength} chars, {lines}/{maxLines} lines");
                result.Context["length"] = text.Length;
                result.Context["maxLength"] = maxLength;
                result.Context["lines"] = lines;
                result.Context["maxLines"] = maxLines;

                // Get max warnings
                var maxWarnsStr = await redis.HashGetAsync($"chat:{chatId}:warnsettings", "mediamax");
                var maxWarns = int.TryParse(maxWarnsStr, out var mw) ? mw : 5;
                var name = message.From.FirstName;

                var reason = Commands.GetLocaleString("reasonTextTooLong", maxLength);

                if (action == "warn")
                {
                    var warnCount = await redis.HashIncrementAsync($"chat:{chatId}:warns", userId.ToString());
                    if (warnCount >= maxWarns)
                    {
                        var fallbackAction =
                            await redis.HashGetAsync($"chat:{chatId}:warnsettings", "action") ?? "kick";
                        await ExecuteActionAsync(chatId, userId, fallbackAction, redis);
                        await redis.HashDeleteAsync($"chat:{chatId}:warns", userId.ToString());

                        var prefix = Commands.GetLocaleString("warnLimitReached", warnCount, maxWarns);
                        var msg = fallbackAction switch
                        {
                            "kick" => prefix + Commands.GetLocaleString("warnKick", name, reason),
                            "ban" => prefix + Commands.GetLocaleString("warnBan", name, reason),
                            "tempban" => prefix + Commands.GetLocaleString("warnTempBan", name, reason),
                            "mute" => prefix + Commands.GetLocaleString("warnMute", name, reason),
                            _ => string.Empty
                        };

                        if (!string.IsNullOrEmpty(msg))
                        {
                            InlineKeyboardButton[]? buttons = fallbackAction switch
                            {
                                "ban" =>
                                [
                                    InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                        $"mod|unban|{userId}")
                                ],
                                "mute" =>
                                [
                                    InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("unmuteButton"),
                                        $"mod|unmute|{userId}")
                                ],
                                _ => null
                            };

                            if (buttons != null)
                                await Bot.Send(msg, chatId, customMenu: new InlineKeyboardMarkup(buttons));
                            else
                                await Bot.Send(msg, chatId);
                        }
                    }
                    else
                    {
                        var msg = Commands.GetLocaleString("warnUser", name, warnCount, maxWarns, reason);
                        var button = InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeWarnButton"),
                            $"mod|remwarn|{userId}");
                        await Bot.Send(msg, chatId, customMenu: new InlineKeyboardMarkup(button));
                    }
                }
                else
                {
                    var success = await ExecuteActionAsync(chatId, userId, action, redis);
                    result.ActionExecuted = success;

                    var msg = action switch
                    {
                        "kick" => Commands.GetLocaleString("warnKick", name, reason),
                        "ban" => Commands.GetLocaleString("warnBan", name, reason),
                        "tempban" => Commands.GetLocaleString("warnTempBan", name, reason),
                        "mute" => Commands.GetLocaleString("warnMute", name, reason),
                        _ => string.Empty
                    };

                    if (!string.IsNullOrEmpty(msg))
                    {
                        InlineKeyboardButton[]? buttons = action switch
                        {
                            "ban" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                    $"mod|unban|{userId}")
                            ],
                            "tempban" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                    $"mod|unban|{userId}")
                            ],
                            "mute" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("unmuteButton"),
                                    $"mod|unmute|{userId}")
                            ],
                            _ => null
                        };

                        if (buttons != null)
                            await Bot.Send(msg, chatId, customMenu: new InlineKeyboardMarkup(buttons));
                        else
                            await Bot.Send(msg, chatId);
                    }
                }

                return result;
            }

            return FilterResult.NoViolation();
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error checking text length for user {message.From.Id}");
            return FilterResult.NoViolation();
        }
    }

    /// <summary>
    /// Check if message media type is blocked
    /// </summary>
    public static async Task CheckMediaTypeAsync(Message message, IRedisHelper redis)
    {
        try
        {
            var chatId = message.Chat.Id;
            var userId = message.From.Id;

            // Ensure default settings exist
            await GroupSettingsHelper.InitializeDefaultSettingsAsync(chatId, redis);

            // Check if user is ignored
            var isIgnored = await redis.SetContainsAsync($"chat:{chatId}:watch", userId.ToString());
            if (isIgnored) return;

            // Get media type
            var mediaType = GetMediaType(message);
            if (string.IsNullOrEmpty(mediaType)) return;

            // Check if this media type is blocked
            var status = await redis.HashGetAsync($"chat:{chatId}:media", mediaType);
            if (status != "blocked") return;

            // Delete the message
            try
            {
                await Bot.Api.DeleteMessage(chatId, message.MessageId);
            }
            catch
            {
                return; // If can't delete, probably no rights, so don't punish? Or continue? Continuing.
            }

            // Get action and max warnings
            var action = await redis.HashGetAsync($"chat:{chatId}:media", "action") ?? "warn";
            var maxWarns = int.TryParse(await redis.HashGetAsync($"chat:{chatId}:warnsettings", "mediamax"), out var mw)
                ? mw
                : 5;
            var name = message.From.FirstName;

            var reason = Commands.GetLocaleString("reasonMediaBlocked", mediaType);

            if (action == "warn")
            {
                var warnCount = await redis.HashIncrementAsync($"chat:{chatId}:warns", userId.ToString());
                if (warnCount >= maxWarns)
                {
                    // Max warnings reached, execute punitive action
                    var fallbackAction = await redis.HashGetAsync($"chat:{chatId}:warnsettings", "action") ?? "kick";
                    await ExecuteActionAsync(chatId, userId, fallbackAction, redis);
                    await redis.HashDeleteAsync($"chat:{chatId}:warns", userId.ToString());

                    // Send notification for the fallback action
                    var prefix = Commands.GetLocaleString("warnLimitReached", warnCount, maxWarns);
                    var msg = fallbackAction switch
                    {
                        "kick" => prefix + Commands.GetLocaleString("warnKick", name, reason),
                        "ban" => prefix + Commands.GetLocaleString("warnBan", name, reason),
                        "tempban" => prefix + Commands.GetLocaleString("warnTempBan", name, reason),
                        "mute" => prefix + Commands.GetLocaleString("warnMute", name, reason),
                        _ => string.Empty
                    };

                    if (!string.IsNullOrEmpty(msg))
                    {
                        InlineKeyboardButton[]? buttons = fallbackAction switch
                        {
                            "ban" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                    $"mod|unban|{userId}")
                            ],
                            "tempban" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                    $"mod|unban|{userId}")
                            ],
                            "mute" =>
                            [
                                InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("unmuteButton"),
                                    $"mod|unmute|{userId}")
                            ],
                            _ => null
                        };

                        if (buttons != null)
                            await Bot.Send(msg, chatId, customMenu: new InlineKeyboardMarkup(buttons));
                        else
                            await Bot.Send(msg, chatId);
                    }
                }
                else
                {
                    // Send warning
                    var msg = Commands.GetLocaleString("warnUser", name, warnCount, maxWarns, reason);
                    var button = InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeWarnButton"),
                        $"mod|remwarn|{userId}");
                    await Bot.Send(msg, chatId, customMenu: new InlineKeyboardMarkup(button));
                }
            }
            else
            {
                // Immediate action
                await ExecuteActionAsync(chatId, userId, action, redis);

                var msg = action switch
                {
                    "kick" => Commands.GetLocaleString("warnKick", name, reason),
                    "ban" => Commands.GetLocaleString("warnBan", name, reason),
                    "mute" => Commands.GetLocaleString("warnMute", name, reason),
                    _ => string.Empty
                };

                if (!string.IsNullOrEmpty(msg))
                {
                    InlineKeyboardButton[]? buttons = action switch
                    {
                        "ban" =>
                        [
                            InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("removeBanButton"),
                                $"mod|unban|{userId}")
                        ],
                        "mute" =>
                        [
                            InlineKeyboardButton.WithCallbackData(Commands.GetLocaleString("unmuteButton"),
                                $"mod|unmute|{userId}")
                        ],
                        _ => null
                    };

                    if (buttons != null)
                        await Bot.Send(msg, chatId, customMenu: new InlineKeyboardMarkup(buttons));
                    else
                        await Bot.Send(msg, chatId);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking media type for user {FromId}", message.From.Id);
        }
    }

    // ==================== HELPER METHODS ====================
    /// <summary>
    /// Execute moderation action (kick/ban/tempban)
    /// </summary>
    private static async Task<bool> ExecuteActionAsync(long chatId, long userId, string action, IRedisHelper redis)
    {
        return action.ToLower() switch
        {
            "kick" => await TelegramModerationHelper.KickAsync(chatId, userId, redis),
            "ban" => await TelegramModerationHelper.BanAsync(chatId, userId, redis),
            "tempban" => await ExecuteTempBanAsync(chatId, userId, redis),
            "mute" => await TelegramModerationHelper.MuteAsync(chatId, userId, null, redis),
            _ => false
        };
    }

    /// <summary>
    /// Execute temporary ban with group-specific duration
    /// </summary>
    private static async Task<bool> ExecuteTempBanAsync(long chatId, long userId, IRedisHelper redis)
    {
        // Get group temp ban time (in minutes, default 30)
        var tempBanTimeStr = await redis.HashGetAsync($"chat:{chatId}:settings", "tempbantime");
        var tempBanMinutes = string.IsNullOrEmpty(tempBanTimeStr) ? 30 : int.Parse(tempBanTimeStr);
        var untilDate = DateTime.UtcNow.AddMinutes(tempBanMinutes);

        return await TelegramModerationHelper.TempBanAsync(chatId, userId, untilDate, redis);
    }

    /// <summary>
    /// Get media type from message
    /// </summary>
    private static string GetMediaType(Message message)
    {
        // Custom checks
        if (message.Document?.FileName?.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) == true) return "apk";

        var hasUrl =
            (message.Entities?.Any(e => e.Type == MessageEntityType.Url || e.Type == MessageEntityType.TextLink) ??
             false) ||
            (message.CaptionEntities?.Any(e =>
                e.Type == MessageEntityType.Url || e.Type == MessageEntityType.TextLink) ?? false);

        if (hasUrl) return "url";
        return message.Type switch
        {
            MessageType.Photo => "photo",
            MessageType.Video => "video",
            MessageType.Audio => "audio",
            MessageType.Voice => "voice",
            MessageType.Document => "document",
            MessageType.Sticker => "sticker",
            MessageType.VideoNote => "videonote",
            MessageType.Contact => "contact",
            MessageType.Location => "location",
            MessageType.Venue => "venue",
            MessageType.Poll => "poll",
            MessageType.Game => "game",
            MessageType.Dice => "dice",
            MessageType.Animation => "animation",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Check if user is in watch/ignore list
    /// </summary>
    public static Task<bool> IsUserIgnoredAsync(long chatId, long userId, IRedisHelper redis)
    {
        return redis.SetContainsAsync($"chat:{chatId}:watch", userId.ToString());
    }
}
