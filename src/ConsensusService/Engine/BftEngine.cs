using System.Collections.Concurrent;
using ConsensusService.Data;
using ConsensusService.Kafka;
using ConsensusService.Models;
using Scada.Shared.Contracts;

namespace ConsensusService.Engine;

public class BftEngine(ConsensusResultProducer producer, ILogger<BftEngine> logger)
{
    private readonly ConcurrentDictionary<string, SensorQuality> _qualities = new();
    private readonly ConcurrentDictionary<string, int> _consecutiveOutliers = new();

    public async Task ProcessWindowAsync(
        IReadOnlyDictionary<string, double[]> snapshot,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        ConsensusDbContext db,
        CancellationToken ct)
    {
        if (snapshot.Count == 0)
        {
            logger.LogDebug("Empty snapshot, skipping consensus window");
            return;
        }

        // Per-sensor mean within the window
        var sensorMeans = snapshot.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Average());

        // Only GOOD sensors contribute to consensus
        var goodSensors = sensorMeans
            .Where(kv => _qualities.GetValueOrDefault(kv.Key, SensorQuality.GOOD) == SensorQuality.GOOD)
            .ToList();

        if (goodSensors.Count < 2)
        {
            logger.LogWarning("Not enough GOOD sensors ({Count}) for consensus — skipping window", goodSensors.Count);
            return;
        }

        double mu = goodSensors.Average(kv => kv.Value);
        double variance = goodSensors.Average(kv => Math.Pow(kv.Value - mu, 2));
        double sigma = Math.Sqrt(variance);

        // Leave-one-out z-score: compute each sensor's deviation against the OTHER sensors' mean/σ.
        // This avoids the N=5 population-stddev bound that caps z at exactly 2.0 when 1 sensor is extreme.
        var outliersThisWindow = new HashSet<string>();
        foreach (var (sensorId, mean) in goodSensors)
        {
            var others = goodSensors.Where(kv => kv.Key != sensorId).Select(kv => kv.Value).ToList();
            if (others.Count < 2) continue;
            double otherMu = others.Average();
            double otherSigma = Math.Sqrt(others.Average(v => Math.Pow(v - otherMu, 2)));
            if (otherSigma > 0 && Math.Abs(mean - otherMu) / otherSigma > 2.0)
            {
                outliersThisWindow.Add(sensorId);
                logger.LogDebug("Sensor {SensorId}: leave-one-out z={Z:F2} (mean={Mean:F1} vs others μ={OMu:F1} σ={OSig:F1})",
                    sensorId, Math.Abs(mean - otherMu) / otherSigma, mean, otherMu, otherSigma);
            }
        }

        logger.LogInformation(
            "Window [{Start:HH:mm:ss}–{End:HH:mm:ss}] μ={Mu:F2} σ={Sigma:F2} GOOD={Good} outliers={Outliers}",
            windowStart, windowEnd, mu, sigma, goodSensors.Count, outliersThisWindow.Count);

        db.ConsensusResults.Add(new ConsensusResult
        {
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            ConsensusValue = mu,
            ContributingSensors = goodSensors.Count,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Update consecutive outlier counters and check for quality downgrade
        foreach (var (sensorId, _) in goodSensors)
        {
            if (outliersThisWindow.Contains(sensorId))
                _consecutiveOutliers.AddOrUpdate(sensorId, 1, (_, c) => c + 1);
            else
                _consecutiveOutliers[sensorId] = 0;
        }

        foreach (var (sensorId, count) in _consecutiveOutliers)
        {
            if (count < 2) continue;
            if (_qualities.GetValueOrDefault(sensorId, SensorQuality.GOOD) != SensorQuality.GOOD) continue;

            var sensorMean = sensorMeans.GetValueOrDefault(sensorId, mu);
            double devSigma = sigma > 0 ? Math.Abs(sensorMean - mu) / sigma : 0;

            db.QualityChanges.Add(new QualityChange
            {
                SensorId = sensorId,
                PreviousQuality = "GOOD",
                NewQuality = "BAD",
                SensorValue = sensorMean,
                ConsensusValue = mu,
                DeviationSigma = devSigma,
                ChangedAt = DateTimeOffset.UtcNow
            });

            _qualities[sensorId] = SensorQuality.BAD;
            _consecutiveOutliers[sensorId] = 0;

            await producer.PublishAsync(new ConsensusResultMessage
            {
                SensorId = sensorId,
                PreviousQuality = SensorQuality.GOOD,
                NewQuality = SensorQuality.BAD,
                ConsensusValue = mu,
                SensorValue = sensorMean,
                DeviationSigma = devSigma,
                Timestamp = DateTimeOffset.UtcNow
            }, ct);

            logger.LogWarning("Sensor {SensorId} downgraded to BAD (σ={Sigma:F2}, consecutive={Count})",
                sensorId, devSigma, count);
        }

        await db.SaveChangesAsync(ct);
    }
}
