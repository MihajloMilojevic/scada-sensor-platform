using Scada.Shared.Contracts;

namespace IngestionService.Pipeline;

// Manages the A/B double-buffer swap and exposes flush-trigger logic.
public class RotationManager(IConfiguration config)
{
    private readonly int _maxBatchSize = int.Parse(config["Flush:MaxBatchSize"] ?? "100");
    private readonly TimeSpan _maxBatchAge = TimeSpan.FromSeconds(
        double.Parse(config["Flush:MaxBatchAgeSeconds"] ?? "5"));

    private BatchStore _active = new();
    private readonly object _swapLock = new();

    public void Add(SensorDataMessage msg)
    {
        lock (_swapLock) _active.Add(msg);
    }

    public bool ShouldFlush() =>
        _active.Count > 0 &&
        (_active.Count >= _maxBatchSize || _active.Age >= _maxBatchAge);

    // Atomically swaps active/standby, returns the completed batch.
    public List<SensorDataMessage> Rotate()
    {
        BatchStore old;
        lock (_swapLock)
        {
            old = _active;
            _active = new BatchStore();
        }
        return old.DrainAll();
    }
}
