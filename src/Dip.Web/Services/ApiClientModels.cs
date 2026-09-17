// Purpose: Defines web-client transport DTOs shared by ApiClient operations.
namespace Dip.Web.Services;

public sealed record CreateClaimRequest(
    string SubjectKey,
    string ClaimedStatus,
    decimal? ClaimedProgress,
    string? ClaimedBy,
    string? Description);

public sealed record PipelineQualityIssueClientDto(
    Guid Id,
    string Severity,
    string Status,
    string Title,
    string Category,
    string Fingerprint,
    string? EvidenceJson);

public sealed record PipelineEvidenceClientDto(
    Guid Id,
    string EvidenceKind,
    string Outcome,
    string Title,
    string Summary,
    string Confidence,
    string? SourceReference,
    DateTimeOffset ObservedAtUtc);

public sealed record PipelineRunClientDto(
    Guid Id,
    Guid ProjectId,
    string CommitSha,
    string ExternalRunId,
    int AttemptNumber,
    string Branch,
    string CollectorType,
    string CollectorVersion,
    string Status,
    DateTimeOffset QueuedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string? FailureReason,
    IReadOnlyList<PipelineQualityIssueClientDto>? QualityIssues = null,
    IReadOnlyList<PipelineEvidenceClientDto>? Evidence = null);

public sealed record GitHubCapabilityCheckClientDto(
    string Name,
    string Status,
    string Message);

public sealed record GitHubConnectionTestClientDto(
    bool Success,
    string Mode,
    string Message,
    int RepositoryCount,
    DateTimeOffset TestedAtUtc,
    string OverallStatus = "Unknown",
    string? Repository = null,
    IReadOnlyList<GitHubCapabilityCheckClientDto>? Checks = null);

public sealed record PullRequestSummaryClientDto(
    int Number,
    string Title,
    string Status,
    string? Url,
    int ChangedFiles,
    int Reviews,
    int CiRuns,
    bool Critical,
    string? ReviewState);

public sealed record PullRequestReviewClientDto(
    string Reviewer,
    string State,
    string? Url);

public sealed record PullRequestCiClientDto(
    string Name,
    string Status,
    string? Url);

public sealed record PullRequestPipelineClientDto(
    Guid Id,
    string CommitSha,
    string Status,
    string CollectorType,
    string ExternalRunId,
    DateTimeOffset? FinishedAtUtc,
    int QualityIssueCount,
    int EvidenceCount);

public sealed record PullRequestDetailsClientDto(
    PullRequestSummaryClientDto Summary,
    IReadOnlyList<string> Revisions,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<PullRequestReviewClientDto> Reviews,
    IReadOnlyList<PullRequestCiClientDto> CiRuns,
    IReadOnlyList<PullRequestPipelineClientDto> PipelineRuns,
    IReadOnlyList<PipelineQualityIssueClientDto> QualityIssues,
    IReadOnlyList<PipelineEvidenceClientDto> Evidence);

public sealed record QualityIssueClientDto(
    Guid Id,
    Guid ProjectId,
    Guid PipelineRunId,
    string Fingerprint,
    string Category,
    string Severity,
    string Title,
    string Description,
    string? Source,
    string? RuleId,
    string? EvidenceJson,
    string Status,
    int OccurrenceCount,
    Guid FirstSeenPipelineRunId,
    Guid LastSeenPipelineRunId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset? ResolvedAtUtc);

public sealed record QaTrendPointClientDto(
    Guid RunId,
    string Language,
    string Status,
    int FindingCount,
    int CriticalFindingCount,
    int WarningFindingCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? FinishedAtUtc);
