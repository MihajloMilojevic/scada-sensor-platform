using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnalyticsService.Data;
using AnalyticsService.Models;
using AnalyticsService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Composition;

public class ReportComposer(
    IConfiguration config,
    IHttpClientFactory httpFactory,
    AnalyticsDbContext db,
    ServiceTokenProvider tokenProvider,
    ILogger<ReportComposer> logger)
{
    private readonly int _ttlSeconds = int.Parse(config["Cache:TtlSeconds"] ?? "30");

    private string IngestionUrl  => config["Services:Ingestion"]  ?? "http://ingestion-service:8080";
    private string ConsensusUrl  => config["Services:Consensus"]  ?? "http://consensus-service:8080";
    private string SensorMgmtUrl => config["Services:SensorMgmt"] ?? "http://sensormgmt-service:8080";

    public Task<object?> GetHistoryAsync(string? sensorId, string? from, string? to, CancellationToken ct)
    {
        var qs = BuildQueryString(("sensorId", sensorId), ("from", from), ("to", to));
        return GetOrFetchAsync($"history:{qs}", () =>
            FetchAsync($"{IngestionUrl}/api/ingest/measurements{qs}", ct), ct);
    }

    public Task<object?> GetConsensusAsync(string? from, string? to, CancellationToken ct)
    {
        var qs = BuildQueryString(("from", from), ("to", to));
        return GetOrFetchAsync($"consensus:{qs}", () =>
            FetchAsync($"{ConsensusUrl}/api/consensus{qs}", ct), ct);
    }

    public Task<object?> GetQualityChangesAsync(CancellationToken ct)
        => GetOrFetchAsync("quality-changes:", () =>
            FetchAsync($"{ConsensusUrl}/api/consensus/quality-changes", ct), ct);

    public Task<object?> GetSensorsAsync(CancellationToken ct)
        => GetOrFetchAsync("sensors:", () =>
            FetchAsync($"{SensorMgmtUrl}/api/sensors", ct), ct);

    public async Task<object?> GetSummaryAsync(CancellationToken ct)
        => await GetOrFetchAsync("summary:", async () =>
        {
            var sensors        = await FetchAsync($"{SensorMgmtUrl}/api/sensors", ct);
            var consensus      = await FetchAsync($"{ConsensusUrl}/api/consensus", ct);
            var qualityChanges = await FetchAsync($"{ConsensusUrl}/api/consensus/quality-changes", ct);

            return (object?)new { sensors, consensus, qualityChanges, generatedAt = DateTimeOffset.UtcNow };
        }, ct);

    // ── Cache helpers ─────────────────────────────────────────────────────────

    private async Task<object?> GetOrFetchAsync(string key, Func<Task<object?>> fetch, CancellationToken ct)
    {
        var cacheKey = ComputeHash(key);
        var cached   = await db.ReportCache.FindAsync([cacheKey], ct);
        if (cached != null && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            logger.LogDebug("Cache hit: {Key}", key);
            return JsonSerializer.Deserialize<object>(cached.Payload);
        }

        var result  = await fetch();
        var payload = JsonSerializer.Serialize(result);
        var now     = DateTimeOffset.UtcNow;

        if (cached == null)
        {
            db.ReportCache.Add(new ReportCache
            {
                CacheKey  = cacheKey,
                Payload   = payload,
                CreatedAt = now,
                ExpiresAt = now.AddSeconds(_ttlSeconds)
            });
        }
        else
        {
            cached.Payload   = payload;
            cached.CreatedAt = now;
            cached.ExpiresAt = now.AddSeconds(_ttlSeconds);
        }

        try { await db.SaveChangesAsync(ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to write cache for {Key}", key); }

        return result;
    }

    private async Task<object?> FetchAsync(string url, CancellationToken ct)
    {
        var client = httpFactory.CreateClient("upstream");
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", tokenProvider.GetToken());

        try
        {
            var res  = await client.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            return res.IsSuccessStatusCode
                ? JsonSerializer.Deserialize<object>(body)
                : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch {Url}", url);
            return null;
        }
    }

    private static string BuildQueryString(params (string, string?)[] pairs)
    {
        var parts = pairs
            .Where(p => !string.IsNullOrEmpty(p.Item2))
            .Select(p => $"{Uri.EscapeDataString(p.Item1)}={Uri.EscapeDataString(p.Item2!)}");
        var qs = string.Join("&", parts);
        return qs.Length > 0 ? "?" + qs : "";
    }

    private static string ComputeHash(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant()[..32];
}