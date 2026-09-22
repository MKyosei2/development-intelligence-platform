// Purpose: Applies incident, reproduction, and PM intelligence processing to materialized QA findings.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Incidents;
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.QualityAutomation;

internal sealed record QaFindingProcessingSummary(
    int ReleaseBlockers,
    int P0Decisions);

public sealed class QaFindingProcessor(
    IReproductionCoordinator reproduction,
    IReproductionAttemptRepository attempts,
    IUnitOfWork unitOfWork,
    ISystemClock clock,
    IncidentAggregationService incidents,
    PmEvidenceTranslator pm)
{
    internal async Task<QaFindingProcessingSummary> ProcessAsync(
        QaExecutionPlan plan,
        Guid runId,
        IReadOnlyList<QaFinding> findings,
        CancellationToken ct)
    {
        foreach (var finding in findings.Where(
                     x => x.Severity is Severity.Critical or Severity.Warning))
        {
            await incidents.ObserveQaFindingAsync(
                finding,
                plan.Command.CommitSha,
                ct);
        }

        if (plan.Command.AutomaticallyReproduce
            && !string.IsNullOrWhiteSpace(plan.Command.RuntimeExecutable))
        {
            var executable = ResolveRuntimeExecutable(
                plan.Workspace,
                plan.Command.RuntimeExecutable!);
            foreach (var finding in findings
                         .Where(ShouldReproduce)
                         .OrderByDescending(x => x.Severity)
                         .Take(10))
            {
                var intelligence = ProductionQaIntelligence.Classify(finding);
                var scenario = new ReproductionScenario(
                    plan.Command.ProjectId,
                    finding.Id,
                    finding.Language,
                    plan.Workspace,
                    plan.ArtifactRoot,
                    executable,
                    plan.Command.RuntimeArguments ?? [],
                    ReproductionEnvironment(finding.Language),
                    finding.Fingerprint,
                    Math.Clamp(plan.Command.TimeoutSeconds, 5, 1800),
                    intelligence.RecommendedAttempts,
                    plan.Command.RecordScreen,
                    plan.Command.RecordSystemAudio);
                var reproduced = await reproduction.ReproduceAsync(scenario, ct);

                foreach (var item in reproduced.Attempts)
                {
                    var entity = new ReproductionAttempt(
                        Guid.NewGuid(),
                        plan.Command.ProjectId,
                        finding.Id,
                        item.AttemptNumber,
                        item.ArtifactDirectory,
                        clock.UtcNow);
                    entity.Complete(
                        item.Status,
                        item.Reproduced,
                        item.Crashed,
                        item.TimedOut,
                        item.ExitCode,
                        item.DurationMs,
                        item.VideoPath,
                        item.AudioPath,
                        item.DumpPath,
                        item.ResultJson,
                        clock.UtcNow);
                    await attempts.AddAsync(entity, ct);
                }

                finding.ApplyReproduction(
                    reproduced.Status,
                    reproduced.ReproducedAttempts,
                    reproduced.TotalAttempts);
                await unitOfWork.SaveChangesAsync(ct);

                if (reproduced.Status == ReproductionStatus.Reproduced)
                {
                    await incidents.AttachReproductionAsync(
                        plan.Command.ProjectId,
                        finding.Fingerprint,
                        $"Automatically reproduced {reproduced.ReproducedAttempts}/{reproduced.TotalAttempts} attempt(s).",
                        ct);
                }
            }
        }

        var rows = findings.Select(finding =>
        {
            var candidate = ToCandidate(finding);
            var classified = ProductionQaIntelligence.Classify(finding);
            var decision = pm.Decide(
                candidate,
                finding.ReproductionStatus == ReproductionStatus.Reproduced);
            return new
            {
                findingId = finding.Id,
                fingerprint = finding.Fingerprint,
                classification = classified,
                pmDecision = decision,
                reproductionStatus = finding.ReproductionStatus.ToString()
            };
        }).ToArray();

        var intelligenceJson = JsonSerializer.Serialize(
            new
            {
                schema = "dip-qa-intelligence-v1",
                projectId = plan.Command.ProjectId,
                qaRunId = runId,
                subjectKey = plan.Command.SubjectKey,
                commitSha = plan.Command.CommitSha,
                branch = new
                {
                    plan.Command.BranchName,
                    sourceRef = plan.SourceRef,
                    refreshStatus = plan.RefreshStatus,
                    warning = plan.Warning,
                    isolated = plan.IsIsolated
                },
                production = plan.Production,
                findings = rows
            },
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(
            Path.Combine(plan.ArtifactRoot, "qa-intelligence.json"),
            intelligenceJson,
            ct);

        return new(
            rows.Count(x => x.pmDecision.BlocksRelease),
            rows.Count(x => x.pmDecision.Priority == "P0"));
    }

    private static bool ShouldReproduce(QaFinding finding)
    {
        var intelligence = ProductionQaIntelligence.Classify(finding);
        return intelligence.ReleaseBlocker
            || finding.Category.Contains("runtime", StringComparison.OrdinalIgnoreCase)
            || finding.Category.Contains("crash", StringComparison.OrdinalIgnoreCase)
            || finding.Category.Contains("test", StringComparison.OrdinalIgnoreCase)
            || finding.Category.Contains("hang", StringComparison.OrdinalIgnoreCase)
            || finding.Category.Contains("network", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveRuntimeExecutable(string workspace, string value)
    {
        if (Path.IsPathRooted(value))
            throw new InvalidOperationException(
                "Runtime executable must be workspace-relative.");

        var candidate = Path.GetFullPath(Path.Combine(workspace, value));
        var prefix = workspace.TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Runtime executable escapes the QA workspace.");
        return candidate;
    }

    private static IReadOnlyDictionary<string, string> ReproductionEnvironment(
        LanguageId language) =>
        language == LanguageId.Python
            ? new Dictionary<string, string>
            {
                ["PYTHONFAULTHANDLER"] = "1",
                ["PYTHONASYNCIODEBUG"] = "1"
            }
            : new Dictionary<string, string>();

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
}
