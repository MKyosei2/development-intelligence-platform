// Purpose: Persists QA runs and findings and computes the final run summary from execution results.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation;

internal sealed record QaCompletedRunMaterialization(
    IReadOnlyList<QaToolExecution> Tools,
    string Summary);

public sealed class QaResultMaterializer(
    IQaRunRepository runs,
    IQaFindingRepository findings,
    IUnitOfWork unitOfWork,
    ISystemClock clock)
{
    internal async Task<QaRun> CreateRunAsync(
        QaExecutionPlan plan,
        Guid runId,
        CancellationToken ct)
    {
        var run = new QaRun(
            runId,
            plan.Command.ProjectId,
            plan.Command.SubjectKey,
            plan.RunLanguage,
            plan.SourceWorkspaceRelativePath,
            plan.Command.CommitSha,
            plan.ArtifactRoot,
            clock.UtcNow);
        await runs.AddAsync(run, ct);
        run.Start(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return run;
    }

    internal async Task<List<QaFinding>> MaterializeFindingsAsync(
        QaExecutionPlan plan,
        QaAdapterExecutionBatch batch,
        Guid runId,
        CancellationToken ct)
    {
        var candidates = batch.Outputs
            .SelectMany(output => output.Result.Findings.Select(
                finding => (Language: output.Language, Finding: finding)))
            .Concat(batch.CrossResults.SelectMany(result => result.Findings.Select(
                finding => (Language: plan.RunLanguage, Finding: finding))))
            .ToArray();

        var entities = candidates.Select(x => new QaFinding(
            Guid.NewGuid(),
            plan.Command.ProjectId,
            runId,
            plan.Command.SubjectKey,
            x.Language,
            x.Finding.Category,
            x.Finding.Severity,
            x.Finding.Tool,
            x.Finding.Title,
            x.Finding.Message,
            QaFingerprint.Create(x.Language, x.Finding),
            x.Finding.FilePath,
            x.Finding.Line,
            x.Finding.Column,
            x.Finding.Function,
            x.Finding.ExceptionType,
            x.Finding.StackTrace,
            x.Finding.RuleId,
            clock.UtcNow,
            x.Finding.DiagnosticSchemaVersion,
            x.Finding.SourceFormat,
            x.Finding.SourceFormatVersion)).ToList();

        await findings.AddRangeAsync(entities, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return entities;
    }

    internal QaCompletedRunMaterialization Complete(
        QaExecutionPlan plan,
        QaAdapterExecutionBatch batch,
        QaRun run,
        IReadOnlyList<QaFinding> findings,
        int releaseBlockers,
        int p0Decisions)
    {
        var tools = batch.Outputs.SelectMany(x => x.Result.Tools)
            .Concat(batch.CrossResults.SelectMany(x => x.Tools))
            .ToArray();
        var critical = findings.Count(x => x.Severity == Severity.Critical);
        var warning = findings.Count(x => x.Severity == Severity.Warning);
        var unavailableLanguages = batch.Outputs
            .Where(x => x.Result.Tools.Count == 0 || x.Result.Tools.All(t => !t.WasAvailable))
            .Select(x => x.Language.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var allAdaptersFailed = unavailableLanguages.Length >= plan.Selections.Count;
        var finalStatus = allAdaptersFailed
            ? QaRunStatus.InfrastructureFailure
            : critical > 0
                ? QaRunStatus.Failed
                : warning > 0
                  || batch.AdapterFailures.Count > 0
                  || unavailableLanguages.Length > 0
                  || plan.UnsupportedDetectedLanguages.Count > 0
                    ? QaRunStatus.Partial
                    : QaRunStatus.Succeeded;

        var summary = JsonSerializer.Serialize(new
        {
            schema = "dip-universal-qa-v2",
            requestedLanguage = plan.RequestedLanguage.Value,
            runLanguage = plan.RunLanguage.Value,
            branch = new
            {
                name = plan.Command.BranchName,
                commitSha = plan.Command.CommitSha,
                sourceRef = plan.SourceRef,
                refreshStatus = plan.RefreshStatus,
                warning = plan.Warning,
                isolated = plan.IsIsolated
            },
            detected = new
            {
                languages = plan.Profile.Languages.Select(x => x.Value),
                plan.Profile.BuildSystems,
                plan.Profile.Frameworks,
                plan.Profile.GameEngines,
                plan.Profile.Evidence
            },
            production = plan.Production,
            adapters = plan.Selections.Select(
                x => new { language = x.Language.Value, adapterId = x.Adapter.AdapterId }),
            crossCuttingPacks = batch.CrossResults.Select(x => x.PackId),
            adapterFailures = batch.AdapterFailures,
            unavailableLanguages,
            unsupportedDetectedLanguages = plan.UnsupportedDetectedLanguages,
            tools = tools.Select(x => new
            {
                x.Tool,
                x.WasAvailable,
                x.Succeeded,
                x.ExitCode,
                x.DurationMs,
                x.Executable,
                x.Version,
                x.SandboxJson,
                x.CpuTimeMs
            }),
            findingCount = findings.Count,
            critical,
            warning,
            releaseBlockers,
            p0Decisions,
            intelligenceArtifact = "qa-intelligence.json"
        });

        run.Complete(
            finalStatus,
            findings.Count,
            critical,
            warning,
            summary,
            clock.UtcNow);
        return new(tools, summary);
    }
}
