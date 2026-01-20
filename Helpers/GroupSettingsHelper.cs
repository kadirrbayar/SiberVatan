using Serilog;
using SiberVatan.Interfaces;

namespace SiberVatan.Helpers;

public static class GroupSettingsHelper
{
    public static async Task InitializeDefaultSettingsAsync(long chatId, IRedisHelper redis)
    {
        try
        {   
            await SetIfNotExistsAsync(redis, $"chat:{chatId}:settings", "Welcome", "yes"); 
            await SetIfNotExistsAsync(redis, $"chat:{chatId}:settings", "DeleteLastWelcome", "no");
            await SetIfNotExistsAsync(redis, $"chat:{chatId}:settings", "NewUsersCaptcha", "no");

            await SetIfNotExistsAsync(redis, $"chat:{chatId}:settings", "Extralist", "no");
            await SetIfNotExistsAsync(redis, $"chat:{chatId}:settings", "Extra", "no");

            await SetIfNotExistsAsync(redis, $"chat:{chatId}:settings", "AllowChannelForward", "no");
            await SetIfNotExistsAsync(redis, $"chat:{chatId}:char", "Forward", "warn");
            //await SetIfNotExistsAsync(redis, $"chat:{chatId}:settings", "CASBan", "no"); 

            var mediaTypes = new[]
            {
                "photo", "video", "audio", "voice", "document", "sticker",
                "videonote", "contact", "location", "venue", "poll", "game",
                "apk", "dice", "animation", "url"
            };

            foreach (var mediaType in mediaTypes)
            {
                await SetIfNotExistsAsync(redis, $"chat:{chatId}:media", mediaType, "allowed");
            }

            await SetIfNotExistsAsync(redis, $"chat:{chatId}:media", "action", "warn");
            await SetIfNotExistsAsync(redis, $"chat:{chatId}:antitextlength", "enabled", "no"); 
            await SetIfNotExistsAsync(redis, $"chat:{chatId}:antitextlength", "maxlength", "4000");
            await SetIfNotExistsAsync(redis, $"chat:{chatId}:antitextlength", "maxlines", "50");
            await SetIfNotExistsAsync(redis, $"chat:{chatId}:antitextlength", "action", "warn");

            await SetIfNotExistsAsync(redis, $"chat:{chatId}:warnsettings", "mediamax", "5");
            await SetIfNotExistsAsync(redis, $"chat:{chatId}:warnsettings", "action", "kick");

            await SetIfNotExistsAsync(redis, $"chat:{chatId}:settings", "Flood", "no");
            await SetIfNotExistsAsync(redis, $"chat:{chatId}:flood", "MaxFlood", "5");
            await SetIfNotExistsAsync(redis, $"chat:{chatId}:flood", "ActionFlood", "mute");

            await SetIfNotExistsAsync(redis, $"chat:{chatId}:settings", "ServiceMsg", "no");
            Log.Information("Checked/initialized default settings for chat {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing default settings for chat {ChatId}", chatId);
        }
    }

    private static async Task SetIfNotExistsAsync(IRedisHelper redis, string key, string field, string value)
    {
        var exists = await redis.HashExistsAsync(key, field);
        if (!exists)
        {
            await redis.HashSetAsync(key, field, value);
        }
    }

    public static string GetActionIcon(string? action)
    {
        return (action ?? "").ToLower() switch
        {
            "kick" => "👟",
            "ban" => "🔨",
            "tempban" => "⏰",
            "warn" => "⚠️",
            "mute" => "🔇",
            "allowed" => "✅",
            "blocked" => "🚫",
            _ => "❓"
        };
    }

    public static string GetToggleIcon(bool enabled)
    {
        return enabled ? "✅" : "⛔";
    }

    public static string GetToggleIcon(string value)
    {
        return value == "no" ? "✅" : "⛔"; // "no" means enabled (confusing but matches old code)
    }

    public static string GetNextAction(string? currentAction)
    {
        var actions = new[] { "kick", "ban", "tempban", "warn", "mute" };
        var currentIndex = Array.IndexOf(actions, (currentAction ?? "kick").ToLower());
        var nextIndex = (currentIndex + 1) % actions.Length;
        return actions[nextIndex];
    }

    public static string ToggleMediaStatus(string currentStatus)
    {
        return currentStatus == "blocked" ? "allowed" : "blocked";
    }

    public static string ToggleYesNo(string currentValue)
    {
        return currentValue == "yes" ? "no" : "yes";
    }
}