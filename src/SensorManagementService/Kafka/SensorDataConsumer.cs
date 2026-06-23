using Microsoft.EntityFrameworkCore;
using Scada.Shared.Contracts;
using Scada.Shared.Kafka;
using StackExchange.Redis;
using SensorManagementService.Data;

namespace SensorManagementService.Kafka;

public class SensorDataConsumer(
    IConfiguration config,
    IConnectionMultiplexer redis,
    IServiceScopeFactory scopeFactory,
    ILogger<SensorDataConsumer> logger)
    : BackgroundService
{
    private readonly KafkaConsumer<SensorDataMessage> _consumer = new(
        config["Kafka:BootstrapServers"] ?? "localhost:9092",
        "sensormgmt-sensordata",
        "sensor.data");

    private readonly int _ttlSeconds = int.Parse(config["Heartbeat:TtlSeconds"] ?? "10");
    private readonly HashSet<string> _seenSensors = [];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _consumer.ConsumeLoopAsync(async (key, msg, token) =>
        {
            try
            {
                var db = redis.GetDatabase();
                await db.StringSetAsync(
                    $"heartbeat:{msg.SensorId}",
                    "1",
                    TimeSpan.FromSeconds(_ttlSeconds));

                // Mark last_seen_at once when sensor first connects
                if (_seenSensors.Add(msg.SensorId))
                {
                    using var scope = scopeFactory.CreateScope();
                    var dbCtx = scope.ServiceProvider.GetRequiredService<SensorMgmtDbContext>();
                    var sensor = await dbCtx.Sensors.FindAsync([msg.SensorId], token);
                    if (sensor != null && sensor.LastSeenAt == null)
                    {
                        sensor.LastSeenAt = DateTimeOffset.UtcNow;
                        sensor.UpdatedAt = DateTimeOffset.UtcNow;
                        await dbCtx.SaveChangesAsync(token);
                        logger.LogInformation("Sensor {SensorId} first heartbeat recorded", msg.SensorId);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to refresh heartbeat for {SensorId}", msg.SensorId);
            }
        }, ct);
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
