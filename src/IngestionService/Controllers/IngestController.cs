using System;
using System.Threading.Tasks;
using IngestionService.InfluxDb;
using IngestionService.Models;
using IngestionService.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IngestionService.Controllers;

[ApiController]
[Route("api/ingest")]
public class IngestController(
    MessageValidator validator,
    WriteAheadLog wal,
    RotationManager rotation,
    InfluxWriter influx,
    ILogger<IngestController> logger) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "IngestWrite")]
    public async Task<IActionResult> Ingest([FromBody] IngestRequest request)
    {
        var sub = User.FindFirst("sub")?.Value ?? "";
        var (valid, error, message) = validator.Validate(request, sub);

        if (!valid)
        {
            logger.LogWarning("Rejected ingest from {SensorId}: {Error}", request.SensorId, error);
            return error?.Contains("Replay") == true || error?.Contains("stale") == true
                ? BadRequest(new { error })
                : Unauthorized(new { error });
        }

        // WAL write first (durability), then add to batch
        await wal.AppendAsync(message!);
        rotation.Add(message!);

        logger.LogInformation("Accepted reading from {SensorId} msgId={MsgId}", request.SensorId, request.MessageId);
        return Accepted();
    }

    [HttpGet("measurements")]
    [Authorize]
    public async Task<IActionResult> GetMeasurements(
        [FromQuery] string? sensorId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var start = from ?? DateTimeOffset.UtcNow.AddHours(-1);
        var end = to ?? DateTimeOffset.UtcNow;
        var results = await influx.QueryAsync(sensorId, start, end);
        return Ok(results);
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "ok", service = "IngestionService" });
}