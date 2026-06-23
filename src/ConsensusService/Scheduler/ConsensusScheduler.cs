using ConsensusService.Data;
using ConsensusService.Engine;
using ConsensusService.State;

namespace ConsensusService.Scheduler;

public class ConsensusScheduler(
    StateStoreManager stateManager,
    BftEngine engine,
    IServiceScopeFactory scopeFactory,
    ILogger<ConsensusScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var windowStart = DateTimeOffset.UtcNow;
        var intervalSeconds = 60;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (await timer.WaitForNextTickAsync(ct))
        {
            var windowEnd = DateTimeOffset.UtcNow;
            var snapshot = stateManager.SwapAndSnapshot();

            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ConsensusDbContext>();
                await engine.ProcessWindowAsync(snapshot, windowStart, windowEnd, db, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing consensus window");
            }

            windowStart = windowEnd;
        }
    }
}
