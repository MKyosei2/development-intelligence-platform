namespace GenericJsonCollector.Models;

public sealed record CollectorMetric(
    string Name,
    decimal Value,
    string Unit,
    decimal? BaselineValue);

public sealed record CollectorIssue(
    string Category,
    string Severity,
    string Title,
    string Description,
    string? Source);

public sealed record CollectorReport(
    string SchemaVersion,
    string CollectorType,
    string CollectorVersion,
    Guid ProjectId,
    string CommitSha,
    string Branch,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string Status,
    IReadOnlyList<CollectorMetric> Metrics,
    IReadOnlyList<CollectorIssue> Issues);
