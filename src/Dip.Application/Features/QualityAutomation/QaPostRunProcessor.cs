// Purpose: Materializes QA pipeline/evidence records and runs governance and Project Truth post-processing.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.QualityAutomation;

public sealed class QaPostRunProcessor(
    IPipelineRunRepository pipelineRuns,
    IQualityIssueRepository qualityIssues,
    IEvidenceRepository evidence,
    IUnitOfWork unitOfWork,
    ISystemClock clock,
    PmEvidenceTranslator pm,
    QaGovernanceRunProcessor governance,
    ProjectTruthService truth)
{
    internal async Task<Guid?> ProcessAsync(
        QaExecutionPlan plan,
        QaRun run,
        IReadOnlyList<QaFinding> findings,
        IReadOnlyList<QaToolExecution> tools,
        string summary,
        CancellationToken ct)
    {
        var pipelineRunId = await MaterializePipelineAndIssuesAsync(
            plan.Command,
            run,
            findings,
            ct);
        await evidence.AddRangeAsync(
            BuildEvidence(
                plan,
                run,
                findings,
                tools,
                summary,
                pipelineRunId),
            ct);
        await unitOfWork.SaveChangesAsync(ct);
        await governance.ProcessCompletedRunAsync(run, findings, ct);
        await truth.RecalculateAsync(plan.Command.ProjectId, ct);
        return pipelineRunId;
    }

    private async Task<Guid?> MaterializePipelineAndIssuesAsync(
        QaExecutionCommand command,
        QaRun run,
        IReadOnlyList<QaFinding> findings,
        CancellationToken ct)
    {
        if (!TryCommitSha(command.CommitSha, out var commitSha))
            return null;

        var branch = string.IsNullOrWhiteSpace(command.BranchName)
            ? "qa-workspace"
            : command.BranchName.Trim();
        var pipeline = new PipelineRun(
            run.Id,
            command.ProjectId,
            commitSha,
            $"qa:{run.Id:N}",
            1,
            branch,
            "DIP-QA",
            "universal-qa-v2",
            run.CreatedAtUtc);
        pipeline.Start(run.StartedAtUtc ?? run.CreatedAtUtc);
        var finished = run.FinishedAtUtc ?? clock.UtcNow;

        switch (run.Status)
        {
            case QaRunStatus.Succeeded:
                pipeline.Complete(finished);
                break;
            case QaRunStatus.Partial:
                pipeline.CompletePartial(
                    "QA completed with warnings, unsupported languages, or unavailable optional capabilities.",
                    finished);
                break;
            case QaRunStatus.Cancelled:
                pipeline.Cancel("QA run was cancelled.", finished);
                break;
            case QaRunStatus.InfrastructureFailure:
                pipeline.Fail("QA infrastructure was unavailable.", finished);
                break;
            default:
                pipeline.Fail("QA reported one or more critical findings.", finished);
                break;
        }

        await pipelineRuns.AddAsync(pipeline, ct);

        foreach (var finding in findings
                     .Where(x => x.Severity is Severity.Critical or Severity.Warning)
                     .GroupBy(x => x.Fingerprint, StringComparer.OrdinalIgnoreCase)
                     .Select(x => x.OrderByDescending(item => item.Severity).First()))
        {
            var existing = await qualityIssues.FindOpenByFingerprintAsync(
                command.ProjectId,
                finding.Fingerprint,
                ct);
            if (existing is not null)
            {
                existing.Reoccur(pipeline.Id, finding.Severity, clock.UtcNow);
                continue;
            }

            var intelligence = ProductionQaIntelligence.Classify(finding);
            var decision = pm.Decide(
                ToCandidate(finding),
                finding.ReproductionStatus == ReproductionStatus.Reproduced);
            var evidenceJson = TrimPayload(JsonSerializer.Serialize(new
            {
                origin = "dip-universal-qa-v2",
                qaRunId = run.Id,
                pipelineRunId = pipeline.Id,
                commitSha = command.CommitSha,
                branch = command.BranchName,
                language = finding.Language.Value,
                finding.Tool,
                finding.FilePath,
                finding.Line,
                finding.Column,
                finding.Function,
                finding.ExceptionType,
                finding.ReproductionStatus,
                finding.ReproducedAttempts,
                finding.TotalAttempts,
                finding.DiagnosticSchemaVersion,
                finding.SourceFormat,
                finding.SourceFormatVersion,
                intelligence,
                pmDecision = decision
            }));

            var issue = new QualityIssue(
                Guid.NewGuid(),
                command.ProjectId,
                pipeline.Id,
                finding.Fingerprint,
                Limit(finding.Category, 80),
                finding.Severity,
                Limit(finding.Title, 200),
                Limit(finding.Message, 4000),
                LimitOptional(finding.FilePath, 1000),
                LimitOptional(finding.RuleId, 200),
                evidenceJson,
                clock.UtcNow);
            await qualityIssues.AddAsync(issue, ct);
        }

        return pipeline.Id;
    }

    private IEnumerable<EvidenceRecord> BuildEvidence(
        QaExecutionPlan plan,
        QaRun run,
        IReadOnlyList<QaFinding> findings,
        IReadOnlyList<QaToolExecution> tools,
        string summary,
        Guid? pipelineRunId)
    {
        var confidence = tools.Count > 0 && tools.All(x => x.WasAvailable)
            ? EvidenceConfidence.Verified
            : EvidenceConfidence.High;
        var outcome = run.Status == QaRunStatus.InfrastructureFailure
            ? EvidenceOutcome.Unknown
            : run.Status == QaRunStatus.Failed
              || findings.Any(x => x.Severity == Severity.Critical)
                ? EvidenceOutcome.Failed
                : run.Status == QaRunStatus.Partial
                  || findings.Any(x => x.Severity == Severity.Warning)
                    ? EvidenceOutcome.Warning
                    : EvidenceOutcome.Succeeded;

        yield return new EvidenceRecord(
            Guid.NewGuid(),
            plan.Command.ProjectId,
            pipelineRunId,
            plan.Command.SubjectKey,
            EvidenceSourceType.Quality,
            $"qa.{run.Language.Value}",
            outcome,
            $"{run.Language.Value} universal QA {outcome}",
            $"{findings.Count} finding(s); branch {plan.Command.BranchName ?? "workspace"}; "
            + $"production domain {plan.Production.Domain}; detected languages: "
            + $"{string.Join(", ", plan.Profile.Languages.Select(x => x.Value))}.",
            confidence,
            null,
            TrimPayload(summary),
            clock.UtcNow,
            clock.UtcNow);

        foreach (var finding in findings
                     .Where(x => x.Severity is Severity.Critical or Severity.Warning)
                     .Take(50))
        {
            var reproduced =
                finding.ReproductionStatus == ReproductionStatus.Reproduced;
            var candidate = ToCandidate(finding);
            var explanation = pm.Explain(candidate);
            var decision = pm.Decide(candidate, reproduced);
            var intelligence = ProductionQaIntelligence.Classify(finding);
            var payload = TrimPayload(JsonSerializer.Serialize(new
            {
                language = finding.Language.Value,
                finding.Fingerprint,
                finding.Tool,
                finding.Title,
                finding.Message,
                finding.FilePath,
                finding.Line,
                finding.ExceptionType,
                stackPreview = Preview(finding.StackTrace, 4000),
                finding.ReproductionStatus,
                finding.ReproducedAttempts,
                finding.TotalAttempts,
                classification = intelligence,
                pmDecision = decision,
                explanation.RecommendedAction,
                productionDomain = plan.Production.Domain,
                branch = plan.Command.BranchName,
                commitSha = plan.Command.CommitSha
            }));
            var text = reproduced
                ? $"{explanation.Summary} The same failure was automatically reproduced "
                  + $"{finding.ReproducedAttempts}/{finding.TotalAttempts} attempts."
                : explanation.Summary;

            yield return new EvidenceRecord(
                Guid.NewGuid(),
                plan.Command.ProjectId,
                pipelineRunId,
                plan.Command.SubjectKey,
                EvidenceSourceType.Quality,
                $"qa.finding.{finding.Category}",
                finding.Severity == Severity.Critical
                    ? EvidenceOutcome.Failed
                    : EvidenceOutcome.Warning,
                explanation.Title,
                text,
                reproduced ? EvidenceConfidence.Verified : EvidenceConfidence.High,
                null,
                payload,
                clock.UtcNow,
                clock.UtcNow);
        }
    }

    private static QaFindingCandidate ToCandidate(QaFinding finding) =>
        new(
            finding.Category,
            finding.Severity,
            finding.Tool,
            finding.Title,
            finding.Message,
            finding.FilePath,
            finding.Line,
            finding.Column,
            finding.Function,
            finding.ExceptionType,
            finding.StackTrace,
            finding.RuleId,
            finding.DiagnosticSchemaVersion,
            finding.SourceFormat,
            finding.SourceFormatVersion);

    private static bool TryCommitSha(string? value, out CommitSha sha)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is >= 7 and <= 64 && text.All(Uri.IsHexDigit))
        {
            sha = new CommitSha(text);
            return true;
        }

        sha = default;
        return false;
    }

    private static string Limit(string value, int max)
    {
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static string? LimitOptional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static string TrimPayload(string value) =>
        value.Length <= 15900 ? value : value[..15900];

    private static string? Preview(string? value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max
            ? value
            : value[..max];
}
