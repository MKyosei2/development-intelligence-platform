using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Quality;
using Dip.Application.Services;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.Services;
using Dip.Domain.ValueObjects;
using Dip.Shared.Contracts.Collectors;

namespace Dip.Application.Features.Collectors;

public sealed record CollectorReportProcessed(Guid PipelineRunId);

public sealed record CollectorReportResult(
    Guid PipelineRunId,
    string PipelineStatus,
    bool WasDuplicate,
    QualityGateResultDto QualityGate);

public sealed class CollectorReportService
{
    private readonly IProjectRepository _projects;
    private readonly IPipelineRunRepository _runs;
    private readonly IMetricRepository _metrics;
    private readonly IQualityIssueRepository _issues;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemClock _clock;
    private readonly QualityGateEvaluator _gate;
    private readonly CollectorReportValidator _validator;
    private readonly OutboxWriter _outbox;

    public CollectorReportService(
        IProjectRepository projects,
        IPipelineRunRepository runs,
        IMetricRepository metrics,
        IQualityIssueRepository issues,
        IUnitOfWork unitOfWork,
        ISystemClock clock,
        QualityGateEvaluator gate,
        CollectorReportValidator validator,
        OutboxWriter outbox)
    {
        _projects = projects;
        _runs = runs;
        _metrics = metrics;
        _issues = issues;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _gate = gate;
        _validator = validator;
        _outbox = outbox;
    }

    public async Task<CollectorReportResult> ProcessAsync(
        CollectorReport report,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        _validator.ValidateAndThrow(report);

        var existingRun = await _runs.FindByExternalRunAsync(
            report.ProjectId,
            report.ExternalRunId,
            report.CollectorType,
            report.AttemptNumber,
            cancellationToken);

        if (existingRun is not null)
        {
            return new(
                existingRun.Id,
                existingRun.Status.ToString(),
                true,
                new(
                    "PreviouslyProcessed",
                    0,
                    0,
                    100,
                    new[] { "Report already processed." }));
        }

        var project = await _projects.GetByIdAsync(
                          report.ProjectId,
                          cancellationToken)
                      ?? throw new KeyNotFoundException(
                          $"Project {report.ProjectId} not found.");

        if (!RepositoryMatches(project.RepositoryUrl, report.Repository))
        {
            throw new ArgumentException(
                "Collector repository does not match the project.",
                nameof(report));
        }

        var run = new PipelineRun(
            Guid.NewGuid(),
            project.Id,
            new CommitSha(report.CommitSha),
            report.ExternalRunId,
            report.AttemptNumber,
            report.Branch.Trim(),
            report.CollectorType.Trim(),
            report.CollectorVersion.Trim(),
            report.StartedAt);

        run.Start(report.StartedAt);
        ApplyStatus(run, report.Status, report.FinishedAt);

        var metrics = report.Metrics.Select(metric => new Metric(
            Guid.NewGuid(),
            run.Id,
            metric.Name.Trim(),
            metric.Value,
            metric.Unit.Trim(),
            metric.BaselineValue,
            null,
            Clean(metric.EnvironmentId),
            Clean(metric.ScenarioId),
            Clean(metric.Platform),
            Clean(metric.BuildConfiguration),
            Clean(metric.BenchmarkVersion))).ToArray();

        var effectiveIssues = new List<QualityIssue>();

        foreach (var input in report.Issues)
        {
            if (!Enum.TryParse<Severity>(
                    input.Severity,
                    true,
                    out var severity)
                || !Enum.IsDefined(severity))
            {
                throw new ArgumentException(
                    $"Unsupported issue severity '{input.Severity}'.",
                    nameof(report));
            }

            var fingerprint = QualityIssueFingerprint.Create(
                project.Id,
                input.Category,
                input.RuleId,
                input.Source,
                input.Title);

            var existingIssue = await _issues.FindOpenByFingerprintAsync(
                project.Id,
                fingerprint,
                cancellationToken);

            if (existingIssue is not null)
            {
                existingIssue.Reoccur(
                    run.Id,
                    severity,
                    _clock.UtcNow);

                effectiveIssues.Add(existingIssue);
                continue;
            }

            var issue = new QualityIssue(
                Guid.NewGuid(),
                project.Id,
                run.Id,
                fingerprint,
                input.Category.Trim(),
                severity,
                input.Title.Trim(),
                input.Description.Trim(),
                Clean(input.Source),
                Clean(input.RuleId),
                Clean(input.EvidenceJson),
                _clock.UtcNow);

            await _issues.AddAsync(issue, cancellationToken);
            effectiveIssues.Add(issue);
        }

        var qualityGate = _gate.Evaluate(metrics, effectiveIssues);

        await _runs.AddAsync(run, cancellationToken);
        await _metrics.AddRangeAsync(metrics, cancellationToken);

        // The report data and the outbox event are committed together.
        // This prevents a successfully stored report from silently missing
        // its asynchronous quality-gate evaluation.
        await _outbox.EnqueueAsync(
            new CollectorReportProcessed(run.Id),
            report.ReportId,
            report.ExternalRunId,
            _clock.UtcNow,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new(
            run.Id,
            run.Status.ToString(),
            false,
            qualityGate);
    }

    private static void ApplyStatus(
        PipelineRun run,
        CollectorRunStatus status,
        DateTimeOffset finishedAt)
    {
        switch (status)
        {
            case CollectorRunStatus.Succeeded:
                run.Complete(finishedAt);
                break;
            case CollectorRunStatus.PartialSuccess:
                run.CompletePartial(
                    "Partial success.",
                    finishedAt);
                break;
            case CollectorRunStatus.Failed:
                run.Fail(
                    "Collector failure.",
                    finishedAt);
                break;
            case CollectorRunStatus.Cancelled:
                run.Cancel(
                    "Collector cancelled.",
                    finishedAt);
                break;
            case CollectorRunStatus.TimedOut:
                run.Timeout(
                    "Collector timed out.",
                    finishedAt);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    private static bool RepositoryMatches(
        string expected,
        string actual)
    {
        static string Normalize(string value)
        {
            var normalized = value.Trim().TrimEnd('/');

            if (normalized.EndsWith(
                    ".git",
                    StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^4];
            }

            if (Uri.TryCreate(
                    normalized,
                    UriKind.Absolute,
                    out var uri))
            {
                normalized = uri.AbsolutePath.Trim('/');
            }

            return normalized;
        }

        return string.Equals(
            Normalize(expected),
            Normalize(actual),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
