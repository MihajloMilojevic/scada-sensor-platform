using Scada.Shared.Contracts;
using Scada.Shared.Kafka;

namespace IngestionService.Kafka;

public class SensorDataProducer : IDisposable
{
    private readonly KafkaProducer<SensorDataMessage> _producer;

    public SensorDataProducer(IConfiguration config)
    {
        var servers = config["Kafka:BootstrapServers"] ?? "localhost:9092";
        _producer = new KafkaProducer<SensorDataMessage>(servers);
    }

    public Task ProduceAsync(SensorDataMessage msg, CancellationToken ct = default) =>
        _producer.ProduceAsync("sensor.data", msg.SensorId, msg, ct);

    public void Dispose() => _producer.Dispose();
}
