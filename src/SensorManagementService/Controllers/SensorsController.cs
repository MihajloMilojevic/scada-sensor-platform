using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SensorManagementService.Data;
using SensorManagementService.Services;

namespace SensorManagementService.Controllers;

[ApiController]
[Route("api/sensors")]
[Authorize]
public class SensorsController(
    SensorMgmtDbContext db,
    PoolManager poolManager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await db.Sensors.OrderBy(s => s.SensorId).ToListAsync(ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var sensor = await db.Sensors.FindAsync([id], ct);
        return sensor == null ? NotFound() : Ok(sensor);
    }

    [HttpPost("{id}/activate")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Activate(string id, CancellationToken ct)
    {
        try
        {
            await poolManager.ManualActivateAsync(id, db, ct);
            var sensor = await db.Sensors.FindAsync([id], ct);
            return Ok(new { message = $"{id} → {sensor?.Status}", status = sensor?.Status });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id}/deactivate")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Deactivate(string id, CancellationToken ct)
    {
        try
        {
            await poolManager.ManualDeactivateAsync(id, db, ct);
            return Ok(new { message = $"{id} deactivated — standby promoted automatically" });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id}/block")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Block(string id, CancellationToken ct)
    {
        var sensor = await db.Sensors.FindAsync([id], ct);
        if (sensor == null) return NotFound();

        const int blockSeconds = 30;
        await poolManager.BlockSensorAsync(id, blockSeconds, db, ct);

        return Ok(new { message = $"{id} blocked for {blockSeconds}s — will send READY when unblocked" });
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "ok", service = "SensorManagementService" });
}
