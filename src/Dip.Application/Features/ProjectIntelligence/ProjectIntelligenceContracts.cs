// Purpose: Coordinates P ro je ct In te ll ig en ce Co nt ra ct s application behavior for ProjectIntelligence.
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectIntelligence;

public sealed record SourceProvenanceDto(
    string Provider,string SourceId,string Revision,string? Url,DateTimeOffset ObservedAtUtc,
    string Confidence,string RawSha256,string? Channel=null,string? Author=null);

public sealed record PlanColumnMappingDto(
    string SheetName,int HeaderRowIndex,int? WbsColumn,int? TitleColumn,int? ParentColumn,int? StartColumn,
    int? DueColumn,int? ProgressColumn,int? OwnerColumn,int? StatusColumn,int? DependencyColumn,int? MilestoneColumn,
    decimal Confidence,IReadOnlyList<string> Headers,IReadOnlyList<string> Notes);

public sealed record PlanItemCandidateDto(
    string Key,string Name,string? ParentKey,DateTimeOffset? PlannedStartUtc,DateTimeOffset? DueAtUtc,
    decimal? DeclaredProgressPercent,string? Owner,string? Status,IReadOnlyList<string> DependencyKeys,
    bool IsMilestone,string SheetName,int SourceRow,string SourceReference);

public sealed record PlanSheetAnalysisDto(string SheetName,PlanColumnMappingDto Mapping,int DataRows,IReadOnlyList<PlanItemCandidateDto> Preview);
public sealed record ProjectPlanAnalysisDto(
    string FileName,string SourceSha256,IReadOnlyList<PlanSheetAnalysisDto> Sheets,int CandidateItems,
    IReadOnlyList<string> Warnings,DateTimeOffset AnalyzedAtUtc);
public sealed record ProjectPlanImportResult(
    Guid ProjectId,string ImportKey,string FileName,string SourceSha256,int ImportedItems,int ParentLinks,int DependencyLinks,
    int AutoResolvedEntities,IReadOnlyList<string> Warnings,DateTimeOffset ImportedAtUtc);

public sealed record ProjectSourceProviderDto(string Provider,bool Configured);
public sealed record ProjectSourcePullCommand(Guid ProjectId,string Provider,bool DryRun=false);
public sealed record ProjectSourceObservation(
    string Provider,string ExternalId,string Kind,string Title,string? Body,string Status,decimal? ProgressPercent,
    string? Owner,DateTimeOffset? StartAtUtc,DateTimeOffset? DueAtUtc,DateTimeOffset UpdatedAtUtc,string Revision,
    string? Url,IReadOnlyList<string> References,IReadOnlyDictionary<string,string>? Fields=null);
public sealed record ProjectSourceSyncResult(
    Guid ProjectId,string Provider,int Observed,int AddedOrChanged,int Unchanged,int EvidenceRecords,int AutoResolved,
    bool DryRun,IReadOnlyList<string> Warnings,DateTimeOffset SyncedAtUtc);

public interface IProjectSourceAdapter
{
    string ProviderName { get; }
    bool IsConfigured { get; }
    bool IsConfiguredFor(Guid projectId);
    Task<IReadOnlyList<ProjectSourceObservation>> PullAsync(Guid projectId,CancellationToken ct);
}

public sealed record CollaborationEvidenceCommand(
    Guid ProjectId,string Provider,string ExternalId,string? Channel,string? Author,string Text,
    DateTimeOffset ObservedAtUtc,string? Url=null,IReadOnlyList<string>? References=null,string? Revision=null);
public sealed record CollaborationEvidenceResult(
    string ObservationKey,string SubjectKey,string Confidence,bool EvidenceBacked,IReadOnlyList<string> References,
    int AutoResolved,DateTimeOffset IngestedAtUtc);

public sealed record EntityMatchDto(
    string SourceNodeType,string SourceKey,string SourceName,string Provider,string? TargetNodeType,string? TargetKey,
    string? TargetName,decimal Confidence,string Resolution,string Reason,DateTimeOffset ObservedAtUtc);
public sealed record EntityResolutionOverviewDto(Guid ProjectId,int Resolved,int Suggested,int Unresolved,IReadOnlyList<EntityMatchDto> Items,DateTimeOffset GeneratedAtUtc);
public sealed record BindProjectEntityCommand(Guid ProjectId,string SourceType,string SourceKey,string TargetType,string TargetKey,string Actor,string? Reason=null);

public sealed record ProgressEvidenceDto(string Provider,string Kind,string Summary,string Confidence,string? Url,DateTimeOffset ObservedAtUtc,bool MachineObserved,IReadOnlyList<string>? References=null);
public sealed record ReconstructedProgressDto(
    string PlanKey,string Name,string? ParentKey,string? Owner,DateTimeOffset? PlannedStartUtc,DateTimeOffset? DueAtUtc,
    decimal? PlannedPercent,decimal? OperationalPercent,decimal? EvidenceReadinessPercent,string ReadinessConfidence,
    string Status,string? CanonicalTarget,bool IsMilestone,IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Contradictions,IReadOnlyList<string> StaleSignals,
    IReadOnlyList<ProgressEvidenceDto> Evidence,IReadOnlyList<SourceProvenanceDto> Sources);
public sealed record PlanActualContradictionDto(string Key,string Severity,string Title,string Explanation,string RecommendedAction,IReadOnlyList<string> Sources);
public sealed record ProjectProgressOverviewDto(
    Guid ProjectId,int PlanItems,int OnTrack,int Watch,int AtRisk,int UnverifiedClaims,int StaleItems,
    IReadOnlyList<ReconstructedProgressDto> Items,IReadOnlyList<PlanActualContradictionDto> Contradictions,
    DateTimeOffset GeneratedAtUtc);

public sealed record GanttIntelligenceItemDto(
    string Key,string Name,string? ParentKey,string? Owner,DateTimeOffset? StartUtc,DateTimeOffset? DueAtUtc,
    decimal? PlannedPercent,decimal? ActualReadinessPercent,string Status,string Confidence,bool IsMilestone,
    IReadOnlyList<string> Dependencies,IReadOnlyList<string> Flags);
public sealed record WbsGanttIntelligenceDto(Guid ProjectId,DateTimeOffset? RangeStartUtc,DateTimeOffset? RangeEndUtc,IReadOnlyList<GanttIntelligenceItemDto> Items,DateTimeOffset GeneratedAtUtc);

public sealed record ProjectManagementDigestDto(
    Guid ProjectId,string OverallStatus,string ExecutiveSummary,IReadOnlyList<string> NeedsAttention,
    IReadOnlyList<string> EvidenceGaps,IReadOnlyList<string> SourceContradictions,IReadOnlyList<string> RecentProgress,
    IReadOnlyList<string> RecommendedActions,DateTimeOffset GeneratedAtUtc);

public sealed record ProjectIntelligenceLayerDto(
    Guid ProjectId,ProjectProgressOverviewDto Progress,WbsGanttIntelligenceDto Gantt,EntityResolutionOverviewDto EntityResolution,
    ProjectManagementDigestDto Digest,DateTimeOffset GeneratedAtUtc);
