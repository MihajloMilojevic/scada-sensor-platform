using Microsoft.AspNetCore.SignalR;
using SensorManagementService.Hubs;

namespace SensorManagementService.Services;

/// <summary>
/// Servis kojeg injectuju PoolManager i SensorsController
/// da pošalju komande konkretnom senzoru kroz SignalR grupu.
/// </summary>
public class SensorCommandSender(IHubContext<SensorCommandHub> hub, ILogger<SensorCommandSender> logger)
{
    /// <summary>Senzor treba da nastavi/počne slanje.</summary>
    public async Task SendStartAsync(string sensorId)
    {
        logger.LogInformation("→ CMD START → {SensorId}", sensorId);
        await hub.Clients.Group(sensorId).SendAsync("Command", "START", 0);
    }

    /// <summary>Senzor treba da prestane slati (npr. failover — neko drugi ga zamjenjuje).</summary>
    public async Task SendStopAsync(string sensorId)
    {
        logger.LogInformation("→ CMD STOP → {SensorId}", sensorId);
        await hub.Clients.Group(sensorId).SendAsync("Command", "STOP", 0);
    }

    /// <summary>Senzor treba da pauzira tačno <paramref name="durationSeconds"/> sekundi.</summary>
    public async Task SendBlockAsync(string sensorId, int durationSeconds = 30)
    {
        logger.LogInformation("→ CMD BLOCK {Sec}s → {SensorId}", durationSeconds, sensorId);
        await hub.Clients.Group(sensorId).SendAsync("Command", "BLOCK", durationSeconds);
    }
}
