using Microsoft.EntityFrameworkCore;
using Scada.Shared.Contracts;
using SensorManagementService.Data;
using SensorManagementService.Kafka;
using SensorManagementService.Models;

namespace SensorManagementService.Services;

public class PoolManager(SensorStatusProducer producer, ILogger<PoolManager> logger)
{
    private const int TargetActiveCount = 5;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task HandleSensorDownAsync(string sensorId, SensorMgmtDbContext db, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var sensor = await db.Sensors.FindAsync([sensorId], ct);
            if (sensor == null || sensor.Status != "ACTIVE") return;

            await TransitionAsync(sensor, "INACTIVE", "AUTO_FAILOVER", db, ct);

            var activeCount = await db.Sensors.CountAsync(s => s.Status == "ACTIVE", ct);
            if (activeCount < TargetActiveCount)
            {
                var standby = await db.Sensors
                    .Where(s => s.Status == "STANDBY")
                    .OrderBy(s => s.SensorId)
                    .FirstOrDefaultAsync(ct);

                if (standby != null)
                    await TransitionAsync(standby, "ACTIVE", "AUTO_FAILOVER", db, ct);
                else
                    logger.LogWarning("No STANDBY sensor available to promote");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ManualTransitionAsync(
        string sensorId, string newStatus, string reason,
        SensorMgmtDbContext db, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var sensor = await db.Sensors.FindAsync([sensorId], ct)
                ?? throw new KeyNotFoundException($"Sensor {sensorId} not found");
            await TransitionAsync(sensor, newStatus, reason, db, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task TransitionAsync(
        Sensor sensor, string newStatus, string reason,
        SensorMgmtDbContext db, CancellationToken ct)
    {
        var oldStatus = sensor.Status;
        sensor.Status = newStatus;
        sensor.UpdatedAt = DateTimeOffset.UtcNow;

        db.SensorStatusHistory.Add(new SensorStatusHistory
        {
            SensorId = sensor.SensorId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Reason = reason,
            ChangedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("{SensorId}: {Old} → {New} ({Reason})",
            sensor.SensorId, oldStatus, newStatus, reason);

        await producer.PublishAsync(new SensorStatusMessage
        {
            SensorId = sensor.SensorId,
            Status = Enum.Parse<SensorStatus>(newStatus),
            PreviousStatus = Enum.Parse<SensorStatus>(oldStatus),
            Reason = reason,
            Timestamp = DateTimeOffset.UtcNow
        }, ct);
    }
}
