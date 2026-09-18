// Purpose: Runs background processing for A ut om at ic Re gr es si on Tr ia ge Wo rk er.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Triage;
using Dip.Domain.Enums;
namespace Dip.Worker.Workers;

public sealed class AutomaticRegressionTriageWorker(IServiceScopeFactory scopes,IConfiguration configuration,ILogger<AutomaticRegressionTriageWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var interval=TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue<int?>("AutomaticTriage:IntervalSeconds")??30,10,600));
        while(!stop.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();var projects=scope.ServiceProvider.GetRequiredService<IProjectRepository>();var incidents=scope.ServiceProvider.GetRequiredService<IIncidentRepository>();var triages=scope.ServiceProvider.GetRequiredService<IRegressionTriageRepository>();var service=scope.ServiceProvider.GetRequiredService<RegressionTriageService>();
                foreach(var project in await projects.ListAsync(stop))
                    foreach(var incident in await incidents.ListRecentAsync(project.Id,100,stop))
                    {
                        if(incident.Status==IncidentStatus.Resolved||incident.Severity==Severity.Info)continue;if(await triages.GetLatestForIncidentAsync(incident.Id,stop) is not null)continue;await service.TriageAsync(new RegressionTriageRequest(project.Id,incident.Id),stop);logger.LogInformation("Automatically triaged incident {IncidentId}.",incident.Id);
                    }
            }
            catch(OperationCanceledException)when(stop.IsCancellationRequested){break;}catch(Exception ex){logger.LogError(ex,"Automatic regression triage cycle failed.");}
            await Task.Delay(interval,stop);
        }
    }
}
