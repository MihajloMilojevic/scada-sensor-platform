using Scada.Shared.Contracts;

namespace IngestionService.Pipeline;

// One half of the A/B double-buffer.
public class BatchStore
{
    private readonly List<SensorDataMessage> _messages = [];
    private DateTime _firstMessageAt = DateTime.MaxValue;

    public void Add(SensorDataMessage msg)
    {
        lock (_messages)
        {
            if (_messages.Count == 0)
                _firstMessageAt = DateTime.UtcNow;
            _messages.Add(msg);
        }
    }

    public int Count { get { lock (_messages) return _messages.Count; } }

    public TimeSpan Age => _messages.Count == 0
        ? TimeSpan.Zero
        : DateTime.UtcNow - _firstMessageAt;

    public List<SensorDataMessage> DrainAll()
    {
        lock (_messages)
        {
            var copy = new List<SensorDataMessage>(_messages);
            _messages.Clear();
            _firstMessageAt = DateTime.MaxValue;
            return copy;
        }
    }
}
