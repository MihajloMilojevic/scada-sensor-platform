using System.Collections.Concurrent;

namespace ConsensusService.State;

public class StateStoreManager
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<double>> _storeA = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<double>> _storeB = new();
    private volatile ConcurrentDictionary<string, ConcurrentBag<double>> _active;

    public StateStoreManager() => _active = _storeA;

    public void AddValue(string sensorId, double value)
        => _active.GetOrAdd(sensorId, _ => new ConcurrentBag<double>()).Add(value);

    public IReadOnlyDictionary<string, double[]> SwapAndSnapshot()
    {
        var current = _active;
        var next = ReferenceEquals(current, _storeA) ? _storeB : _storeA;
        next.Clear();
        _active = next;
        return current.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }
}
