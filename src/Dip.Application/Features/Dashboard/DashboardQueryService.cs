namespace Dip.Application.Features.Dashboard;

public sealed record ProjectDashboardSummary(
    Guid ProjectId,
    string Name,
    string Repository,
    string DefaultBranch,
    decimal HealthScore,
    string RiskLevel,
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

public sealed record ProjectDashboardDetails(
    Guid ProjectId,
    string Name,
    string Repository,
    string DefaultBranch,
    decimal HealthScore,
    string RiskLevel,
    Guid? LatestPipelineRunId,
    string? LatestPipelineStatus,
    DateTimeOffset? LatestPipelineFinishedAtUtc,
    string? QualityGateStatus,
    decimal? QualityGateScore,
    int CriticalIssueCount,
    int WarningIssueCount,
    IReadOnlyList<string> QualityGateReasons,
    IReadOnlyList<DashboardMetricPoint> Metrics,
    IReadOnlyList<DashboardIssue> OpenIssues);

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
