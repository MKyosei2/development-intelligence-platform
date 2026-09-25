// Purpose: Implements Fleet Worker endpoint-agent responsibilities for fleet worker.
using Dip.Agent.Core;
namespace Dip.Agent.Windows;
public sealed class FleetWorker(AgentOptions options,ILogger<FleetWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var client=new EnterpriseAgentClient(options);var version=typeof(FleetWorker).Assembly.GetName().Version?.ToString()??"0.0.0";
        while(!stoppingToken.IsCancellationRequested)
        {
            try{var health=$"{{\"status\":\"healthy\",\"machine\":\"{Environment.MachineName.GetHashCode():X8}\"}}";
                await client.HeartbeatAsync(version,0,health,stoppingToken);
                var update=await client.GetUpdateAsync("Stable",stoppingToken);
                if(update?.UpdateAvailable==true)logger.LogInformation("DIP Agent update {Version} is available.",update.Version);
                }
            catch(Exception ex) when(ex is not OperationCanceledException){logger.LogWarning(ex,"Fleet heartbeat/update check failed; local collection continues and will retry.");}
            await Task.Delay(TimeSpan.FromMinutes(1),stoppingToken);
        }
    }
}
