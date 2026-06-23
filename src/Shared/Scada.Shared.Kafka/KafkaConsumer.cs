using System;
using Confluent.Kafka;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Scada.Shared.Kafka;

public class KafkaConsumer<TValue> : IDisposable
{
    private readonly IConsumer<string, string> _consumer;

    public KafkaConsumer(string bootstrapServers, string groupId, string topic)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = true
        };
        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe(topic);
    }

    public async Task ConsumeLoopAsync(
        Func<string, TValue, CancellationToken, Task> handler,
        CancellationToken ct)
    {
        await Task.Run(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(ct);
                    if (result?.Message?.Value == null) continue;
                    var value = JsonSerializer.Deserialize<TValue>(
                        result.Message.Value,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                        });
                    if (value != null)
                        handler(result.Message.Key, value, ct).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) { break; }
                catch (ConsumeException) { }
                catch when (!ct.IsCancellationRequested) { }
            }
        }, CancellationToken.None);
    }

    public void Dispose()
    {
        try { _consumer.Close(); } catch { }
        _consumer.Dispose();
    }
}