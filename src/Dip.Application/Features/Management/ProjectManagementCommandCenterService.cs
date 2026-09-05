using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.QualityAutomation;

namespace Dip.Application.Features.Management;

public sealed record PmExecutionKpisDto(
    int Workstreams,
    int ActiveTasks,
    int CriticalPathItems,
    int AtRiskScheduleItems,
    int OpenBlockers,
    int CriticalBlockers,
    int OpenRisks,
    int CriticalRisks,
    int PendingDecisions,
    int OverdueActions,
    int PendingApprovals,
    int OpenScopeChanges,
    int MissingAccountableAssignments,
    int QaP0Blockers);

public sealed record PmScheduleControlDto(
    string Status,
    DateTimeOffset? PlannedFinishUtc,
    DateTimeOffset? ForecastFinishUtc,
    int DelayDays,
    int CriticalItems,
    int AtRiskItems,
    IReadOnlyList<string> CriticalPath,
    IReadOnlyList<string> Warnings);

public sealed record PmProgressControlDto(
    decimal MilestoneProgressPercent,
    int Milestones,
    int CompletedMilestones,
    int AtRiskMilestones,
    int BlockedMilestones,
    decimal ScopeGrowthPercent,
    int IncompleteHighPriorityRequirements);

public sealed record PmWorkstreamControlDto(
    string Key,
    string Name,
    string Team,
    decimal UtilizationPercent,
    decimal PlannedHours,
    decimal RemainingHours,
    int ActiveTasks,
    int BlockedTasks,
    string Status);

public sealed record PmControlCoverageDto(
    bool PlanAndTasks,
    bool ScheduleAndCriticalPath,
    bool Milestones,
    bool Dependencies,
    bool Risks,
    bool DecisionsAndActions,
    bool ScopeAndChangeControl,
    bool CapacityAndResources,
    bool Raci,
    bool Approvals,
    bool BranchQuality,
    bool ReleaseQualityGate);

public sealed record ProjectManagementCommandCenterDto(
    Guid ProjectId,
    string ProjectName,
    string DefaultBranch,
    DateTimeOffset? ReleaseDateUtc,
    string OverallStatus,
    decimal HealthScore,
    PmExecutionKpisDto Kpis,
    PmScheduleControlDto Schedule,
    PmProgressControlDto Progress,
    IReadOnlyList<PmWorkstreamControlDto> Workstreams,
    IReadOnlyList<ManagementMilestoneDto> Milestones,
    IReadOnlyList<BlockerImpactDto> Blockers,
    IReadOnlyList<RiskRegisterItemDto> Risks,
    IReadOnlyList<ManagementDecisionDto> PendingDecisions,
    IReadOnlyList<ManagementActionDto> OpenActions,
    ScopeOverviewDto Scope,
    ScopeChangeOverviewDto ScopeChanges,
    DependencyOverviewDto Dependencies,
    ResourceAllocationOverviewDto Resources,
    ApprovalOverviewDto Approvals,
    RaciOverviewDto Raci,
    QaReleaseGateDto QualityGate,
    QaOperationsKpiDto QualityKpis,
    IReadOnlyList<CockpitDecisionDto> FocusNow,
    IReadOnlyList<string> RecommendedActions,
    PmControlCoverageDto ControlCoverage,
    DateTimeOffset GeneratedAtUtc);

/// <summary>
/// Commercial PM command read model. This does not invent a second project model;
/// it composes the existing WorkManagement, ManagementAnalytics, approval/RACI and
/// quality evidence so the PM can control delivery from one surface while edits
/// continue through the existing durable management commands.
/// </summary>
public sealed class ProjectManagementCommandCenterService
{
    private readonly IProjectRepository _projects;
    private readonly ManagementDashboardService _dashboard;
    private readonly ManagementOperationsService _operations;
    private readonly CommercialQualityOperationsService _quality;
    private readonly ISystemClock _clock;

    public ProjectManagementCommandCenterService(
        IProjectRepository projects,
        ManagementDashboardService dashboard,
        ManagementOperationsService operations,
        CommercialQualityOperationsService quality,
        ISystemClock clock)
    {
        _projects=projects;_dashboard=dashboard;_operations=operations;_quality=quality;_clock=clock;
    }

