using Microsoft.EntityFrameworkCore;
using Scada.Shared.Contracts;
using SensorManagementService.Data;
using SensorManagementService.Kafka;
using SensorManagementService.Models;

namespace SensorManagementService.Services;

public class PoolManager(
    SensorStatusProducer producer,
    SensorCommandSender commandSender,
    ILogger<PoolManager> logger)
{
    private const int TargetActiveCount = 5;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Senzor se prijavio kao spreman (READY).
    /// Ako fali aktivnih → aktiviraj ga. Ako ih ima dovoljno → pošalji STOP.
    /// </summary>
    public async Task HandleSensorReadyAsync(string sensorId, SensorMgmtDbContext db, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var sensor = await db.Sensors.FindAsync([sensorId], ct);
            if (sensor == null) return;

            var activeCount = await db.Sensors.CountAsync(s => s.Status == "ACTIVE", ct);

            if (activeCount < TargetActiveCount)
            {
                // Fali aktivnih — aktiviraj ovaj senzor
                logger.LogInformation("Sensor {SensorId} READY → activating (active={Count})", sensorId, activeCount);
                sensor.BlockedUntilAt = null;
                await TransitionAsync(sensor, "ACTIVE", "READY_ACTIVATE", db, ct);
                await commandSender.SendStartAsync(sensorId);
            }
            else
            {
                // Dovoljno aktivnih — pošalji u STANDBY
                logger.LogInformation("Sensor {SensorId} READY → standby (active={Count})", sensorId, activeCount);
                sensor.BlockedUntilAt = null;
                if (sensor.Status != "STANDBY")
                    await TransitionAsync(sensor, "STANDBY", "READY_STANDBY", db, ct);
                else
                    await db.SaveChangesAsync(ct); // samo briši BlockedUntilAt
                await commandSender.SendStopAsync(sensorId);
            }
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// Senzor prestao slati heartbeat — failover na rezervni.
    /// </summary>
    public async Task HandleSensorDownAsync(string sensorId, SensorMgmtDbContext db, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var sensor = await db.Sensors.FindAsync([sensorId], ct);
            if (sensor == null || sensor.Status != "ACTIVE") return;

            await TransitionAsync(sensor, "INACTIVE", "AUTO_FAILOVER", db, ct);
            await commandSender.SendStopAsync(sensorId);

            await PromoteStandbyAsync(db, ct);
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// Ručna aktivacija senzora sa UI-a.
    /// Ako ima mjesta (ispod 5 aktivnih) — aktiviraj.
    /// Ako ih ima dovoljno — pređi u STANDBY (senzor čeka red).
    /// </summary>
    public async Task ManualActivateAsync(string sensorId, SensorMgmtDbContext db, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var sensor = await db.Sensors.FindAsync([sensorId], ct)
                ?? throw new KeyNotFoundException($"Sensor {sensorId} not found");

            var activeCount = await db.Sensors.CountAsync(s => s.Status == "ACTIVE", ct);

            if (activeCount < TargetActiveCount)
            {
                // Ima mjesta — aktiviraj
                await TransitionAsync(sensor, "ACTIVE", "MANUAL_ACTIVATE", db, ct);
                await commandSender.SendStartAsync(sensorId);
                logger.LogInformation("Sensor {SensorId} manually activated", sensorId);
            }
            else
            {
                // Nema mjesta — stavi u STANDBY, čeka red
                await TransitionAsync(sensor, "STANDBY", "MANUAL_STANDBY", db, ct);
                await commandSender.SendStopAsync(sensorId);
                logger.LogInformation("Sensor {SensorId} → STANDBY (already {Count} active)", sensorId, activeCount);
            }
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// Ručna deaktivacija senzora sa UI-a.
    /// Automatski aktivira rezervni da se održi 5 aktivnih.
    /// </summary>
    public async Task ManualDeactivateAsync(string sensorId, SensorMgmtDbContext db, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var sensor = await db.Sensors.FindAsync([sensorId], ct)
                ?? throw new KeyNotFoundException($"Sensor {sensorId} not found");

            await TransitionAsync(sensor, "INACTIVE", "MANUAL_DEACTIVATE", db, ct);
            await commandSender.SendStopAsync(sensorId);

            // Uvijek aktiviraj rezervni da se održi 5
            await PromoteStandbyAsync(db, ct);
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// Block — deaktiviraj i zamijeni rezervnim. Simulator dobija BLOCK komandu.
    /// Kad se simulator vrati šalje READY i tada odlučujemo.
    /// </summary>
    public async Task BlockSensorAsync(string sensorId, int durationSeconds, SensorMgmtDbContext db, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var sensor = await db.Sensors.FindAsync([sensorId], ct);
            if (sensor == null) return;

            sensor.BlockedUntilAt = DateTimeOffset.UtcNow.AddSeconds(durationSeconds);
            await TransitionAsync(sensor, "INACTIVE", "BLOCKED", db, ct);

            // Pošalji BLOCK komandu — simulator se budi nakon N sekundi i šalje READY
            await commandSender.SendBlockAsync(sensorId, durationSeconds);

            // Aktiviraj rezervni odmah
            await PromoteStandbyAsync(db, ct);
        }
        finally { _lock.Release(); }
    }

    private async Task PromoteStandbyAsync(SensorMgmtDbContext db, CancellationToken ct)
    {
        var activeCount = await db.Sensors.CountAsync(s => s.Status == "ACTIVE", ct);
        if (activeCount >= TargetActiveCount) return;

        var standby = await db.Sensors
            .Where(s => s.Status == "STANDBY")
            .OrderBy(s => s.SensorId)
            .FirstOrDefaultAsync(ct);

        if (standby != null)
        {
            await TransitionAsync(standby, "ACTIVE", "AUTO_PROMOTE", db, ct);
            await commandSender.SendStartAsync(standby.SensorId);
        }
        else
            logger.LogWarning("No STANDBY sensor available to promote!");
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
