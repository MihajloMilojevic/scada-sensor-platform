using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using SensorManagementService.Data;

namespace SensorManagementService.Services;

public class HeartbeatMonitor(
    IConnectionMultiplexer redis,
    IServiceScopeFactory scopeFactory,
    PoolManager poolManager,
    ILogger<HeartbeatMonitor> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await CheckHeartbeatsAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Heartbeat check error");
            }
        }
    }

    private async Task CheckHeartbeatsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SensorMgmtDbContext>();
        var redisDb = redis.GetDatabase();

        // Only check sensors that have sent at least one heartbeat (LastSeenAt != null)
        // Sensors seeded as ACTIVE but not yet connected are left alone until first contact
        var activeSensors = await db.Sensors
            .Where(s => s.Status == "ACTIVE" && s.LastSeenAt != null)
            .ToListAsync(ct);

        foreach (var sensor in activeSensors)
        {
            if (sensor.BlockedUntilAt.HasValue && sensor.BlockedUntilAt.Value > DateTimeOffset.UtcNow)
                continue;

            var exists = await redisDb.KeyExistsAsync($"heartbeat:{sensor.SensorId}");
            if (!exists)
            {
                logger.LogWarning("Heartbeat expired for {SensorId}", sensor.SensorId);
                await poolManager.HandleSensorDownAsync(sensor.SensorId, db, ct);
            }
        }
    }
}
