// Purpose: Runs A rt if ac tR et en ti on Wo rk er background processing.
using Dip.Application.Features.Artifacts;
using Dip.Infrastructure.Artifacts;
using Microsoft.Extensions.Options;

namespace Dip.Worker.Workers;

public sealed class ArtifactRetentionWorker(IServiceScopeFactory scopes,IOptions<ArtifactStoreOptions> options,ILogger<ArtifactRetentionWorker> logger):BackgroundService
{
    private readonly ArtifactStoreOptions _o=options.Value;
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while(!stop.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();var service=scope.ServiceProvider.GetRequiredService<ArtifactService>();
                var deleted=await service.CleanupExpiredAsync(DateTimeOffset.UtcNow,Math.Clamp(_o.RetentionBatchSize,1,1000),stop);
                if(deleted>0)logger.LogInformation("Deleted {Count} expired artifact record(s).",deleted);
            }
            catch(OperationCanceledException)when(stop.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogError(ex,"Artifact retention cleanup failed.");}
            await Task.Delay(TimeSpan.FromMinutes(Math.Clamp(_o.RetentionCleanupMinutes,5,1440)),stop);
        }
    }
}
