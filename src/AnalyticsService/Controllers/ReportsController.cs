using AnalyticsService.Composition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AnalyticsService.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController(ReportComposer composer) : ControllerBase
{
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string? sensorId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct)
        => Ok(await composer.GetHistoryAsync(sensorId, from, to, ct));

    [HttpGet("consensus")]
    public async Task<IActionResult> GetConsensus(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct)
        => Ok(await composer.GetConsensusAsync(from, to, ct));

    [HttpGet("quality-changes")]
    public async Task<IActionResult> GetQualityChanges(CancellationToken ct)
        => Ok(await composer.GetQualityChangesAsync(ct));

    [HttpGet("sensors")]
    public async Task<IActionResult> GetSensors(CancellationToken ct)
        => Ok(await composer.GetSensorsAsync(ct));

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
        => Ok(await composer.GetSummaryAsync(ct));

    [HttpGet("/health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "ok" });
}
