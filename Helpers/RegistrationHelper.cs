using SiberVatan.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SiberVatan.Helpers
{
    public static class RegistrationHelper
    {
        /// <summary>
        /// Register a user for a specific group
        /// </summary>
        public static async Task<bool> RegisterUser(IRedisHelper redis, long groupId, long userId, string nameSurname, User user, string? attendance = null)
        {
            try
            {
                // Store registration
                await redis.HashSetAsync($"group_registrations:{groupId}", userId.ToString(), nameSurname);
                if (!string.IsNullOrEmpty(attendance))
                {
                    await redis.HashSetAsync($"group_registrations_attendance:{groupId}", userId.ToString(),
                        attendance);
                }

                // Store user info
                await redis.HashSetAsync($"user_info:{userId}", "user_id", userId.ToString());
                await redis.HashSetAsync($"user_info:{userId}", "first_name", user.FirstName);
                await redis.HashSetAsync($"user_info:{userId}", "last_name", user.LastName ?? "");
                await redis.HashSetAsync($"user_info:{userId}", "username", user.Username ?? "");

                // Add group to groups list
                await redis.SetAddAsync("groups_list", groupId.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if user is already registered for a group
        /// </summary>
        public static Task<string?> GetRegistration(IRedisHelper redis, long groupId, long userId)
        {
            return redis.HashGetAsync($"group_registrations:{groupId}", userId.ToString());
        }

        /// <summary>
        /// Get all registered users for a group
        /// </summary>
        public static Task<Dictionary<string, string>> GetAllRegistrations(IRedisHelper redis, long groupId)
        {
            return redis.HashGetAllAsync($"group_registrations:{groupId}");
        }

        /// <summary>
        /// Get all attendance records for a group
        /// </summary>
        public static Task<Dictionary<string, string>> GetAllAttendances(IRedisHelper redis, long groupId)
        {
            return redis.HashGetAllAsync($"group_registrations_attendance:{groupId}");
        }

        /// <summary>
        /// Get user info from Redis
        /// </summary>
        public static Task<Dictionary<string, string>> GetUserInfo(IRedisHelper redis, long userId)
        {
            return redis.HashGetAllAsync($"user_info:{userId}");
        }

        /// <summary>
        /// Store group info
        /// </summary>
        public static async Task StoreGroupInfo(IRedisHelper redis, long groupId, string title)
        {
            await redis.HashSetAsync($"group_info:{groupId}", "title", title);
            await redis.SetAddAsync("groups_list", groupId.ToString());
        }

        /// <summary>
        /// Get group title
        /// </summary>
        public static async Task<string> GetGroupTitle(IRedisHelper redis, long groupId)
        {
            var title = await redis.HashGetAsync($"group_info:{groupId}", "title");
            return string.IsNullOrEmpty(title) ? "Unknown Group" : title;
        }

        /// <summary>
        /// Get all registered groups
        /// </summary>
        public static async Task<List<string>> GetAllGroups(IRedisHelper redis)
        {
            var groups = await redis.SetMembersAsync("groups_list");
            return groups.ToList();
        }

        /// <summary>
        /// Check if user is member of a group
        /// </summary>
        public static async Task<bool> IsMemberOfGroup(ITelegramBotClient bot, long groupId, long userId)
        {
            try
            {
                var member = await bot.GetChatMember(groupId, userId);
                return member.Status != ChatMemberStatus.Left && member.Status != ChatMemberStatus.Kicked;
            }
            catch
            {
                return false;
            }
        }
    }
}
