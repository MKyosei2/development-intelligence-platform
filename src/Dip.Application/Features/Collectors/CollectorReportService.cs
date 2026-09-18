// Purpose: Orchestrates the DIP workflow for C ol le ct or Re po rt Se rv ic e.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ProjectTruth;
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
    private readonly IEvidenceRepository _evidence;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemClock _clock;
    private readonly QualityGateEvaluator _gate;
    private readonly CollectorReportValidator _validator;
    private readonly OutboxWriter _outbox;
    private readonly ProjectTruthService _truth;

    public CollectorReportService(
        IProjectRepository projects,
        IPipelineRunRepository runs,
        IMetricRepository metrics,
        IQualityIssueRepository issues,
        IEvidenceRepository evidence,
        IUnitOfWork unitOfWork,
        ISystemClock clock,
        QualityGateEvaluator gate,
        CollectorReportValidator validator,
        OutboxWriter outbox,
        ProjectTruthService truth)
    {
        _projects = projects;
        _runs = runs;
        _metrics = metrics;
        _issues = issues;
        _evidence = evidence;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _gate = gate;
        _validator = validator;
        _outbox = outbox;
        _truth = truth;
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
            // A previous attempt may have committed the report but failed while
            // refreshing Project Truth. Re-evaluate on a duplicate delivery so
            // the dashboard can self-heal without duplicating collector data.
            await _truth.RecalculateAsync(existingRun.ProjectId, cancellationToken);

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
        var evidence = BuildEvidence(
            project.Id,
            run,
            report,
            metrics,
            effectiveIssues,
            qualityGate,
            _clock.UtcNow);

        await _runs.AddAsync(run, cancellationToken);
        await _metrics.AddRangeAsync(metrics, cancellationToken);
        await _evidence.AddRangeAsync(evidence, cancellationToken);

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

        // Project Truth is intentionally downstream of durable evidence.
        await _truth.RecalculateAsync(project.Id, cancellationToken);

        return new(
            run.Id,
            run.Status.ToString(),
            false,
            qualityGate);
    }

    private static IReadOnlyList<EvidenceRecord> BuildEvidence(
        Guid projectId,
        PipelineRun run,
        CollectorReport report,
        IReadOnlyCollection<Metric> metrics,
        IReadOnlyCollection<QualityIssue> issues,
        QualityGateResultDto qualityGate,
        DateTimeOffset createdAtUtc)
    {
        var result = new List<EvidenceRecord>();
        var sourceType = CollectorSourceType(report.CollectorType);
        var runOutcome = report.Status switch
        {
            CollectorRunStatus.Succeeded => EvidenceOutcome.Succeeded,
            CollectorRunStatus.PartialSuccess => EvidenceOutcome.Warning,
            CollectorRunStatus.Failed => EvidenceOutcome.Failed,
            CollectorRunStatus.Cancelled => EvidenceOutcome.Warning,
            CollectorRunStatus.TimedOut => EvidenceOutcome.Failed,
            _ => EvidenceOutcome.Unknown
        };

        result.Add(new EvidenceRecord(
            Guid.NewGuid(),
            projectId,
            run.Id,
            "project",
            sourceType,
            "pipeline",
            runOutcome,
            $"{report.CollectorType} pipeline {report.Status}",
            $"Collector {report.CollectorType} {report.CollectorVersion} finished with status {report.Status}.",
            EvidenceConfidence.High,
            run.Id.ToString(),
            JsonSerializer.Serialize(new
            {
                report.ExternalRunId,
                report.AttemptNumber,
                report.Branch,
                report.CommitSha,
                report.CollectorType,
                report.CollectorVersion
            }),
            report.FinishedAt,
            createdAtUtc));

        var gateOutcome = qualityGate.Status switch
        {
            "Passed" => EvidenceOutcome.Succeeded,
            "Warning" => EvidenceOutcome.Warning,
            "Failed" => EvidenceOutcome.Failed,
            _ => EvidenceOutcome.Unknown
        };

        result.Add(new EvidenceRecord(
            Guid.NewGuid(),
            projectId,
            run.Id,
            "project",
            EvidenceSourceType.Quality,
            "quality-gate",
            gateOutcome,
            $"Quality gate {qualityGate.Status}",
            string.Join(" ", qualityGate.Reasons.Take(5)),
            EvidenceConfidence.High,
            run.Id.ToString(),
            JsonSerializer.Serialize(qualityGate),
            report.FinishedAt,
            createdAtUtc));

        foreach (var metric in metrics)
        {
            var subject = !string.IsNullOrWhiteSpace(metric.ScenarioId)
                ? $"scenario:{metric.ScenarioId}"
                : "project";

            result.Add(new EvidenceRecord(
                Guid.NewGuid(),
                projectId,
                run.Id,
                subject,
                EvidenceSourceType.Performance,
                $"metric:{metric.Name}",
                EvidenceOutcome.Informational,
                $"Metric observed: {metric.Name}",
                metric.DifferencePercent is { } difference
                    ? $"{metric.Name}={metric.Value} {metric.Unit}; baseline={metric.BaselineValue} {metric.Unit}; change={difference:+0.##;-0.##;0}%"
                    : $"{metric.Name}={metric.Value} {metric.Unit}; no comparable baseline.",
                EvidenceConfidence.High,
                run.Id.ToString(),
                JsonSerializer.Serialize(new
                {
                    metric.Name,
                    metric.Value,
                    metric.Unit,
                    metric.BaselineValue,
                    metric.DifferencePercent,
                    metric.EnvironmentId,
                    metric.ScenarioId,
                    metric.Platform,
                    metric.BuildConfiguration
                }),
                report.FinishedAt,
                createdAtUtc));
        }

        foreach (var issue in issues)
        {
            var outcome = issue.Severity switch
            {
                Severity.Critical => EvidenceOutcome.Failed,
                Severity.Warning => EvidenceOutcome.Warning,
                _ => EvidenceOutcome.Informational
            };

            result.Add(new EvidenceRecord(
                Guid.NewGuid(),
                projectId,
                run.Id,
                "project",
                EvidenceSourceType.Quality,
                $"issue:{issue.Category}",
                outcome,
                issue.Title,
                issue.Description,
                EvidenceConfidence.High,
                issue.Source ?? run.Id.ToString(),
                issue.EvidenceJson,
                issue.LastSeenAtUtc,
                createdAtUtc));
        }

        return result;
    }

    private static EvidenceSourceType CollectorSourceType(string collectorType)
    {
        if (collectorType.Contains("unity", StringComparison.OrdinalIgnoreCase))
            return EvidenceSourceType.Unity;
        if (collectorType.Contains("maya", StringComparison.OrdinalIgnoreCase))
            return EvidenceSourceType.Maya;
        return EvidenceSourceType.Collector;
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
