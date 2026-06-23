using Microsoft.EntityFrameworkCore;
using Scada.Shared.Contracts;
using Scada.Shared.Kafka;
using SensorManagementService.Data;

namespace SensorManagementService.Kafka;

public class ConsensusResultConsumer(
    IConfiguration config,
    IServiceScopeFactory scopeFactory,
    ILogger<ConsensusResultConsumer> logger)
    : BackgroundService
{
    private readonly KafkaConsumer<ConsensusResultMessage> _consumer = new(
        config["Kafka:BootstrapServers"] ?? "localhost:9092",
        "sensormgmt-consensus",
        "consensus.result");

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _consumer.ConsumeLoopAsync(async (key, msg, token) =>
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SensorMgmtDbContext>();
            try
            {
                var sensor = await db.Sensors.FindAsync([msg.SensorId], token);
                if (sensor == null) return;
                sensor.Quality = msg.NewQuality.ToString();
                sensor.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(token);
                logger.LogInformation("Updated quality for {SensorId}: {Quality}", msg.SensorId, msg.NewQuality);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to update quality for {SensorId}", msg.SensorId);
            }
        }, ct);
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
