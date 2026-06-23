using System;
using Confluent.Kafka;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Scada.Shared.Kafka;

public class KafkaProducer<TValue> : IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaProducer(string bootstrapServers)
    {
        var config = new ProducerConfig { BootstrapServers = bootstrapServers };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    private static readonly JsonSerializerOptions _opts = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public async Task ProduceAsync(string topic, string key, TValue value, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value, _opts);
        await _producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = json }, ct);
    }

    public void Dispose() => _producer.Dispose();
}