using System.Collections.Concurrent;

namespace Scada.Shared.Security;

public class ReplayGuard
{
    private readonly ConcurrentDictionary<string, (long LastMsgId, DateTime LastTimestamp)> _state = new();
    private readonly TimeSpan _window;

    public ReplayGuard(TimeSpan? window = null)
    {
        _window = window ?? TimeSpan.FromSeconds(30);
    }

    public bool IsValid(string sensorId, long msgId, DateTime timestamp)
    {
        var now = DateTime.UtcNow;
        if (Math.Abs((now - timestamp).TotalSeconds) > _window.TotalSeconds)
            return false;

        bool accepted = false;
        _state.AddOrUpdate(
            sensorId,
            _ => { accepted = true; return (msgId, timestamp); },
            (_, existing) =>
            {
                if (msgId > existing.LastMsgId)
                {
                    accepted = true;
                    return (msgId, timestamp);
                }
                return existing;
            });
        return accepted;
    }
}
