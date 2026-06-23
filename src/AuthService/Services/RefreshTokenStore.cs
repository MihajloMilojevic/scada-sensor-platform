using System;
using System.Linq;
using StackExchange.Redis;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuthService.Services;

public class RefreshTokenStore
{
    private readonly IConnectionMultiplexer _mux;
    private readonly IDatabase _db;
    private static readonly TimeSpan Expiry = TimeSpan.FromDays(7);

    // Atomically checks and marks token as used.
    // Returns: 1=success, 0=not found, 2=already used (reuse attack)
    private const string UseScript = """
        local val = redis.call('GET', KEYS[1])
        if val == false then return 0 end
        local data = cjson.decode(val)
        if data['used'] then return 2 end
        data['used'] = true
        local ttl = redis.call('TTL', KEYS[1])
        if ttl > 0 then
            redis.call('SET', KEYS[1], cjson.encode(data), 'EX', ttl)
        else
            redis.call('SET', KEYS[1], cjson.encode(data))
        end
        return 1
        """;

    public RefreshTokenStore(IConnectionMultiplexer mux)
    {
        _mux = mux;
        _db = mux.GetDatabase();
    }

    public async Task StoreAsync(string userId, string tokenId)
    {
        var key = Key(userId, tokenId);
        var value = JsonSerializer.Serialize(new { issuedAt = DateTimeOffset.UtcNow, used = false });
        await _db.StringSetAsync(key, value, Expiry);
    }

    public async Task<int> UseTokenAsync(string userId, string tokenId)
    {
        var result = await _db.ScriptEvaluateAsync(UseScript, new RedisKey[] { Key(userId, tokenId) });
        return (int)result;
    }

    public async Task RevokeAllAsync(string userId)
    {
        var server = _mux.GetServer(_mux.GetEndPoints().First());
        var keys = server.Keys(pattern: $"refresh:{userId}:*").ToArray();
        if (keys.Length > 0)
            await _db.KeyDeleteAsync(keys);
    }

    private static string Key(string userId, string tokenId) => $"refresh:{userId}:{tokenId}";
}