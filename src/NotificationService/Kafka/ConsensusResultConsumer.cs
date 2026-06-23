using NotificationService.Dispatch;
using Scada.Shared.Contracts;
using Scada.Shared.Kafka;

namespace NotificationService.Kafka;

public class ConsensusResultConsumer(
    IConfiguration config,
    Dispatcher dispatcher) : BackgroundService
{
    private readonly KafkaConsumer<ConsensusResultMessage> _consumer = new(
        config["Kafka:BootstrapServers"] ?? "localhost:9092",
        "notification-consensus",
        "consensus.result");

    protected override async Task ExecuteAsync(CancellationToken ct)
        => await _consumer.ConsumeLoopAsync((_, msg, token) => dispatcher.HandleQualityChange(msg, token), ct);

    public override void Dispose() { _consumer.Dispose(); base.Dispose(); }
}
