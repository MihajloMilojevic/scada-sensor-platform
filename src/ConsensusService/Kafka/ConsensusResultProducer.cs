using Scada.Shared.Contracts;
using Scada.Shared.Kafka;

namespace ConsensusService.Kafka;

public class ConsensusResultProducer(IConfiguration config) : IDisposable
{
    private readonly KafkaProducer<ConsensusResultMessage> _producer = new(
        config["Kafka:BootstrapServers"] ?? "localhost:9092");

    public Task PublishAsync(ConsensusResultMessage msg, CancellationToken ct = default)
        => _producer.ProduceAsync("consensus.result", msg.SensorId, msg, ct);

    public void Dispose() => _producer.Dispose();
}
