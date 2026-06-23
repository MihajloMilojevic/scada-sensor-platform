using System;
using StackExchange.Redis;
using System.Text.Json;
using System.Threading.Tasks;

namespace ApiGateway.Services;

public record CachedClaims(string Sub, string Role, string? Scope, long Exp);

public class TokenCacheService(IConnectionMultiplexer mux)
{
    private readonly IDatabase _db = mux.GetDatabase();

    public async Task<CachedClaims?> GetAsync(string tokenHash)
    {
        var val = await _db.StringGetAsync($"token:{tokenHash}");
        if (val.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<CachedClaims>(val!);
    }

    public async Task SetAsync(string tokenHash, CachedClaims claims, TimeSpan ttl)
    {
        var json = JsonSerializer.Serialize(claims);
        await _db.StringSetAsync($"token:{tokenHash}", json, ttl);
    }
}