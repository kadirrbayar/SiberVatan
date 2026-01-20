using Serilog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using SiberVatan.Interfaces;

namespace SiberVatan.Helpers;

/// <summary>
/// Helper class for Telegram group moderation operations with Redis integration
/// </summary>
public static class TelegramModerationHelper
{
    // ==================== MUTE OPERATIONS ====================
    /// <summary>
    /// Mute a user in a chat
    /// </summary>
    /// <param name="chatId">Chat ID</param>
    /// <param name="userId">User ID to mute</param>
    /// <param name="untilDate">Optional expiration date for the mute</param>
    /// <param name="redis">Optional IRedisHelper instance for tracking</param>
    /// <returns>Tuple with success status and error message if any</returns>
    public static async Task<bool> MuteAsync(long chatId, long userId, DateTime? untilDate = null, IRedisHelper? redis = null)
    {
        try
        {
            var permissions = new ChatPermissions
            {
                CanSendMessages = false,
                CanSendAudios = false,
                CanSendDocuments = false,
                CanSendPhotos = false,
                CanSendVideos = false,
                CanSendVideoNotes = false,
                CanSendVoiceNotes = false,
                CanSendPolls = false,
                CanSendOtherMessages = false,
                CanAddWebPagePreviews = false,
                CanChangeInfo = false,
                CanInviteUsers = false,
                CanPinMessages = false,
                CanManageTopics = false
            };

            await Bot.Api.RestrictChatMember(chatId, userId, permissions, untilDate: untilDate);
            // Track muted user in Redis
            if (redis != null)
            {
                await redis.SetAddAsync($"chat:{chatId}:muted", userId.ToString());
            }

            Log.Information($"User {userId} muted in chat {chatId}" + (untilDate.HasValue ? $" until {untilDate}" : " permanently"));
            return true;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not enough rights"))
        {
            return false;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("user is an administrator"))
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error muting user {userId} in chat {chatId}");
            return false;
        }
    }

    /// <summary>
    /// Unmute a user in a chat
    /// </summary>
    public static async Task<bool> UnmuteAsync(long chatId,  long userId,  IRedisHelper? redis = null)
    {
        try
        {
            // Get current chat permissions
            var chat = await Bot.Api.GetChat(chatId);
            var defaultPermissions = chat.Permissions ?? new ChatPermissions
            {
                CanSendMessages = true,
                CanSendAudios = true,
                CanSendDocuments = true,
                CanSendPhotos = true,
                CanSendVideos = true,
                CanSendVideoNotes = true,
                CanSendVoiceNotes = true,
                CanSendPolls = true,
                CanSendOtherMessages = true,
                CanAddWebPagePreviews = true,
                CanChangeInfo = true,
                CanInviteUsers = true,
                CanPinMessages = true,
                CanManageTopics = true
            };

            await Bot.Api.RestrictChatMember(chatId, userId, defaultPermissions);

            // Remove from muted list in Redis
            if (redis != null)
            {
                await redis.SetRemoveAsync($"chat:{chatId}:muted", userId.ToString());
            }

            Log.Information($"User {userId} unmuted in chat {chatId}");
            return true;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not enough rights"))
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error unmuting user {userId} in chat {chatId}");
            return false;
        }
    }

    // ==================== BAN OPERATIONS ====================
    /// <summary>
    /// Permanently ban a user from a chat
    /// </summary>
    public static async Task<bool> BanAsync(long chatId, long userId, IRedisHelper? redis = null)
    {
        try
        {
            await Bot.Api.BanChatMember(chatId, userId);
            // Increment ban counter in Redis
            if (redis != null)
            {
                await redis.HashIncrementAsync("bot:general", "ban");
            }

            Log.Information($"User {userId} permanently banned from chat {chatId}");
            return true;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not enough rights"))
        {
            return false;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("user is an administrator"))
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error banning user {userId} from chat {chatId}");
            return false;
        }
    }

    /// <summary>
    /// Kick a user from a chat (ban then immediately unban)
    /// </summary>
    public static async Task<bool> KickAsync(long chatId,  long userId, IRedisHelper? redis = null)
    {
        try
        {
            // Ban the user
            await Bot.Api.BanChatMember(chatId, userId);
            // Wait a moment to ensure the ban is processed
            await Task.Delay(500);
            // Unban to allow them to rejoin
            await Bot.Api.UnbanChatMember(chatId, userId);

            // Increment kick counter in Redis
            if (redis != null)
            {
                await redis.HashIncrementAsync("bot:general", "kick");
            }

            Log.Information($"User {userId} kicked from chat {chatId}");
            return true;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not enough rights"))
        {
            return false;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("user is an administrator"))
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error kicking user {userId} from chat {chatId}");
            return false;
        }
    }

    /// <summary>
    /// Temporarily ban a user from a chat
    /// </summary>
    public static async Task<bool> TempBanAsync(long chatId, long userId, DateTime untilDate, IRedisHelper? redis = null)
    {
        try
        {
            await Bot.Api.BanChatMember(chatId, userId, untilDate);

            // Track in Redis with TTL
            if (redis != null)
            {
                await redis.HashIncrementAsync("bot:general", "ban");
                
                var duration = untilDate - DateTime.UtcNow;
                if (duration.TotalSeconds > 0)
                {
                    await redis.StringSetAsync($"tempban:{chatId}:{userId}", "1", duration);
                }
            }

            Log.Information($"User {userId} temporarily banned from chat {chatId} until {untilDate}");
            return true;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not enough rights"))
        {
            return false;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("user is an administrator"))
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error temp banning user {userId} from chat {chatId}");
            return false;
        }
    }

    /// <summary>
    /// Unban a user from a chat
    /// </summary>
    public static async Task<bool> UnbanAsync(long chatId,  long userId,  IRedisHelper? redis = null)
    {
        try
        {
            await Bot.Api.UnbanChatMember(chatId, userId);
            // Remove from temp ban tracking in Redis
            if (redis != null)
            {
                await redis.KeyDeleteAsync($"tempban:{chatId}:{userId}");
            }

            Log.Information($"User {userId} unbanned from chat {chatId}");
            return true;
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not enough rights"))
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error unbanning user {userId} from chat {chatId}");
            return false;
        }
    }

    // ==================== HELPER METHODS ====================

    /// <summary>
    /// Check if a user is muted in a chat
    /// </summary>
    public static Task<bool> IsMutedAsync(long chatId, long userId, IRedisHelper redis)
    {
        return redis.SetContainsAsync($"chat:{chatId}:muted", userId.ToString());
    }

    /// <summary>
    /// Check if a user is temporarily banned
    /// </summary>
    public static Task<bool> IsTempBannedAsync(long chatId, long userId, IRedisHelper redis)
    {
        return redis.KeyExistsAsync($"tempban:{chatId}:{userId}");
    }

    /// <summary>
    /// Get moderation statistics
    /// </summary>
    public static async Task<(long Bans, long Kicks)> GetStatsAsync(IRedisHelper redis)
    {
        var bansStr = await redis.HashGetAsync("bot:general", "ban");
        var kicksStr = await redis.HashGetAsync("bot:general", "kick");

        var bans = string.IsNullOrEmpty(bansStr) ? 0 : long.Parse(bansStr);
        var kicks = string.IsNullOrEmpty(kicksStr) ? 0 : long.Parse(kicksStr);

        return (bans, kicks);
    }
}
