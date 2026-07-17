using System.Text.Json.Serialization;

namespace GenericJsonCollector.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CollectorRunStatus
{
    Succeeded,
    PartialSuccess,
    Failed,
    Cancelled,
    TimedOut
}

public sealed record CollectorMetric(
    string Name,
    decimal Value,
    string Unit,
    decimal? BaselineValue,
    string? EnvironmentId = null,
    string? ScenarioId = null,
    string? Platform = null,
    string? BuildConfiguration = null,
    string? BenchmarkVersion = null);

public sealed record CollectorIssue(
    string Category,
    string Severity,
    string Title,
    string Description,
    string? Source,
    string? RuleId = null,
    string? EvidenceJson = null);

public sealed record CollectorReport(
    string SchemaVersion,
    string ReportId,
    string ExternalRunId,
    int AttemptNumber,
    string CollectorType,
    string CollectorVersion,
    Guid ProjectId,
    string Repository,
    string CommitSha,
    string Branch,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    CollectorRunStatus Status,
    IReadOnlyList<CollectorMetric> Metrics,
    IReadOnlyList<CollectorIssue> Issues);
