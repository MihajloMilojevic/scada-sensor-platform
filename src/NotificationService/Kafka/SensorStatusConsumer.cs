using NotificationService.Dispatch;
using Scada.Shared.Contracts;
using Scada.Shared.Kafka;

namespace NotificationService.Kafka;

public class SensorStatusConsumer(
    IConfiguration config,
    Dispatcher dispatcher) : BackgroundService
{
    private readonly KafkaConsumer<SensorStatusMessage> _consumer = new(
        config["Kafka:BootstrapServers"] ?? "localhost:9092",
        "notification-sensorstatus",
        "sensor.status");

    protected override async Task ExecuteAsync(CancellationToken ct)
        => await _consumer.ConsumeLoopAsync((_, msg, token) => dispatcher.HandleStatusChange(msg, token), ct);

    public override void Dispose() { _consumer.Dispose(); base.Dispose(); }
}
