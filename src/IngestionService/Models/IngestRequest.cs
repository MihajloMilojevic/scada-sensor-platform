namespace IngestionService.Models;

public record IngestRequest
{
    public string SensorId { get; init; } = "";
    public long MessageId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public double Value { get; init; }
    public int AlarmPriority { get; init; }
    public string Signature { get; init; } = "";
    public string EncryptedPayload { get; init; } = "";
}
