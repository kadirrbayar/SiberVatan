namespace SiberVatan.Interfaces
{
    /// <summary>
    /// Comprehensive Redis Helper interface supporting all Redis data types with dynamic keys
    /// </summary>
    public interface IRedisHelper
    {
        // Connection Management
        void Connect();
        bool IsConnected { get; }

        // ==================== STRING OPERATIONS ====================

        /// <summary>
        /// Set a string value with optional expiration
        /// </summary>
        Task<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null);

        /// <summary>
        /// Get a string value
        /// </summary>
        Task<string?> StringGetAsync(string key);

        /// <summary>
        /// Increment a numeric string value
        /// </summary>
        Task<long> StringIncrementAsync(string key, long value = 1);

        /// <summary>
        /// Decrement a numeric string value
        /// </summary>
        Task<long> StringDecrementAsync(string key, long value = 1);

        // ==================== LIST OPERATIONS ====================

        /// <summary>
        /// Push value to list (left or right)
        /// </summary>
        Task<long> ListPushAsync(string key, string value, bool right = true);

        /// <summary>
        /// Pop value from list (left or right)
        /// </summary>
        Task<string?> ListPopAsync(string key, bool right = false);

        /// <summary>
        /// Get range of values from list
        /// </summary>
        Task<List<string>> ListRangeAsync(string key, long start = 0, long stop = -1);

        /// <summary>
        /// Get list length
        /// </summary>
        Task<long> ListLengthAsync(string key);

        /// <summary>
        /// Remove value from list
        /// </summary>
        Task<long> ListRemoveAsync(string key, string value);

        // ==================== SET OPERATIONS ====================

        /// <summary>
        /// Add member to set
        /// </summary>
        Task<bool> SetAddAsync(string key, string value);

        /// <summary>
        /// Remove member from set
        /// </summary>
        Task<bool> SetRemoveAsync(string key, string value);

        /// <summary>
        /// Get all members of set
        /// </summary>
        Task<List<string>> SetMembersAsync(string key);

        /// <summary>
        /// Check if set contains member
        /// </summary>
        Task<bool> SetContainsAsync(string key, string value);

        /// <summary>
        /// Get set cardinality (count)
        /// </summary>
        Task<long> SetLengthAsync(string key);

        // ==================== HASH OPERATIONS ====================

        /// <summary>
        /// Set hash field
        /// </summary>
        Task<bool> HashSetAsync(string key, string field, string value);

        /// <summary>
        /// Get hash field value
        /// </summary>
        Task<string?> HashGetAsync(string key, string field);

        /// <summary>
        /// Get all hash fields and values
        /// </summary>
        Task<Dictionary<string, string>> HashGetAllAsync(string key);

        /// <summary>
        /// Delete hash field
        /// </summary>
        Task<bool> HashDeleteAsync(string key, string field);

        /// <summary>
        /// Check if hash field exists
        /// </summary>
        Task<bool> HashExistsAsync(string key, string field);

        /// <summary>
        /// Get all field names in hash
        /// </summary>
        Task<List<string>> HashKeysAsync(string key);

        /// <summary>
        /// Get all values in hash
        /// </summary>
        Task<List<string>> HashValuesAsync(string key);

        /// <summary>
        /// Increment a numeric hash field value
        /// </summary>
        Task<long> HashIncrementAsync(string key, string field, long value = 1);

        /// <summary>
        /// Decrement a numeric hash field value
        /// </summary>
        Task<long> HashDecrementAsync(string key, string field, long value = 1);

        // ==================== SORTED SET OPERATIONS ====================

        /// <summary>
        /// Add member to sorted set with score
        /// </summary>
        Task<bool> SortedSetAddAsync(string key, string member, double score);

        /// <summary>
        /// Remove member from sorted set
        /// </summary>
        Task<bool> SortedSetRemoveAsync(string key, string member);

        /// <summary>
        /// Get range from sorted set by rank
        /// </summary>
        Task<List<string>> SortedSetRangeAsync(string key, long start = 0, long stop = -1, bool descending = false);

        /// <summary>
        /// Get range from sorted set by score
        /// </summary>
        Task<List<string>> SortedSetRangeByScoreAsync(string key, double min = double.NegativeInfinity,
            double max = double.PositiveInfinity);

        /// <summary>
        /// Get member's score
        /// </summary>
        Task<double?> SortedSetScoreAsync(string key, string member);

        /// <summary>
        /// Get sorted set cardinality (count)
        /// </summary>
        Task<long> SortedSetLengthAsync(string key);

        // ==================== KEY MANAGEMENT ====================

        /// <summary>
        /// Check if key exists
        /// </summary>
        Task<bool> KeyExistsAsync(string key);

        /// <summary>
        /// Delete key
        /// </summary>
        Task<bool> KeyDeleteAsync(string key);

        /// <summary>
        /// Delete multiple keys
        /// </summary>
        Task<long> KeyDeleteAsync(params string[] keys);

        /// <summary>
        /// Set expiration on key
        /// </summary>
        Task<bool> KeyExpireAsync(string key, TimeSpan expiry);

        /// <summary>
        /// Get keys matching pattern
        /// </summary>
        Task<List<string>> GetKeysAsync(string pattern = "*");

        /// <summary>
        /// Get time to live for key
        /// </summary>
        Task<TimeSpan?> KeyTimeToLiveAsync(string key);
    }
}
