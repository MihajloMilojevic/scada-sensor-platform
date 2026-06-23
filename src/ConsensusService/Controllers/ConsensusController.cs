using ConsensusService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConsensusService.Controllers;

[ApiController]
[Route("api/consensus")]
[Authorize]
public class ConsensusController(ConsensusDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetResults(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var query = db.ConsensusResults.AsQueryable();
        if (from.HasValue) query = query.Where(r => r.WindowStart >= from.Value);
        if (to.HasValue)   query = query.Where(r => r.WindowEnd   <= to.Value);
        var results = await query.OrderByDescending(r => r.WindowStart).Take(100).ToListAsync(ct);
        return Ok(results);
    }

    [HttpGet("quality-changes")]
    public async Task<IActionResult> GetQualityChanges(CancellationToken ct)
    {
        var changes = await db.QualityChanges
            .OrderByDescending(q => q.ChangedAt)
            .Take(100)
            .ToListAsync(ct);
        return Ok(changes);
    }

    [HttpGet("/health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "ok" });
}
