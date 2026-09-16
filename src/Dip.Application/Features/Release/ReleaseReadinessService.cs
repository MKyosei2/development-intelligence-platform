// Purpose: Coordinates R el ea se Re ad in es sS er vi ce application behavior for Release.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Release;
public sealed record EvaluateReleaseReadinessRequest(Guid ProjectId,string CommitSha,int EvidenceFreshnessDays=14);
public sealed record ReleaseReadinessDto(Guid Id,string CommitSha,string Decision,decimal Confidence,IReadOnlyList<string> Blockers,IReadOnlyList<string> Warnings,IReadOnlyList<string> RequiredActions,string Summary,DateTimeOffset EvaluatedAtUtc);

public sealed class ReleaseReadinessService
{
    private readonly IIncidentRepository _incidents;private readonly IDurableJobRepository _jobs;private readonly IPerformanceBudgetRepository _budgets;private readonly IProjectTruthRepository _truth;private readonly IEvidenceRepository _evidence;private readonly IChangeRiskRepository _risk;private readonly IReleaseEvidenceRepository _releaseEvidence;private readonly IReleaseReadinessRepository _repo;private readonly IDevelopmentGraphRepository _graph;private readonly IQaGovernanceRepository _qaGovernance;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public ReleaseReadinessService(IIncidentRepository incidents,IDurableJobRepository jobs,IPerformanceBudgetRepository budgets,IProjectTruthRepository truth,IEvidenceRepository evidence,IChangeRiskRepository risk,IReleaseEvidenceRepository releaseEvidence,IReleaseReadinessRepository repo,IDevelopmentGraphRepository graph,IQaGovernanceRepository qaGovernance,IUnitOfWork uow,ISystemClock clock){_incidents=incidents;_jobs=jobs;_budgets=budgets;_truth=truth;_evidence=evidence;_risk=risk;_releaseEvidence=releaseEvidence;_repo=repo;_graph=graph;_qaGovernance=qaGovernance;_uow=uow;_clock=clock;}
    public async Task<ReleaseReadinessDto> EvaluateAsync(EvaluateReleaseReadinessRequest r,CancellationToken ct)
    {
        var blockers=new List<string>();var warnings=new List<string>();var actions=new List<string>();var incidents=await _incidents.ListRecentAsync(r.ProjectId,200,ct);var openCritical=incidents.Where(x=>x.Status!=IncidentStatus.Resolved&&x.Severity==Severity.Critical).ToArray();if(openCritical.Length>0){blockers.Add($"{openCritical.Length} critical incident(s) remain unresolved.");actions.Add("Resolve or explicitly mitigate all critical incidents with fresh evidence.");}
        var jobs=await _jobs.ListRecentAsync(r.ProjectId,100,ct);var failedJobs=jobs.Count(x=>x.Status is DurableJobStatus.Failed or DurableJobStatus.TimedOut);var runningJobs=jobs.Count(x=>x.Status is DurableJobStatus.Running or DurableJobStatus.Queued);if(failedJobs>0){warnings.Add($"{failedJobs} recent durable job(s) failed or timed out.");actions.Add("Review failed durable jobs and rerun release-relevant checks.");}if(runningJobs>0)warnings.Add($"{runningJobs} durable job(s) are still queued/running.");
        var budgetEvaluations=await _budgets.ListEvaluationsAsync(r.ProjectId,50,ct);if(budgetEvaluations.Any(x=>x.Verdict==PerformanceBudgetVerdict.Failed)){blockers.Add("At least one recent platform performance budget evaluation failed.");actions.Add("Restore the failed platform performance budget and rerun the affected benchmark/replay.");}else if(budgetEvaluations.Any(x=>x.Verdict==PerformanceBudgetVerdict.Warning))warnings.Add("A recent performance budget is near its configured boundary.");
        var evidence=await _evidence.ListForProjectAsync(r.ProjectId,_clock.UtcNow.AddDays(-Math.Clamp(r.EvidenceFreshnessDays,1,90)),ct);if(evidence.Count==0){blockers.Add("No fresh release evidence exists in the configured freshness window.");actions.Add("Run the release QA/benchmark/replay suite to generate fresh evidence.");}
        var activeQaPolicy=await _qaGovernance.GetActivePolicyAsync(r.ProjectId,ct);var qaPolicyRules=ParseQaPolicyRules(activeQaPolicy);var qaGate=await _qaGovernance.GetLatestPolicyEvaluationAsync(r.ProjectId,ct);var gateOverrides=await _qaGovernance.ListReleaseOverridesAsync(r.ProjectId,100,ct);var effectiveOverride=qaGate is null?null:gateOverrides.FirstOrDefault(x=>x.IsEffective(_clock.UtcNow)&&x.PolicyEvaluationId==qaGate.Id&&string.Equals(x.CommitSha,r.CommitSha,StringComparison.OrdinalIgnoreCase));if(qaGate is null){warnings.Add("No QA governance policy evaluation exists for this project.");actions.Add("Run QA with the governance pipeline before release.");}else if(qaGate.Verdict==QaGateVerdict.NoGo&&effectiveOverride is null){blockers.Add($"QA policy gate is NO-GO: {qaGate.Summary}");actions.Add("Resolve the blockers or obtain an owner-approved, commit-bound, unexpired release override.");}else if(qaGate.Verdict==QaGateVerdict.NoGo&&effectiveOverride is not null){warnings.Add($"QA NO-GO is temporarily overridden by {effectiveOverride.ApprovedBy}; override expires {effectiveOverride.ExpiresAtUtc:O}. Original gate: {qaGate.Summary}");actions.Add("Review the accepted risk before the override expires and resolve the underlying QA blocker.");}else if(qaGate.Verdict==QaGateVerdict.ReviewRequired)warnings.Add($"QA policy gate requires review: {qaGate.Summary}");else if(qaGate.Verdict==QaGateVerdict.Unavailable){blockers.Add("QA policy gate is unavailable; release quality cannot be verified.");}
        var qaFreshnessDays=Math.Clamp(activeQaPolicy is null?r.EvidenceFreshnessDays:qaPolicyRules.MaxEvidenceAgeDays,1,90);var qaProvenance=await _qaGovernance.GetLatestProvenanceAsync(r.ProjectId,ct);if(qaProvenance is null){blockers.Add("No QA provenance exists for the release candidate.");actions.Add("Run QA on the candidate commit to capture provenance.");}else{var qaAge=(_clock.UtcNow-qaProvenance.CapturedAtUtc).TotalDays;if(qaAge>qaFreshnessDays){var message=$"Latest QA provenance is stale ({qaAge:0.#} days old; policy maximum {qaFreshnessDays} days).";if(activeQaPolicy is null||qaPolicyRules.BlockOnStaleEvidence)blockers.Add(message);else warnings.Add(message);actions.Add("Rerun QA on the release candidate to refresh exact-revision evidence.");}if(string.IsNullOrWhiteSpace(qaProvenance.CommitSha)){blockers.Add("Latest QA provenance does not identify an exact commit.");actions.Add("Run QA against an immutable release-candidate commit SHA.");}else if(!string.Equals(qaProvenance.CommitSha,r.CommitSha,StringComparison.OrdinalIgnoreCase)){blockers.Add("Latest QA provenance belongs to a different commit than the release candidate.");actions.Add("Run QA against the exact candidate commit.");}}
        var truth=await _truth.GetLatestAsync(r.ProjectId,ct);if(truth is null)warnings.Add("Project Truth has not been evaluated yet.");else{if(truth.BlockerCount>0){blockers.Add($"Project Truth reports {truth.BlockerCount} blocker(s).");actions.Add("Resolve Project Truth blockers before release.");}if(truth.UnverifiedClaimCount>0)warnings.Add($"{truth.UnverifiedClaimCount} project claim(s) remain unverified.");}
        var latestBundle=await _releaseEvidence.GetLatestAsync(r.ProjectId,ct);if(latestBundle is null||!string.Equals(latestBundle.CommitSha,r.CommitSha,StringComparison.OrdinalIgnoreCase)){warnings.Add("No release provenance/SBOM bundle is attached to this exact commit.");actions.Add("Generate a release evidence bundle for the candidate commit.");}
        var risk=await _risk.GetLatestAsync(r.ProjectId,r.CommitSha,ct);if(risk is not null&&risk.RiskLevel is ChangeRiskLevel.Critical){blockers.Add($"Change risk for {r.CommitSha} is Critical ({risk.RiskScore:0.#}/100).");actions.Add("Run the expanded high-risk QA plan and reassess change risk.");}else if(risk is not null&&risk.RiskLevel is ChangeRiskLevel.High)warnings.Add($"Change risk is High ({risk.RiskScore:0.#}/100).");
        var graphNodes=await _graph.ListNodesAsync(r.ProjectId,null,ct);
        var openManagementBlockers=graphNodes.Where(x=>x.Type==DevelopmentGraphNodeType.Blocker&&!IsResolvedManagementStatus(x.Status)).ToArray();
        var criticalManagementBlockers=openManagementBlockers.Count(IsCriticalManagementBlocker);
        if(criticalManagementBlockers>0){blockers.Add($"{criticalManagementBlockers} critical management blocker(s) remain open.");actions.Add("Resolve or explicitly mitigate critical schedule/workstream blockers before release.");}
        else if(openManagementBlockers.Length>0)warnings.Add($"{openManagementBlockers.Length} management blocker(s) remain open.");
        var incompleteHighPriorityRequirements=graphNodes.Count(x=>x.Type==DevelopmentGraphNodeType.Requirement&&IsHighPriorityRequirementOpen(x));
        if(incompleteHighPriorityRequirements>0){warnings.Add($"{incompleteHighPriorityRequirements} high-priority in-scope requirement(s) are incomplete.");actions.Add("Confirm incomplete high-priority requirements are either completed or explicitly removed/deferred from release scope.");}
        var openManagementRisks=graphNodes.Where(x=>x.Type==DevelopmentGraphNodeType.Risk&&!IsResolvedManagementStatus(x.Status)).ToArray();
        var criticalManagementRisks=openManagementRisks.Count(x=>ManagementRiskExposure(x)>=70m);
        var highManagementRisks=openManagementRisks.Count(x=>ManagementRiskExposure(x)>=45m&&ManagementRiskExposure(x)<70m);
        if(criticalManagementRisks>0){blockers.Add($"{criticalManagementRisks} critical management risk(s) remain open.");actions.Add("Review mitigation, ownership, and release impact for every critical management risk.");}
        else if(highManagementRisks>0)warnings.Add($"{highManagementRisks} high management risk(s) remain open.");
        var overdueManagementActions=graphNodes.Count(x=>x.Type==DevelopmentGraphNodeType.ActionItem&&!IsResolvedManagementStatus(x.Status)&&IsPastDue(x,"DueAtUtc",_clock.UtcNow));
        if(overdueManagementActions>0){warnings.Add($"{overdueManagementActions} management action item(s) are overdue.");actions.Add("Escalate overdue release-relevant actions or explicitly re-plan their due dates.");}
        var unresolvedScopeChanges=graphNodes.Count(x=>x.Type==DevelopmentGraphNodeType.ChangeRequest&&!IsFinalChangeStatus(x.Status)&&IsMaterialScopeChange(x));
        if(unresolvedScopeChanges>0){warnings.Add($"{unresolvedScopeChanges} material scope change request(s) are unresolved.");actions.Add("Approve, reject, or defer material scope changes before committing the final release scope/date.");}
        var blockedMilestones=graphNodes.Count(x=>x.Type==DevelopmentGraphNodeType.Milestone&&IsBlockingStatus(x.Status));
        if(blockedMilestones>0){blockers.Add($"{blockedMilestones} milestone(s) are blocked/at risk on the Development Graph.");actions.Add("Resolve critical-path milestone blockers or explicitly rescope the release.");}
        var notReadyFeatures=graphNodes.Count(x=>x.Type==DevelopmentGraphNodeType.Feature&&IsBlockingStatus(x.Status));
        if(notReadyFeatures>0){blockers.Add($"{notReadyFeatures} feature(s) are not release-ready.");actions.Add("Complete required feature acceptance criteria or remove the features from release scope.");}
        var failedCriteria=graphNodes.Count(x=>x.Type==DevelopmentGraphNodeType.AcceptanceCriterion&&IsCriterionBlocking(x));
        if(failedCriteria>0){blockers.Add($"{failedCriteria} required acceptance criterion/criteria lack passing evidence.");actions.Add("Attach fresh evidence for all required acceptance criteria.");}
        var blockedReviews=graphNodes.Count(x=>x.Type==DevelopmentGraphNodeType.Review&&x.Status is "Blocked" or "HighRisk");
        if(blockedReviews>0){warnings.Add($"{blockedReviews} review assessment(s) require attention before release.");actions.Add("Resolve blocked/high-risk reviews and rerun change-impact analysis.");}
        var criticalAssets=graphNodes.Count(x=>x.Type==DevelopmentGraphNodeType.Asset&&x.Status=="Critical");
        if(criticalAssets>0){blockers.Add($"{criticalAssets} critical asset issue(s) remain open.");actions.Add("Resolve asset import/reference/platform compatibility blockers.");}
        var failedCi=graphNodes.Count(x=>x.Type==DevelopmentGraphNodeType.CiRun&&IsFailedStatus(x.Status));
        if(failedCi>0){blockers.Add($"{failedCi} observed CI run(s) are failed/error state.");actions.Add("Restore green CI for the candidate revision or document an explicit exception with evidence.");}
        var decision=blockers.Count>0?ReleaseDecision.NoGo:warnings.Count>0?ReleaseDecision.ConditionalGo:ReleaseDecision.Go;var confidence=Math.Clamp(45m+Math.Min(35m,evidence.Count*2m)+(truth is null?0m:10m)+(budgetEvaluations.Count>0?10m:0m),0m,100m);var summary=decision switch{ReleaseDecision.Go=>"GO: no evidence-backed release blocker is currently open.",ReleaseDecision.ConditionalGo=>"CONDITIONAL GO: no hard blocker is open, but warnings require explicit review.",_=>"NO-GO: one or more evidence-backed release blockers must be resolved."};var entity=new ReleaseReadinessSnapshot(Guid.NewGuid(),r.ProjectId,r.CommitSha,decision,confidence,JsonSerializer.Serialize(blockers),JsonSerializer.Serialize(warnings),JsonSerializer.Serialize(actions.Distinct()),summary,_clock.UtcNow);await _repo.AddAsync(entity,ct);await _uow.SaveChangesAsync(ct);return Map(entity);
    }
    public async Task<ReleaseReadinessDto?> GetLatestAsync(Guid projectId,CancellationToken ct){var x=await _repo.GetLatestAsync(projectId,ct);return x is null?null:Map(x);}

