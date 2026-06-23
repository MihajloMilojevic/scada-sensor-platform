using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SensorManagementService.Data;
using SensorManagementService.Services;

namespace SensorManagementService.Hubs;

/// <summary>
/// Hub ka kome se senzori konektuju da prime komande.
/// Podržava READY handshake — senzor pita da li treba da šalje,
/// SMS odlučuje na osnovu trenutnog broja aktivnih senzora.
/// </summary>
public class SensorCommandHub(
    IServiceScopeFactory scopeFactory,
    PoolManager poolManager,
    ILogger<SensorCommandHub> logger) : Hub
{
    private const int TargetActiveCount = 5;

    public override async Task OnConnectedAsync()
    {
        var sensorId = Context.GetHttpContext()?.Request.Query["sensorId"].ToString();
        if (!string.IsNullOrWhiteSpace(sensorId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, sensorId);
            logger.LogInformation("Sensor {SensorId} connected (connId={ConnId})",
                sensorId, Context.ConnectionId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var sensorId = Context.GetHttpContext()?.Request.Query["sensorId"].ToString();
        if (!string.IsNullOrWhiteSpace(sensorId))
            logger.LogInformation("Sensor {SensorId} disconnected", sensorId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Senzor poziva Ready() kad je spreman za rad (pri startu ili nakon blokade).
    /// SMS odlučuje: START ako fali aktivnih, STOP ako ih ima dovoljno.
    /// </summary>
    public async Task Ready(string sensorId)
    {
        logger.LogInformation("Sensor {SensorId} sent READY", sensorId);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SensorMgmtDbContext>();

        var sensor = await db.Sensors.FindAsync([sensorId]);
        if (sensor == null)
        {
            logger.LogWarning("READY from unknown sensor {SensorId}", sensorId);
            await Clients.Caller.SendAsync("Command", "STOP", 0);
            return;
        }

        await poolManager.HandleSensorReadyAsync(sensorId, db, CancellationToken.None);
    }
}
