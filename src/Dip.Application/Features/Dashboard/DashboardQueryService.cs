namespace Dip.Application.Features.Dashboard;

public sealed record ProjectDashboardSummary(
    Guid ProjectId,
    string Name,
    string Repository,
    string DefaultBranch,
    decimal HealthScore,
    string TruthStatus,
    string RiskLevel,
    decimal VerifiedProgress,
    decimal? ReportedProgress,
    string EvidenceConfidence,
    int BlockerCount,
    int ContradictionCount,
    string? LatestPipelineStatus,
    string? LatestQualityGateStatus,
    DateTimeOffset? LastEvaluatedAtUtc);

public sealed record DashboardMetricPoint(
    string Name,
    decimal Value,
    string Unit,
    decimal? DifferencePercent);

public sealed record DashboardIssue(
    Guid Id,
    string Severity,
    string Category,
    string Title,
    string Status);

public sealed record DashboardBlocker(
    string Code,
    string Severity,
    string Title,
    string Detail,
    string SubjectKey,
    Guid? EvidenceId);

public sealed record DashboardClaimComparison(
    Guid ClaimId,
    string SubjectKey,
    string ClaimedStatus,
    decimal? ClaimedProgress,
    string ClaimedBy,
    string VerificationStatus,
    string VerificationSummary,
    int SupportingEvidenceCount,
    int ContradictingEvidenceCount,
    DateTimeOffset ClaimedAtUtc);

public sealed record DashboardTimelineItem(
    DateTimeOffset AtUtc,
    string Kind,
    string Title,
    string Detail,
    string Confidence,
    string? SubjectKey,
    string? SourceReference);


public sealed record DashboardQaRun(
    Guid Id, string Language, string Status, int FindingCount, int CriticalFindingCount, int WarningFindingCount,
    int ReproducedFindingCount, DateTimeOffset CreatedAtUtc);

public sealed record DashboardHardwareSimulation(
    Guid Id, string ProfileName, string Platform, string Status, decimal ConfidencePercent, DateTimeOffset CreatedAtUtc);

public sealed record ProjectDashboardDetails(
    Guid ProjectId,
    string Name,
    string Repository,
    string DefaultBranch,
    decimal HealthScore,
    string TruthStatus,
    string RiskLevel,
    decimal VerifiedProgress,
    decimal? ReportedProgress,
    string EvidenceConfidence,
    int BlockerCount,
    int ContradictionCount,
    int UnverifiedClaimCount,
    string TruthSummary,
    IReadOnlyList<string> TopActions,
    IReadOnlyList<DashboardBlocker> Blockers,
    IReadOnlyList<DashboardClaimComparison> ReportVsReality,
    IReadOnlyList<DashboardTimelineItem> Timeline,
    DateTimeOffset? TruthEvaluatedAtUtc,
    Guid? LatestPipelineRunId,
    string? LatestPipelineStatus,
    DateTimeOffset? LatestPipelineFinishedAtUtc,
    string? QualityGateStatus,
    decimal? QualityGateScore,
    int CriticalIssueCount,
    int WarningIssueCount,
    IReadOnlyList<string> QualityGateReasons,
    IReadOnlyList<DashboardMetricPoint> Metrics,
    IReadOnlyList<DashboardIssue> OpenIssues)
{
    public IReadOnlyList<DashboardQaRun> RecentQaRuns { get; init; } = Array.Empty<DashboardQaRun>();
    public IReadOnlyList<DashboardHardwareSimulation> RecentHardwareSimulations { get; init; } = Array.Empty<DashboardHardwareSimulation>();
}


public interface IDashboardReadRepository
{
    Task<IReadOnlyList<ProjectDashboardSummary>> GetProjectsAsync(
        CancellationToken cancellationToken);

    Task<ProjectDashboardDetails?> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken);
}

public sealed class DashboardQueryService
{
    private readonly IDashboardReadRepository _repository;

    public DashboardQueryService(IDashboardReadRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<ProjectDashboardSummary>> GetProjectsAsync(
        CancellationToken cancellationToken) =>
        _repository.GetProjectsAsync(cancellationToken);

    public Task<ProjectDashboardDetails?> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException(
                "ProjectId is required.",
                nameof(projectId));

        return _repository.GetProjectAsync(
            projectId,
            cancellationToken);
    }
}
