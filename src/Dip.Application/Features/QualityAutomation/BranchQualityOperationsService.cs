// Purpose: Orchestrates the DIP workflow for B ra nc hQ ua li ty Op er at io ns Se rv ic e.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation;

public sealed record BranchQualityGateDto(string Status,string Label,string Reason,bool CanPromote,string NextAction);

public sealed record BranchQualityStateDto(
    string Branch,
    bool IsDefault,
    bool ExistsInWorkspace,
    bool RemoteTracked,
    string? CommitSha,
    int AheadOfDefault,
    int BehindDefault,
    string Source,
    string RefreshStatus,
    string Freshness,
    Guid? LatestQaRunId,
    string? LatestQaStatus,
    DateTimeOffset? LatestQaAtUtc,
    decimal? LatestQaAgeHours,
    int QaRuns,
    int OpenCriticalIssues,
    int OpenWarningIssues,
    BranchQualityGateDto Gate,
    string? Warning);

public sealed record BranchQualityOverviewDto(
    Guid ProjectId,
    string DefaultBranch,
    string RepositoryRefreshStatus,
    int Branches,
    int VerifiedBranches,
    int ReadyBranches,
    int BlockedBranches,
    IReadOnlyList<BranchQualityStateDto> Items,
    string? Warning,
    DateTimeOffset GeneratedAtUtc);

/// <summary>
/// Branch-level QA read model. It intentionally uses persisted PipelineRun.Branch
/// as the durable branch/evidence boundary, and augments it with the local Git
/// branch catalog so unverified branches are visible rather than silently absent.
/// </summary>
public sealed class BranchQualityOperationsService
{
    private readonly IProjectRepository _projects;
    private readonly IPipelineRunRepository _pipelines;
    private readonly IQaRunRepository _qaRuns;
    private readonly IQaWorkspaceResolver _workspace;
    private readonly IBranchQaWorkspaceManager _branches;
    private readonly ISystemClock _clock;
    private readonly CommercialQaServiceLevelPolicy _policy;

    public BranchQualityOperationsService(
        IProjectRepository projects,
        IPipelineRunRepository pipelines,
        IQaRunRepository qaRuns,
        IQaWorkspaceResolver workspace,
        IBranchQaWorkspaceManager branches,
        ISystemClock clock,
        CommercialQaServiceLevelPolicy? policy=null)
    {
        _projects=projects;_pipelines=pipelines;_qaRuns=qaRuns;_workspace=workspace;_branches=branches;_clock=clock;
        _policy=policy??CommercialQaServiceLevelPolicy.Default;
    }

