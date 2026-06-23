using IngestionService.InfluxDb;
using IngestionService.Kafka;

namespace IngestionService.Pipeline;

public class FlushWorker(
    RotationManager rotation,
    WriteAheadLog wal,
    InfluxWriter influx,
    SensorDataProducer kafka,
    ILogger<FlushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!rotation.ShouldFlush()) continue;
            try { await FlushAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Flush failed"); }
        }
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        var batch = rotation.Rotate();
        if (batch.Count == 0) return;

        logger.LogInformation("Flushing {Count} messages", batch.Count);

        // Rotate WAL files for all sensors in this batch (before writing, so new inbound goes to fresh WAL)
        var sensorIds = batch.Select(m => m.SensorId).Distinct();
        var flushingPaths = sensorIds
            .Select(id => wal.RotateToFlushing(id))
            .Where(p => p != null)
            .ToList();

        // Write to InfluxDB and Kafka
        await influx.WriteAsync(batch, ct);
        foreach (var msg in batch)
            await kafka.ProduceAsync(msg, ct);

        // Clean up WAL segments for the flushed batch
        foreach (var path in flushingPaths)
            wal.DeleteSegment(path!);

        logger.LogInformation("Flush complete: {Count} messages", batch.Count);
    }
}
