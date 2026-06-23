using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Scada.Shared.Contracts;

namespace IngestionService.InfluxDb;

public class InfluxWriter(IConfiguration config, ILogger<InfluxWriter> logger)
{
    private readonly string _url = config["InfluxDB:Url"] ?? "http://localhost:8086";
    private readonly string _token = config["InfluxDB:Token"] ?? "scada-influx-token";
    private readonly string _org = config["InfluxDB:Org"] ?? "scada";
    private readonly string _bucket = config["InfluxDB:Bucket"] ?? "sensor-data";

    public async Task WriteAsync(IEnumerable<SensorDataMessage> messages, CancellationToken ct = default)
    {
        using var client = new InfluxDBClient(_url, _token);
        var writeApi = client.GetWriteApiAsync();

        var points = messages.Select(msg =>
            PointData.Measurement("temperature")
                .Tag("sensorId", msg.SensorId)
                .Field("value", msg.Value)
                .Field("alarmPriority", (long)msg.AlarmPriority)
                .Field("quality", msg.Quality.ToString())
                .Timestamp(msg.Timestamp.ToUnixTimeMilliseconds(), WritePrecision.Ms))
            .ToList();

        await writeApi.WritePointsAsync(points, _bucket, _org, ct);
        logger.LogInformation("Wrote {Count} points to InfluxDB", points.Count);
    }

    public async Task<List<SensorDataMessage>> QueryAsync(
        string sensorId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        using var client = new InfluxDBClient(_url, _token);
        var queryApi = client.GetQueryApi();

        var flux = $"""
            from(bucket: "{_bucket}")
              |> range(start: {from:O}, stop: {to:O})
              |> filter(fn: (r) => r._measurement == "temperature" and r["sensorId"] == "{sensorId}")
              |> pivot(rowKey:["_time"], columnKey: ["_field"], valueColumn: "_value")
            """;

        var tables = await queryApi.QueryAsync(flux, _org, ct);
        var results = new List<SensorDataMessage>();

        foreach (var table in tables)
        {
            foreach (var record in table.Records)
            {
                var time = record.GetTime()?.ToDateTimeOffset() ?? DateTimeOffset.UtcNow;
                results.Add(new SensorDataMessage
                {
                    SensorId = record.GetValueByKey("sensorId")?.ToString() ?? sensorId,
                    Value = Convert.ToDouble(record.GetValueByKey("value") ?? 0),
                    Timestamp = time,
                    AlarmPriority = Convert.ToInt32(record.GetValueByKey("alarmPriority") ?? 0),
                    Quality = Enum.TryParse<SensorQuality>(
                        record.GetValueByKey("quality")?.ToString(), out var q) ? q : SensorQuality.GOOD
                });
            }
        }
        return results;
    }
}