    public async Task<ProjectManagementCommandCenterDto> GetAsync(
        Guid projectId,
        int horizonWeeks,
        string? ownerRole,
        CancellationToken ct)
    {
        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        horizonWeeks=Math.Clamp(horizonWeeks,1,26);
        var project=await _projects.GetByIdAsync(projectId,ct)??throw new KeyNotFoundException($"Project {projectId} not found.");

        var dashboardTask=_dashboard.GetAsync(projectId,horizonWeeks,ct);
        var cockpitTask=_operations.GetDailyCockpitAsync(projectId,horizonWeeks,ownerRole,ct);
        var approvalTask=_operations.GetApprovalsAsync(projectId,ct);
        var raciTask=_operations.GetRaciAsync(projectId,ct);
        var qualityTask=_quality.GetAsync(projectId,60,ct);
        await Task.WhenAll(dashboardTask,cockpitTask,approvalTask,raciTask,qualityTask);

        var dashboard=await dashboardTask;
        var cockpit=await cockpitTask;
        var approvals=await approvalTask;
        var raci=await raciTask;
        var quality=await qualityTask;

        var milestones=dashboard.Milestones.Milestones
            .OrderBy(x=>x.State.Equals("Completed",StringComparison.OrdinalIgnoreCase)?1:0)
            .ThenBy(x=>x.DueAtUtc)
            .ToArray();
        var progress=milestones.Length==0
            ?0m
            :decimal.Round(milestones.Average(x=>Math.Clamp(x.LinkedTaskCompletionPercent>0m?x.LinkedTaskCompletionPercent:x.RecordedCompletionPercent,0m,100m)),1);
        var workstreams=dashboard.Capacity.Workstreams
            .OrderByDescending(x=>x.BlockedTasks)
            .ThenByDescending(x=>x.UtilizationPercent)
            .Select(x=>new PmWorkstreamControlDto(x.Key,x.Name,x.TeamName,x.UtilizationPercent,x.PlannedHours,x.RemainingHours,x.ActiveTasks,x.BlockedTasks,x.Status))
            .ToArray();
        var risks=dashboard.Risks.Items
            .Where(x=>!x.Status.Equals("Closed",StringComparison.OrdinalIgnoreCase)&&!x.Status.Equals("Resolved",StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x=>x.ExposureScore)
            .ThenBy(x=>x.ReviewDueUtc??DateTimeOffset.MaxValue)
            .ToArray();
        var blockers=dashboard.Blockers.Items
            .Where(x=>!x.Status.Equals("Resolved",StringComparison.OrdinalIgnoreCase)&&!x.Status.Equals("Closed",StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x=>SeverityRank(x.Severity))
            .ThenByDescending(x=>x.AgeDays)
            .ToArray();
        var pendingDecisions=dashboard.Decisions.Decisions
            .Where(x=>!x.Status.Equals("Decided",StringComparison.OrdinalIgnoreCase)&&!x.Status.Equals("Closed",StringComparison.OrdinalIgnoreCase))
            .OrderBy(x=>x.DecisionDueUtc??DateTimeOffset.MaxValue)
            .ToArray();
        var openActions=dashboard.Decisions.Actions
            .Where(x=>!x.Status.Equals("Done",StringComparison.OrdinalIgnoreCase)&&!x.Status.Equals("Closed",StringComparison.OrdinalIgnoreCase)&&!x.Status.Equals("Completed",StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x=>x.Overdue)
            .ThenBy(x=>x.DueAtUtc??DateTimeOffset.MaxValue)
            .ToArray();

        var activeTasks=dashboard.Schedule.Items.Count(x=>!x.Status.Equals("Done",StringComparison.OrdinalIgnoreCase)&&!x.Status.Equals("Completed",StringComparison.OrdinalIgnoreCase));
        var kpis=new PmExecutionKpisDto(
            workstreams.Length,
            activeTasks,
            dashboard.Schedule.CriticalItems,
            dashboard.Schedule.AtRiskItems,
            dashboard.Blockers.Open,
            dashboard.Blockers.Critical,
            dashboard.Risks.Open,
            dashboard.Risks.Critical,
            dashboard.Decisions.DecisionsPending,
            dashboard.Decisions.OverdueActions,
            approvals.Pending,
            dashboard.ScopeChanges.Proposed,
            raci.MissingAccountable,
            quality.Kpis.P0Blockers);

        var schedule=new PmScheduleControlDto(
            dashboard.Schedule.Status,dashboard.Schedule.PlannedFinishUtc,dashboard.Schedule.ForecastFinishUtc,
            dashboard.Schedule.DelayDays,dashboard.Schedule.CriticalItems,dashboard.Schedule.AtRiskItems,
            dashboard.Schedule.CriticalPath,dashboard.Schedule.Warnings);
        var progressControl=new PmProgressControlDto(
            progress,dashboard.Milestones.Total,dashboard.Milestones.Completed,dashboard.Milestones.AtRisk,dashboard.Milestones.Blocked,
            dashboard.Scope.ScopeGrowthPercent,dashboard.Scope.IncompleteHighPriority);

        var recommended=new List<string>();
        recommended.AddRange(cockpit.Now.Take(5).Select(x=>$"{x.Title}: {x.Action}"));
        recommended.AddRange(dashboard.ActionsRequired.Take(6));
        if(quality.ReleaseGate.Status=="Blocked")recommended.Insert(0,$"Quality gate is blocked: {quality.ReleaseGate.NextAction}");
        if(dashboard.Dependencies.HasCycle)recommended.Insert(0,"Resolve the dependency cycle before trusting the current critical-path forecast.");
        if(approvals.Overdue>0)recommended.Insert(0,$"Resolve {approvals.Overdue} overdue approval(s).");
        recommended=recommended.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();

        var overall=DeriveOverallStatus(dashboard.OverallStatus,quality.ReleaseGate.Status,dashboard.Blockers.Critical,dashboard.Risks.Critical,dashboard.Schedule.DelayDays);
        var coverage=new PmControlCoverageDto(true,true,true,true,true,true,true,true,true,true,true,true);

        return new(
            projectId,project.Name,project.DefaultBranch,project.ReleaseDateUtc,overall,dashboard.HealthScore,kpis,schedule,progressControl,
            workstreams,milestones,blockers,risks,pendingDecisions,openActions,dashboard.Scope,dashboard.ScopeChanges,dashboard.Dependencies,
            dashboard.Resources,approvals,raci,quality.ReleaseGate,quality.Kpis,cockpit.Now,recommended,coverage,_clock.UtcNow);
    }

    private static string DeriveOverallStatus(string management,string quality,int criticalBlockers,int criticalRisks,int delayDays)
    {
        if(quality.Equals("Blocked",StringComparison.OrdinalIgnoreCase)||criticalBlockers>0)return "Critical";
        if(management.Equals("Critical",StringComparison.OrdinalIgnoreCase)||criticalRisks>0||delayDays>7)return "AtRisk";
        if(quality.Equals("Hold",StringComparison.OrdinalIgnoreCase)||delayDays>0)return "Attention";
        return management;
    }

    private static int SeverityRank(string severity)=>severity.ToLowerInvariant() switch{"critical" or "blocker"=>3,"high"=>2,"medium" or "warning"=>1,_=>0};
}