    public async Task<BranchQualityOverviewDto> GetAsync(
        Guid projectId,
        string workspaceRelativePath,
        bool refreshRemote,
        CancellationToken ct)
    {
        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        var project=await _projects.GetByIdAsync(projectId,ct)??throw new KeyNotFoundException($"Project {projectId} not found.");
        var now=_clock.UtcNow;
        BranchRepositoryCatalogDto? git=null;string? warning=null;
        try
        {
            var full=_workspace.Resolve(workspaceRelativePath);
            git=await _branches.ListBranchesAsync(full,project.DefaultBranch,refreshRemote,ct);
            warning=git.Warning;
        }
        catch(Exception ex) when(ex is not OperationCanceledException)
        {
            warning=$"Git branch catalog is unavailable: {ex.Message}";
        }

        var allPipelines=await _pipelines.ListByProjectAsync(projectId,500,ct);
        var qaPipelines=allPipelines
            .Where(x=>x.CollectorType.Equals("DIP-QA",StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x=>x.FinishedAtUtc??x.QueuedAtUtc)
            .ToArray();
        var qaRunRows=await _qaRuns.ListRecentAsync(projectId,500,ct);
        var qaRunsById=qaRunRows.ToDictionary(x=>x.Id);

        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase){project.DefaultBranch};
        foreach(var branch in git?.Branches??[])names.Add(branch.Branch);
        foreach(var run in qaPipelines.Where(x=>!x.Branch.Equals("qa-workspace",StringComparison.OrdinalIgnoreCase)))names.Add(run.Branch);

        var items=new List<BranchQualityStateDto>();
        foreach(var name in names.OrderBy(x=>x.Equals(project.DefaultBranch,StringComparison.OrdinalIgnoreCase)?0:1).ThenBy(x=>x,StringComparer.OrdinalIgnoreCase))
        {
            var gitRef=git?.Branches.FirstOrDefault(x=>x.Branch.Equals(name,StringComparison.OrdinalIgnoreCase));
            var runs=qaPipelines.Where(x=>x.Branch.Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();
            var latest=runs.FirstOrDefault();
            var latestQa=latest is not null&&qaRunsById.TryGetValue(latest.Id,out var matchedQa)?matchedQa:null;
            var critical=latestQa?.CriticalFindingCount??0;
            var warnings=latestQa?.WarningFindingCount??0;
            var at=latest?.FinishedAtUtc??latest?.QueuedAtUtc;
            decimal? age=at.HasValue?decimal.Round((decimal)Math.Max(0,(now-at.Value).TotalHours),1):null;
            var freshness=age is null?"Unverified":age<=6m?"Fresh":age<=_policy.FreshVerificationHours?"Current":"Stale";
            var status=latest?.Status.ToString();
            var gate=EvaluateGate(status,age,critical,warnings,_policy.FreshVerificationHours);
            items.Add(new(
                name,
                name.Equals(project.DefaultBranch,StringComparison.OrdinalIgnoreCase),
                gitRef is not null,
                gitRef?.RemoteTracked??false,
                gitRef?.CommitSha??(latest is null?null:latest.CommitSha.Value),
                gitRef?.AheadOfDefault??0,
                gitRef?.BehindDefault??0,
                gitRef?.Source??(latest is null?"unknown":"history"),
                gitRef?.RefreshStatus??(git?.RefreshStatus??"Unavailable"),
                freshness,
                latest?.Id,
                status,
                at,
                age,
                runs.Length,
                critical,
                warnings,
                gate,
                gitRef?.Warning));
        }

        return new(
            projectId,
            project.DefaultBranch,
            git?.RefreshStatus??"Unavailable",
            items.Count,
            items.Count(x=>x.LatestQaRunId.HasValue),
            items.Count(x=>x.Gate.Status=="Ready"),
            items.Count(x=>x.Gate.Status=="Blocked"),
            items,
            warning,
            now);
    }

    public static BranchQualityGateDto EvaluateGate(string? latestStatus,decimal? age,int critical,int warnings,decimal freshVerificationHours=24m)
    {
        if(string.IsNullOrWhiteSpace(latestStatus)||age is null)
            return new("Unknown","QA required","No branch-scoped QA evidence exists for this branch.",false,"Run branch QA before merge or promotion.");
        if(latestStatus.Equals("Failed",StringComparison.OrdinalIgnoreCase)||latestStatus.Equals("TimedOut",StringComparison.OrdinalIgnoreCase)||latestStatus.Equals("Cancelled",StringComparison.OrdinalIgnoreCase))
            return new("Blocked","Blocked",$"Latest branch QA is {latestStatus}.",false,"Resolve the failed QA run and rerun the same branch revision.");
        if(critical>0)
            return new("Blocked","Blocked",$"Latest branch QA contains {critical} critical finding(s).",false,"Fix or formally disposition every critical issue before promotion.");
        if(age>freshVerificationHours)
            return new("Hold","Stale",$"Latest branch QA is {age:0.#} hours old; release evidence must be within {freshVerificationHours:0.#} hours.",false,"Rerun QA on the current branch head.");
        if(latestStatus.Equals("PartiallySucceeded",StringComparison.OrdinalIgnoreCase)||warnings>0)
            return new("Hold","Review",$"Latest branch QA contains {warnings} warning finding(s) or partial verification.",false,"Triage warnings and record an explicit decision before merge.");
        if(latestStatus.Equals("Succeeded",StringComparison.OrdinalIgnoreCase))
            return new("Ready","Verified","Current branch head has fresh passing QA evidence and no open critical/warning issues linked to its QA runs.",true,"Proceed to review/approval while preserving this revision SHA.");
        return new("Hold","Review",$"Latest branch QA state is {latestStatus}.",false,"Review the branch QA evidence before promotion.");
    }
}
