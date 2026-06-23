using Microsoft.AspNetCore.SignalR;
using NotificationService.Hubs;
using Scada.Shared.Contracts;

namespace NotificationService.Dispatch;

public class Dispatcher(IHubContext<NotificationHub> hub, RateLimiter rateLimiter)
{
    public Task HandleSensorData(SensorDataMessage msg, CancellationToken ct)
    {
        if (msg.AlarmPriority > 0)
        {
            return hub.Clients.All.SendAsync("Alarm", new
            {
                sensorId      = msg.SensorId,
                value         = msg.Value,
                timestamp     = msg.Timestamp,
                alarmPriority = msg.AlarmPriority
            }, ct);
        }

        rateLimiter.Enqueue(msg);
        return Task.CompletedTask;
    }

    public Task HandleStatusChange(SensorStatusMessage msg, CancellationToken ct)
        => hub.Clients.All.SendAsync("StatusChanged", new
        {
            sensorId       = msg.SensorId,
            status         = msg.Status.ToString(),
            previousStatus = msg.PreviousStatus.ToString(),
            reason         = msg.Reason,
            timestamp      = msg.Timestamp
        }, ct);

    public Task HandleQualityChange(ConsensusResultMessage msg, CancellationToken ct)
        => hub.Clients.All.SendAsync("QualityChanged", new
        {
            sensorId        = msg.SensorId,
            previousQuality = msg.PreviousQuality.ToString(),
            newQuality      = msg.NewQuality.ToString(),
            timestamp       = msg.Timestamp
        }, ct);
}
