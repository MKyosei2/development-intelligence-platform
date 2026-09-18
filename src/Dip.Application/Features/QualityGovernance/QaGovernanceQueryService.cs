// Purpose: Orchestrates the DIP workflow for Q aG ov er na nc eQ ue ry Se rv ic e.
using System.Text;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityGovernance;

/// <summary>Read model/DTO boundary for governance APIs. Domain entities do not escape directly to HTTP callers.</summary>
public sealed class QaGovernanceQueryService(QaGovernanceCoreEngine core,IQaGovernanceRepository repo,IAuditLogRepository audit,ISystemClock clock)
{
    public async Task<QaGovernanceOverviewDto> GetOverviewAsync(Guid projectId,CancellationToken ct)
    {
        var value=await core.GetOverviewAsync(projectId,ct);var overrides=await repo.ListReleaseOverridesAsync(projectId,50,ct);var now=clock.UtcNow;var active=overrides.FirstOrDefault(x=>x.IsEffective(now));
        return active is null?value:value with{GateVerdict="OverrideApproved",GateSummary=$"TEMPORARY GO override approved by {active.ApprovedBy}; expires {active.ExpiresAtUtc:O}. Original gate: {value.GateVerdict}. {value.GateSummary}"};
    }
    public async Task<PageResult<QaFindingStateDto>> FindFindingsAsync(Guid projectId,string? cursor,int pageSize,CancellationToken ct)
    {
        pageSize=Math.Clamp(pageSize,1,500);var decoded=DecodeCursor(cursor);var rows=await repo.ListFindingStatesPageAsync(projectId,decoded?.At,decoded?.Fingerprint,pageSize+1,ct);var page=rows.Take(pageSize).Select(Map).ToArray();var next=rows.Count>pageSize?EncodeCursor(rows[pageSize-1].LastSeenAtUtc,rows[pageSize-1].Fingerprint):null;return new(page,next,pageSize);
    }
    public async Task<IReadOnlyList<QaWaiverDto>> ListWaiversAsync(Guid p,CancellationToken ct){var now=clock.UtcNow;return(await repo.ListWaiversAsync(p,ct)).Select(x=>Map(x,now)).ToArray();}
    public async Task<IReadOnlyList<QaReliabilityDto>> ListReliabilityAsync(Guid p,int take,CancellationToken ct)=>(await repo.ListReliabilityAsync(p,take,ct)).Select(x=>new QaReliabilityDto(x.Id,x.ProjectId,x.TestKey,x.PassCount,x.FailCount,x.ConsecutivePasses,x.ConsecutiveFailures,x.Classification,x.LastCommitSha,x.FirstObservedAtUtc,x.LastObservedAtUtc,x.FailureRatePercent)).ToArray();
    public async Task<IReadOnlyList<QaBaselineDto>> ListBaselinesAsync(Guid p,int take,CancellationToken ct)=>(await repo.ListBaselinesAsync(p,take,ct)).Select(x=>new QaBaselineDto(x.Id,x.ProjectId,x.SubjectKey,x.MetricKey,x.BaselineCommit,x.LastKnownGoodCommit,x.FirstBadCommit,x.BaselineValue,x.CurrentValue,x.RegressionPercent,x.Status,x.UpdatedAtUtc)).ToArray();
    public async Task<IReadOnlyList<QaHealthDto>> ListHealthAsync(Guid p,int take,CancellationToken ct)=>(await repo.ListSystemHealthAsync(p,take,ct)).Select(x=>new QaHealthDto(x.Id,x.ProjectId,x.QaRunId,x.Component,x.Status,x.Detail,x.ObservedAtUtc)).ToArray();
    public async Task<IReadOnlyList<QaTrendDto>> ListTrendsAsync(Guid p,int take,CancellationToken ct)=>(await repo.ListTrendsAsync(p,take,ct)).Select(x=>new QaTrendDto(x.Id,x.ProjectId,x.QaRunId,x.OpenFindingCount,x.CriticalCount,x.WarningCount,x.FlakyCount,x.ActiveWaiverCount,x.TechnicalDebtScore,x.CapturedAtUtc)).ToArray();
    public async Task<IReadOnlyList<QaOwnershipRuleDto>> ListOwnershipAsync(Guid p,CancellationToken ct)=>(await repo.ListOwnershipRulesAsync(p,ct)).Select(x=>new QaOwnershipRuleDto(x.Id,x.ProjectId,x.Name,x.PathPattern,x.Team,x.Workstream,x.Priority,x.Enabled,x.CreatedAtUtc,x.UpdatedAtUtc)).ToArray();
    public async Task<IReadOnlyList<QaPolicyDto>> ListPoliciesAsync(Guid p,CancellationToken ct)=>(await repo.ListPoliciesAsync(p,ct)).Select(Map).ToArray();
    public async Task<QaPolicyDto?> GetActivePolicyAsync(Guid p,CancellationToken ct){var x=await repo.GetActivePolicyAsync(p,ct);return x is null?null:Map(x);}
    public async Task<IReadOnlyList<QaTestImpactRuleDto>> ListTestImpactAsync(Guid p,CancellationToken ct)=>(await repo.ListTestImpactRulesAsync(p,ct)).Select(x=>new QaTestImpactRuleDto(x.Id,x.ProjectId,x.PathPattern,x.TestKey,x.CommandJson,x.Priority,x.Enabled,x.CreatedAtUtc)).ToArray();
    public async Task<IReadOnlyList<QaRetentionRuleDto>> ListRetentionAsync(Guid p,CancellationToken ct)=>(await repo.ListRetentionRulesAsync(p,ct)).Select(x=>new QaRetentionRuleDto(x.Id,x.ProjectId,x.KindPattern,x.RetentionClass,x.RetentionDays,x.PreserveOnCritical,x.Enabled,x.CreatedAtUtc)).ToArray();
    public async Task<IReadOnlyList<QaMinimizationRecordDto>> ListMinimizationsAsync(Guid p,int take,CancellationToken ct)=>(await repo.ListMinimizationsAsync(p,take,ct)).Select(x=>new QaMinimizationRecordDto(x.Id,x.ProjectId,x.FindingId,x.OriginalUnits,x.MinimizedUnits,x.Status,x.Detail,x.CreatedAtUtc)).ToArray();
    public async Task<IReadOnlyList<QaReleaseGateOverrideDto>> ListOverridesAsync(Guid p,int take,CancellationToken ct){var now=clock.UtcNow;return(await repo.ListReleaseOverridesAsync(p,take,ct)).Select(x=>Map(x,now)).ToArray();}
    public async Task<IReadOnlyList<QaBackupVerificationDto>> ListBackupVerificationsAsync(Guid p,int take,CancellationToken ct)=>(await repo.ListBackupVerificationsAsync(p,take,ct)).Select(x=>new QaBackupVerificationDto(x.Id,x.ProjectId,x.BackupKind,x.BackupReference,x.SourceCommit,x.EnvironmentFingerprint,x.Status,x.Detail,x.VerifiedAtUtc)).ToArray();
    public async Task<IReadOnlyList<GovernanceAuditLogDto>> ListAuditAsync(Guid p,int take,CancellationToken ct)=>(await audit.ListByProjectAsync(p,take,ct)).Select(x=>new GovernanceAuditLogDto(x.Id,x.ProjectId,x.Category,x.Action,x.Actor,x.SubjectKey,x.DetailJson,x.RequestId,x.CorrelationId,x.Reason,x.CreatedAtUtc)).ToArray();
    public Task<IReadOnlyList<SmartTestSelectionDto>> SelectTestsAsync(SmartTestSelectionRequest r,CancellationToken ct)=>core.SelectTestsAsync(r,ct);
    public Task<IReadOnlyList<TestRecommendationDto>> RecommendTestsAsync(Guid p,IReadOnlyList<string> paths,CancellationToken ct)=>core.RecommendTestsAsync(p,paths,ct);
    public Task<RootCauseCorrelationDto> CorrelateAsync(Guid p,string fingerprint,CancellationToken ct)=>core.CorrelateAsync(p,fingerprint,ct);
    public Task<QaFreshnessDto> GetFreshnessAsync(Guid p,int days,CancellationToken ct)=>core.GetFreshnessAsync(p,days,ct);
    public Task<QaCostSummaryDto> GetCostSummaryAsync(Guid p,int take,CancellationToken ct)=>core.GetCostSummaryAsync(p,take,ct);
    public Task<CrossProjectBenchmarkComparisonDto> CompareProjectsAsync(string d,string m,string a,int t,CancellationToken ct)=>core.CompareProjectsAsync(d,m,a,t,ct);
    public static DiagnosticSchemaDto GetDiagnosticSchema()=>QaGovernanceCoreEngine.GetDiagnosticSchema();
    private static QaFindingStateDto Map(QaFindingState x)=>new(x.Id,x.ProjectId,x.Fingerprint,x.LastFindingId,x.SubjectKey,x.LastCommitSha,x.Status,x.Occurrences,x.Team,x.Workstream,x.OwnershipSource,x.DispositionReason,x.UpdatedBy,x.FirstSeenAtUtc,x.LastSeenAtUtc,x.UpdatedAtUtc,x.Version);
    private static QaWaiverDto Map(QaWaiver x,DateTimeOffset now)=>new(x.Id,x.ProjectId,x.Name,x.Reason,x.RequestedBy,x.ApprovedBy,x.DecisionReason,x.Fingerprint,x.RuleId,x.PathPattern,x.Status==WaiverStatus.Active&&x.ExpiresAtUtc<=now?WaiverStatus.Expired:x.Status,x.CreatedAtUtc,x.UpdatedAtUtc,x.ExpiresAtUtc,x.ApprovedAtUtc,x.RevokedAtUtc,x.Version);
    private static QaPolicyDto Map(QaPolicy x)=>new(x.Id,x.ProjectId,x.Name,x.Version,x.RulesJson,x.LifecycleStatus,x.Revision,x.SupersedesPolicyId,x.ApprovedBy,x.ActivatedBy,x.CreatedAtUtc,x.UpdatedAtUtc,x.ApprovedAtUtc,x.ActivatedAtUtc,x.ConcurrencyVersion);
    private static QaReleaseGateOverrideDto Map(QaReleaseGateOverride x,DateTimeOffset now)=>new(x.Id,x.ProjectId,x.PolicyEvaluationId,x.QaRunId,x.CommitSha,x.Reason,x.RequestedBy,x.Status==QaReleaseOverrideStatus.Approved&&x.ExpiresAtUtc<=now?QaReleaseOverrideStatus.Expired:x.Status,x.ApprovedBy,x.DecisionReason,x.CreatedAtUtc,x.UpdatedAtUtc,x.ExpiresAtUtc,x.DecidedAtUtc,x.Version);
    private static string EncodeCursor(DateTimeOffset at,string fingerprint)=>Convert.ToBase64String(Encoding.UTF8.GetBytes($"{at.UtcDateTime.Ticks}|{Convert.ToBase64String(Encoding.UTF8.GetBytes(fingerprint))}"));
    private static (DateTimeOffset At,string Fingerprint)? DecodeCursor(string? cursor){if(string.IsNullOrWhiteSpace(cursor))return null;try{var raw=Encoding.UTF8.GetString(Convert.FromBase64String(cursor));var split=raw.IndexOf('|');if(split<=0||!long.TryParse(raw[..split],out var ticks))throw new FormatException();var fingerprint=Encoding.UTF8.GetString(Convert.FromBase64String(raw[(split+1)..]));if(string.IsNullOrWhiteSpace(fingerprint))throw new FormatException();return(new DateTimeOffset(ticks,TimeSpan.Zero),fingerprint);}catch(Exception ex)when(ex is FormatException or ArgumentException){throw new ArgumentException("Invalid pagination cursor.",nameof(cursor));}}
}
