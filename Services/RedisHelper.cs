using StackExchange.Redis;
using SiberVatan.Interfaces;

namespace SiberVatan.Services
{
    /// <summary>
    /// Comprehensive Redis Helper with support for all Redis data types and dynamic keys
    /// </summary>
    public class RedisHelper : IRedisHelper
    {
        private ConnectionMultiplexer? _redis;
        private IDatabase? _db;

        private readonly string _host;
        private readonly string _port;
        private readonly string _password;
        private readonly int _dbId;

        public bool IsConnected => _redis?.IsConnected ?? false;

        public RedisHelper()
        {
            _host = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
            _port = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
            _password = Environment.GetEnvironmentVariable("REDIS_PASSWORD") ?? "";
            _dbId = int.TryParse(Environment.GetEnvironmentVariable("REDIS_DB"), out var db) ? db : 1;

            Connect();
        }

        public void Connect()
        {
            try
            {
                var config = new ConfigurationOptions
                {
                    EndPoints = { $"{_host}:{_port}" },
                    Password = string.IsNullOrEmpty(_password) ? null : _password,
                    AbortOnConnectFail = false,
                    ConnectTimeout = 5000,
                    SyncTimeout = 5000
                };

                _redis = ConnectionMultiplexer.Connect(config);
                _db = _redis.GetDatabase(_dbId);
                Console.WriteLine($"Redis connected successfully on {_host}:{_port}, DB: {_dbId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Redis Connection Error: {ex.Message}");
                _redis = null;
                _db = null;
            }
        }

        private void EnsureConnection()
        {
            if (_db == null || !IsConnected)
            {
                throw new InvalidOperationException("Redis is not connected");
            }
        }

        // ==================== STRING OPERATIONS ====================

