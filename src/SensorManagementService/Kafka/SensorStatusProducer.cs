using Scada.Shared.Contracts;
using Scada.Shared.Kafka;

namespace SensorManagementService.Kafka;

public class SensorStatusProducer : IDisposable
{
    private readonly KafkaProducer<SensorStatusMessage> _producer;

    public SensorStatusProducer(IConfiguration config)
    {
        var servers = config["Kafka:BootstrapServers"] ?? "localhost:9092";
        _producer = new KafkaProducer<SensorStatusMessage>(servers);
    }

    public Task PublishAsync(SensorStatusMessage msg, CancellationToken ct = default) =>
        _producer.ProduceAsync("sensor.status", msg.SensorId, msg, ct);

    public void Dispose() => _producer.Dispose();
}