    private static bool IsResolvedManagementStatus(string status)=>status.Equals("Resolved",StringComparison.OrdinalIgnoreCase)||status.Equals("Closed",StringComparison.OrdinalIgnoreCase)||status.Equals("Done",StringComparison.OrdinalIgnoreCase)||status.Equals("Completed",StringComparison.OrdinalIgnoreCase);
    private static bool IsCriticalManagementBlocker(DevelopmentGraphNode node)
    {
        try{using var d=JsonDocument.Parse(node.MetadataJson);return d.RootElement.TryGetProperty("Severity",out var s)&&string.Equals(s.GetString(),"Critical",StringComparison.OrdinalIgnoreCase);}catch{return node.Status.Equals("Critical",StringComparison.OrdinalIgnoreCase);}
    }
    private static bool IsHighPriorityRequirementOpen(DevelopmentGraphNode node)
    {
        if(IsResolvedManagementStatus(node.Status))return false;try{using var d=JsonDocument.Parse(node.MetadataJson);var r=d.RootElement;var priority=r.TryGetProperty("Priority",out var p)?p.GetString():"Medium";var scope=r.TryGetProperty("ScopeState",out var sc)?sc.GetString():"Included";return (priority is "Critical" or "High")&&scope is not ("Deferred" or "Removed");}catch{return false;}
    }
    private static decimal ManagementRiskExposure(DevelopmentGraphNode node)
    {
        try{using var d=JsonDocument.Parse(node.MetadataJson);var root=d.RootElement;var p=root.TryGetProperty("ProbabilityPercent",out var pe)&&pe.TryGetDecimal(out var pv)?pv:0m;var i=root.TryGetProperty("ImpactPercent",out var ie)&&ie.TryGetDecimal(out var iv)?iv:0m;return Math.Clamp(p,0m,100m)*Math.Clamp(i,0m,100m)/100m;}catch{return 0m;}
    }
    private static bool IsPastDue(DevelopmentGraphNode node,string property,DateTimeOffset now)
    {
        try{using var d=JsonDocument.Parse(node.MetadataJson);return d.RootElement.TryGetProperty(property,out var v)&&v.TryGetDateTimeOffset(out var due)&&due<now;}catch{return false;}
    }
    private static bool IsFinalChangeStatus(string status)=>status.Equals("Approved",StringComparison.OrdinalIgnoreCase)||status.Equals("Rejected",StringComparison.OrdinalIgnoreCase)||status.Equals("Cancelled",StringComparison.OrdinalIgnoreCase)||status.Equals("Closed",StringComparison.OrdinalIgnoreCase);
    private static bool IsMaterialScopeChange(DevelopmentGraphNode node)
    {
        try{using var d=JsonDocument.Parse(node.MetadataJson);var r=d.RootElement;var days=r.TryGetProperty("ScheduleDaysDelta",out var de)&&de.TryGetInt32(out var dv)?Math.Abs(dv):0;var risk=r.TryGetProperty("RiskDeltaPercent",out var re)&&re.TryGetDecimal(out var rv)?Math.Abs(rv):0m;var engineering=r.TryGetProperty("EngineeringHoursDelta",out var ee)&&ee.TryGetDecimal(out var ev)?Math.Abs(ev):0m;return days>0||risk>=10m||engineering>=40m;}catch{return false;}
    }
    private static bool IsBlockingStatus(string status)=>status is "Blocked" or "AtRisk" or "NotReady" or "Failed" or "Critical";
    private static bool IsFailedStatus(string status)=>status.Equals("Failed",StringComparison.OrdinalIgnoreCase)||status.Equals("Failure",StringComparison.OrdinalIgnoreCase)||status.Equals("Error",StringComparison.OrdinalIgnoreCase)||status.Equals("Cancelled",StringComparison.OrdinalIgnoreCase);
    private static bool IsCriterionBlocking(DevelopmentGraphNode node)
    {
        if(node.Status is not ("Failed" or "Unverified" or "MissingEvidence"))return false;
        try{using var d=JsonDocument.Parse(node.MetadataJson);return !d.RootElement.TryGetProperty("Required",out var required)||required.ValueKind!=JsonValueKind.False;}catch{return true;}
    }
    private static QaPolicyRules ParseQaPolicyRules(QaPolicy? policy){if(policy is null)return new();try{return JsonSerializer.Deserialize<QaPolicyRules>(policy.RulesJson)??new();}catch(JsonException){return new();}}
    private static ReleaseReadinessDto Map(ReleaseReadinessSnapshot x)=>new(x.Id,x.CommitSha,x.Decision.ToString(),x.Confidence,JsonSerializer.Deserialize<string[]>(x.BlockersJson)??[],JsonSerializer.Deserialize<string[]>(x.WarningsJson)??[],JsonSerializer.Deserialize<string[]>(x.RequiredActionsJson)??[],x.Summary,x.EvaluatedAtUtc);
}
