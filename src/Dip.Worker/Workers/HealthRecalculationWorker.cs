// Purpose: Runs Health Recalculation Worker background processing for the DIP worker runtime.
namespace Dip.Worker.Workers;
public sealed class HealthRecalculationWorker : BackgroundService
{
    private readonly ILogger<HealthRecalculationWorker> _log;
    public HealthRecalculationWorker(ILogger<HealthRecalculationWorker> l) => _log = l;
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _log.LogInformation("Health worker heartbeat at {UtcNow}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }
}
