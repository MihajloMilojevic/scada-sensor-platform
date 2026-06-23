using NotificationService.Dispatch;
using Scada.Shared.Contracts;
using Scada.Shared.Kafka;

namespace NotificationService.Kafka;

public class SensorDataConsumer(
    IConfiguration config,
    Dispatcher dispatcher) : BackgroundService
{
    private readonly KafkaConsumer<SensorDataMessage> _consumer = new(
        config["Kafka:BootstrapServers"] ?? "localhost:9092",
        "notification-sensordata",
        "sensor.data");

    protected override async Task ExecuteAsync(CancellationToken ct)
        => await _consumer.ConsumeLoopAsync((_, msg, token) => dispatcher.HandleSensorData(msg, token), ct);

    public override void Dispose() { _consumer.Dispose(); base.Dispose(); }
}
