using ConsensusService.State;
using Scada.Shared.Contracts;
using Scada.Shared.Kafka;

namespace ConsensusService.Kafka;

public class SensorDataConsumer(
    IConfiguration config,
    StateStoreManager stateManager) : BackgroundService
{
    private readonly KafkaConsumer<SensorDataMessage> _consumer = new(
        config["Kafka:BootstrapServers"] ?? "localhost:9092",
        "consensus-sensordata",
        "sensor.data");

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _consumer.ConsumeLoopAsync((key, msg, token) =>
        {
            stateManager.AddValue(msg.SensorId, msg.Value);
            return Task.CompletedTask;
        }, ct);
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
