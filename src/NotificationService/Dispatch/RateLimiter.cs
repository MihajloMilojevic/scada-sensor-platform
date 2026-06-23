using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Hubs;
using Scada.Shared.Contracts;

namespace NotificationService.Dispatch;

public class RateLimiter(IHubContext<NotificationHub> hub) : BackgroundService
{
    private readonly ConcurrentQueue<object> _queue = new();

    public void Enqueue(SensorDataMessage msg)
        => _queue.Enqueue(new
        {
            sensorId     = msg.SensorId,
            value        = msg.Value,
            timestamp    = msg.Timestamp,
            alarmPriority = msg.AlarmPriority,
            quality      = msg.Quality.ToString()
        });

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(ct))
        {
            var batch = new List<object>();
            while (_queue.TryDequeue(out var item))
                batch.Add(item);

            if (batch.Count > 0)
                await hub.Clients.All.SendAsync("SensorReading", batch, ct);
        }
    }
}
