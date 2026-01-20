using SiberVatan.Interfaces;

namespace SiberVatan.Helpers
{
    /// <summary>
    /// Helper class for managing global message extras stored in Redis
    /// </summary>
    public static class ExtraHelper
    {
        private const string ExtrasKey = "extras";
        private static IRedisHelper? _redis;

        /// <summary>
        /// Initialize with Redis helper instance
        /// </summary>
        public static void Initialize(IRedisHelper redis)
        {
            _redis = redis;
        }

        /// <summary>
        /// Save an extra message with hashtag
        /// </summary>
        public static async Task<bool> SaveExtra(string hashtag, string content)
        {
            if (_redis == null)
                throw new InvalidOperationException("ExtraHelper not initialized");

            // Remove # prefix if present
            hashtag = hashtag.TrimStart('#').ToLower();
            if (string.IsNullOrWhiteSpace(hashtag))
                return false;

            return await _redis.HashSetAsync(ExtrasKey, hashtag, content);
        }

        /// <summary>
        /// Get all extras as dictionary
        /// </summary>
        public static Task<Dictionary<string, string>> GetAllExtras()
        {
            return _redis == null
                ? throw new InvalidOperationException("ExtraHelper not initialized")
                : _redis.HashGetAllAsync(ExtrasKey);
        }

        /// <summary>
        /// Get a specific extra by hashtag
        /// </summary>
        public static Task<string?> GetExtra(string hashtag)
        {
            if (_redis == null)
                throw new InvalidOperationException("ExtraHelper not initialized");

            hashtag = hashtag.TrimStart('#').ToLower();
            return _redis.HashGetAsync(ExtrasKey, hashtag);
        }

        /// <summary>
        /// Delete an extra by hashtag
        /// </summary>
        public static Task<bool> DeleteExtra(string hashtag)
        {
            if (_redis == null)
                throw new InvalidOperationException("ExtraHelper not initialized");

            hashtag = hashtag.TrimStart('#').ToLower();
            return _redis.HashDeleteAsync(ExtrasKey, hashtag);
        }

        /// <summary>
        /// Check if extra exists
        /// </summary>
        public static Task<bool> ExtraExists(string hashtag)
        {
            if (_redis == null)
                throw new InvalidOperationException("ExtraHelper not initialized");

            hashtag = hashtag.TrimStart('#').ToLower();
            return _redis.HashExistsAsync(ExtrasKey, hashtag);
        }
    }
}
