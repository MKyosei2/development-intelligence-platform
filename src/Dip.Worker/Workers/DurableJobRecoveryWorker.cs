// Purpose: Runs Durable Job Recovery background processing for DIP.
using Dip.Application.Abstractions.Observability;
using Dip.Application.Features.Jobs;
using Microsoft.Extensions.Options;
namespace Dip.Worker.Workers;
public sealed class DurableJobRecoveryWorker(IServiceScopeFactory scopes, IOptions<DurableJobQueueOptions> options, IDipOperationalMetrics metrics, ILogger<DurableJobRecoveryWorker> logger) : BackgroundService
{
    private readonly DurableJobQueueOptions _o = options.Value;
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<DurableJobService>();
                var count = await service.RecoverExpiredAsync(100, stop);
                if (count > 0)
                {
                    for (var i = 0; i < count; i++) metrics.LeaseRecovered();
                    logger.LogWarning("Recovered {Count} durable job(s) with expired worker leases.", count);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Durable job lease recovery failed.");
            }
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_o.RecoveryIntervalSeconds, 5, 300)), stop);
        }
    }
}