        public async Task<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null)
        {
            try
            {
                EnsureConnection();
                if (expiry.HasValue)
                {
                    return await _db!.StringSetAsync((RedisKey)key, (RedisValue)value, expiry.Value);
                }
                else
                {
                    return await _db!.StringSetAsync((RedisKey)key, (RedisValue)value);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StringSetAsync for key: {key} - {ex.Message}");
                return false;
            }
        }

        public async Task<string?> StringGetAsync(string key)
        {
            try
            {
                EnsureConnection();
                var value = await _db!.StringGetAsync((RedisKey)key);
                return value.HasValue ? value.ToString() : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StringGetAsync for key: {key} - {ex.Message}");
                return null;
            }
        }

        public async Task<long> StringIncrementAsync(string key, long value = 1)
        {
            try
            {
                EnsureConnection();
                return await _db!.StringIncrementAsync((RedisKey)key, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StringIncrementAsync for key: {key} - {ex.Message}");
                return 0;
            }
        }

        public async Task<long> StringDecrementAsync(string key, long value = 1)
        {
            try
            {
                EnsureConnection();
                return await _db!.StringDecrementAsync((RedisKey)key, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StringDecrementAsync for key: {key} - {ex.Message}");
                return 0;
            }
        }

        // ==================== LIST OPERATIONS ====================

        public async Task<long> ListPushAsync(string key, string value, bool right = true)
        {
            try
            {
                EnsureConnection();
                return right
                    ? await _db!.ListRightPushAsync((RedisKey)key, (RedisValue)value)
                    : await _db!.ListLeftPushAsync((RedisKey)key, (RedisValue)value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ListPushAsync for key: {key} - {ex.Message}");
                return 0;
            }
        }

        public async Task<string?> ListPopAsync(string key, bool right = false)
        {
            try
            {
                EnsureConnection();
                var value = right
                    ? await _db!.ListRightPopAsync((RedisKey)key)
                    : await _db!.ListLeftPopAsync((RedisKey)key);
                return value.HasValue ? value.ToString() : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ListPopAsync for key: {key} - {ex.Message}");
                return null;
            }
        }

        public async Task<List<string>> ListRangeAsync(string key, long start = 0, long stop = -1)
        {
            try
            {
                EnsureConnection();
                var values = await _db!.ListRangeAsync((RedisKey)key, start, stop);
                return [.. values.Select(v => v.ToString())];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ListRangeAsync for key: {key} - {ex.Message}");
                return [];
            }
        }

        public async Task<long> ListLengthAsync(string key)
        {
            try
            {
                EnsureConnection();
                return await _db!.ListLengthAsync((RedisKey)key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ListLengthAsync for key: {key} - {ex.Message}");
                return 0;
            }
        }

        public async Task<long> ListRemoveAsync(string key, string value)
        {
            try
            {
                EnsureConnection();
                return await _db!.ListRemoveAsync((RedisKey)key, (RedisValue)value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ListRemoveAsync for key: {key} - {ex.Message}");
                return 0;
            }
        }

        // ==================== SET OPERATIONS ====================

        public async Task<bool> SetAddAsync(string key, string value)
        {
            try
            {
                EnsureConnection();
                return await _db!.SetAddAsync((RedisKey)key, (RedisValue)value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SetAddAsync for key: {key} - {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetRemoveAsync(string key, string value)
        {
            try
            {
                EnsureConnection();
                return await _db!.SetRemoveAsync((RedisKey)key, (RedisValue)value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SetRemoveAsync for key: {key} - {ex.Message}");
                return false;
            }
        }

        public async Task<List<string>> SetMembersAsync(string key)
        {
            try
            {
                EnsureConnection();
                var values = await _db!.SetMembersAsync((RedisKey)key);
                return [.. values.Select(v => v.ToString())];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SetMembersAsync for key: {key} - {ex.Message}");
                return [];
            }
        }

        public async Task<bool> SetContainsAsync(string key, string value)
        {
            try
            {
                EnsureConnection();
                return await _db!.SetContainsAsync((RedisKey)key, (RedisValue)value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SetContainsAsync for key: {key} - {ex.Message}");
                return false;
            }
        }

        public async Task<long> SetLengthAsync(string key)
        {
            try
            {
                EnsureConnection();
                return await _db!.SetLengthAsync((RedisKey)key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SetLengthAsync for key: {key} - {ex.Message}");
                return 0;
            }
        }

        // ==================== HASH OPERATIONS ====================

        public async Task<bool> HashSetAsync(string key, string field, string value)
        {
            try
            {
                EnsureConnection();
                return await _db!.HashSetAsync((RedisKey)key, (RedisValue)field, (RedisValue)value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HashSetAsync for key: {key}, field: {field} - {ex.Message}");
                return false;
            }
        }

        public async Task<string?> HashGetAsync(string key, string field)
        {
            try
            {
                EnsureConnection();
                var value = await _db!.HashGetAsync((RedisKey)key, (RedisValue)field);
                return value.HasValue ? value.ToString() : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HashGetAsync for key: {key}, field: {field} - {ex.Message}");
                return null;
            }
        }

        public async Task<Dictionary<string, string>> HashGetAllAsync(string key)
        {
            try
            {
                EnsureConnection();
                var entries = await _db!.HashGetAllAsync((RedisKey)key);
                return entries.ToDictionary(
                    e => e.Name.ToString(),
                    e => e.Value.ToString()
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HashGetAllAsync for key: {key} - {ex.Message}");
                return [];
            }
        }

        public async Task<bool> HashDeleteAsync(string key, string field)
        {
            try
            {
                EnsureConnection();
                return await _db!.HashDeleteAsync((RedisKey)key, (RedisValue)field);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HashDeleteAsync for key: {key}, field: {field} - {ex.Message}");
                return false;
            }
        }

        public async Task<bool> HashExistsAsync(string key, string field)
        {
            try
            {
                EnsureConnection();
                return await _db!.HashExistsAsync((RedisKey)key, (RedisValue)field);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HashExistsAsync for key: {key}, field: {field} - {ex.Message}");
                return false;
            }
        }

        public async Task<List<string>> HashKeysAsync(string key)
        {
            try
            {
                EnsureConnection();
                var keys = await _db!.HashKeysAsync((RedisKey)key);
                return [.. keys.Select(k => k.ToString())];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HashKeysAsync for key: {key} - {ex.Message}");
                return [];
            }
        }

        public async Task<List<string>> HashValuesAsync(string key)
        {
            try
            {
                EnsureConnection();
                var values = await _db!.HashValuesAsync((RedisKey)key);
                return [.. values.Select(v => v.ToString())];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HashValuesAsync for key: {key} - {ex.Message}");
                return [];
            }
        }

        public async Task<long> HashIncrementAsync(string key, string field, long value = 1)
        {
            try
            {
                EnsureConnection();
                return await _db!.HashIncrementAsync((RedisKey)key, (RedisValue)field, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HashIncrementAsync for key: {key}, field: {field} - {ex.Message}");
                return 0;
            }
        }

        public async Task<long> HashDecrementAsync(string key, string field, long value = 1)
        {
            try
            {
                EnsureConnection();
                return await _db!.HashDecrementAsync((RedisKey)key, (RedisValue)field, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HashDecrementAsync for key: {key}, field: {field} - {ex.Message}");
                return 0;
            }
        }

        // ==================== SORTED SET OPERATIONS ====================

        public async Task<bool> SortedSetAddAsync(string key, string member, double score)
        {
            try
            {
                EnsureConnection();
                return await _db!.SortedSetAddAsync((RedisKey)key, (RedisValue)member, score);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SortedSetAddAsync for key: {key} - {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SortedSetRemoveAsync(string key, string member)
        {
            try
            {
                EnsureConnection();
                return await _db!.SortedSetRemoveAsync((RedisKey)key, (RedisValue)member);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SortedSetRemoveAsync for key: {key} - {ex.Message}");
                return false;
            }
        }

        public async Task<List<string>> SortedSetRangeAsync(string key, long start = 0, long stop = -1,
            bool descending = false)
        {
            try
            {
                EnsureConnection();
                var order = descending ? Order.Descending : Order.Ascending;
                var values = await _db!.SortedSetRangeByRankAsync((RedisKey)key, start, stop, order);
                return [.. values.Select(v => v.ToString())];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SortedSetRangeAsync for key: {key} - {ex.Message}");
                return [];
            }
        }

        public async Task<List<string>> SortedSetRangeByScoreAsync(string key, double min = double.NegativeInfinity,
            double max = double.PositiveInfinity)
        {
            try
            {
                EnsureConnection();
                var values = await _db!.SortedSetRangeByScoreAsync((RedisKey)key, min, max);
                return [.. values.Select(v => v.ToString())];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SortedSetRangeByScoreAsync for key: {key} - {ex.Message}");
                return [];
            }
        }

        public async Task<double?> SortedSetScoreAsync(string key, string member)
        {
            try
            {
                EnsureConnection();
                return await _db!.SortedSetScoreAsync((RedisKey)key, (RedisValue)member);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SortedSetScoreAsync for key: {key} - {ex.Message}");
                return null;
            }
        }

        public async Task<long> SortedSetLengthAsync(string key)
        {
            try
            {
                EnsureConnection();
                return await _db!.SortedSetLengthAsync((RedisKey)key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SortedSetLengthAsync for key: {key} - {ex.Message}");
                return 0;
            }
        }

        // ==================== KEY MANAGEMENT ====================

        public async Task<bool> KeyExistsAsync(string key)
        {
            try
            {
                EnsureConnection();
                return await _db!.KeyExistsAsync((RedisKey)key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in KeyExistsAsync for key: {key} - {ex.Message}");
                return false;
            }
        }

        public async Task<bool> KeyDeleteAsync(string key)
        {
            try
            {
                EnsureConnection();
                return await _db!.KeyDeleteAsync((RedisKey)key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in KeyDeleteAsync for key: {key} - {ex.Message}");
                return false;
            }
        }

        public async Task<long> KeyDeleteAsync(params string[] keys)
        {
            try
            {
                EnsureConnection();
                var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
                return await _db!.KeyDeleteAsync(redisKeys);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in KeyDeleteAsync for multiple keys - {ex.Message}");
                return 0;
            }
        }

        public async Task<bool> KeyExpireAsync(string key, TimeSpan expiry)
        {
            try
            {
                EnsureConnection();
                return await _db!.KeyExpireAsync((RedisKey)key, expiry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in KeyExpireAsync for key: {key} - {ex.Message}");
                return false;
            }
        }

        public async Task<List<string>> GetKeysAsync(string pattern = "*")
        {
            try
            {
                EnsureConnection();
                var server = _redis!.GetServer(_redis.GetEndPoints().First());
                var keys = server.Keys(_dbId, pattern: pattern);
                return await Task.FromResult(keys.Select(k => k.ToString()).ToList());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetKeysAsync for pattern: {pattern} - {ex.Message}");
                return [];
            }
        }

        public async Task<TimeSpan?> KeyTimeToLiveAsync(string key)
        {
            try
            {
                EnsureConnection();
                return await _db!.KeyTimeToLiveAsync((RedisKey)key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in KeyTimeToLiveAsync for key: {key} - {ex.Message}");
                return null;
            }
        }
    }
}
