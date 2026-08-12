using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Management;

namespace Dip.Application.Features.Dashboard;

public sealed class ManagementDashboardService
{
    private readonly ManagementAnalyticsService _analytics;private readonly ISystemClock _clock;
    public ManagementDashboardService(ManagementAnalyticsService analytics,ISystemClock clock){_analytics=analytics;_clock=clock;}

    public async Task<ManagementDashboardDto> GetAsync(Guid projectId,int horizonWeeks,CancellationToken ct)
    {
        var capacity=await _analytics.GetCapacityAsync(projectId,horizonWeeks,ct);
        var schedule=await _analytics.GetScheduleAsync(projectId,ct);
        var blockers=await _analytics.GetBlockersAsync(projectId,ct);
        var dependencies=await _analytics.GetDependenciesAsync(projectId,ct);
        var milestones=await _analytics.GetMilestonesAsync(projectId,ct);
        var scope=await _analytics.GetScopeAsync(projectId,ct);
        var dims=new List<ManagementHealthDimension>();
        var scheduleScore=schedule.Status switch{"OnTrack"=>100m,"AtRisk"=>65m,"Blocked"=>35m,"CycleDetected"=>10m,_=>70m};
        dims.Add(new("Schedule",ScoreStatus(scheduleScore),scheduleScore,schedule.Status=="OnTrack"?"Current task/milestone forecast is on track.":$"Schedule is {schedule.Status}; forecast delay is {schedule.DelayDays} day(s)."));
        var maxUtil=capacity.Teams.Count==0?0m:capacity.Teams.Max(x=>x.UtilizationPercent);var capScore=maxUtil<=90m?100m:maxUtil<=100m?80m:maxUtil<=110m?60m:35m;if(capacity.Teams.Any(x=>x.Status=="Blocked"))capScore=Math.Min(capScore,45m);
        dims.Add(new("Capacity",ScoreStatus(capScore),capScore,capacity.Teams.Count==0?"No team capacity data is registered.":$"Peak team utilization is {maxUtil:0.#}%; {capacity.Bottlenecks.Count} bottleneck(s) identified."));
        var blockerScore=blockers.Open==0?100m:Math.Max(15m,100m-blockers.Critical*30m-(blockers.Open-blockers.Critical)*8m-blockers.AgingOverFiveDays*5m);
        dims.Add(new("Blockers",ScoreStatus(blockerScore),blockerScore,$"{blockers.Open} open blocker(s), {blockers.Critical} critical, {blockers.AgingOverFiveDays} open for 5+ days."));
        var depScore=dependencies.HasCycle?15m:dependencies.CriticalLinks>0?Math.Max(55m,100m-dependencies.CriticalLinks*4m):100m;
        dims.Add(new("Dependencies",ScoreStatus(depScore),depScore,dependencies.HasCycle?"A dependency cycle prevents reliable scheduling.":$"{dependencies.CriticalLinks} critical and {dependencies.CrossWorkstreamLinks} cross-workstream dependency link(s)."));
        var scopeScore=scope.ScopeGrowthPercent<=5m?100m:scope.ScopeGrowthPercent<=10m?85m:scope.ScopeGrowthPercent<=20m?65m:40m;if(scope.IncompleteHighPriority>0)scopeScore=Math.Min(scopeScore,70m);
        dims.Add(new("Scope",ScoreStatus(scopeScore),scopeScore,$"Scope growth is {scope.ScopeGrowthPercent:0.#}% from baseline; {scope.IncompleteHighPriority} high-priority requirement(s) incomplete."));
        var milestoneScore=milestones.Total==0?75m:milestones.Blocked>0?Math.Max(25m,75m-milestones.Blocked*15m):milestones.AtRisk>0?Math.Max(55m,90m-milestones.AtRisk*8m):100m;
        dims.Add(new("Milestones",ScoreStatus(milestoneScore),milestoneScore,$"{milestones.Completed}/{milestones.Total} completed; {milestones.AtRisk} at risk; {milestones.Blocked} blocked."));
        var health=Math.Round(dims.Average(x=>x.Score),1,MidpointRounding.AwayFromZero);var overall=ScoreStatus(health);
        var actions=new List<string>();
        actions.AddRange(capacity.Bottlenecks.Take(5));
        actions.AddRange(blockers.Items.Where(x=>!WorkManagementService.IsDone(x.Status)).OrderByDescending(x=>x.Severity=="Critical").ThenByDescending(x=>x.AgeDays).Take(5).Select(x=>x.ManagementAction+" — "+x.Title));
        if(schedule.Status is "AtRisk" or "Blocked")actions.Add($"Review the critical path: {string.Join(" → ",schedule.CriticalPath.Take(8))}. Current forecast delay: {schedule.DelayDays} day(s).");
        if(dependencies.HasCycle)actions.Add("Remove the management dependency cycle before relying on schedule forecasts.");
        actions.AddRange(scope.Warnings.Take(4));
        return new(projectId,overall,health,dims,capacity,schedule,blockers,dependencies,milestones,scope,actions.Distinct().Take(15).ToArray(),_clock.UtcNow);
    }

    private static string ScoreStatus(decimal score)=>score>=85m?"Healthy":score>=70m?"Watch":score>=50m?"AtRisk":"Critical";
}
