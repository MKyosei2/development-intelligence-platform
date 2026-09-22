// Purpose: Coordinates the QA pipeline through planning, execution, finding processing, persistence, and post-run verification.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation;

public sealed class QaOrchestrationService(
    IQaRunRepository runs,
    IQaFindingRepository findings,
    IPipelineRunRepository pipelineRuns,
    QaExecutionPlanner planner,
    QaAdapterExecutionCoordinator adapterExecution,
    QaResultMaterializer materializer,
    QaFindingProcessor findingProcessor,
    QaPostRunProcessor postRun)
{
    public async Task<QaRunResultDto> RunAsync(
        QaExecutionCommand command,
        CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid();
        await using var plan = await planner.CreateAsync(
            command,
            runId,
            cancellationToken);

        var run = await materializer.CreateRunAsync(
            plan,
            runId,
            cancellationToken);
        var batch = await adapterExecution.ExecuteAsync(
            plan,
            cancellationToken);
        var findingEntities = await materializer.MaterializeFindingsAsync(
            plan,
            batch,
            runId,
            cancellationToken);
        var findingSummary = await findingProcessor.ProcessAsync(
            plan,
            runId,
            findingEntities,
            cancellationToken);
        var completed = materializer.Complete(
            plan,
            batch,
            run,
            findingEntities,
            findingSummary.ReleaseBlockers,
            findingSummary.P0Decisions);
        var pipelineRunId = await postRun.ProcessAsync(
            plan,
            run,
            findingEntities,
            completed.Tools,
            completed.Summary,
            cancellationToken);

        return Map(run, findingEntities, pipelineRunId);
    }

    public async Task<QaRunResultDto?> GetAsync(
        Guid qaRunId,
        CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(qaRunId, cancellationToken);
        if (run is null)
            return null;

        var runFindings = await findings.ListByRunAsync(
            qaRunId,
            cancellationToken);
        var pipeline = await pipelineRuns.GetByIdAsync(
            qaRunId,
            cancellationToken);
        return Map(run, runFindings, pipeline?.Id);
    }

    public async Task<IReadOnlyList<QaCrashClusterDto>> GetCrashClustersAsync(
        Guid projectId,
        int take,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));

        var requested = Math.Clamp(take, 1, 200);
        var recent = await findings.ListRecentByProjectAsync(
            projectId,
            Math.Clamp(requested * 20, 100, 4000),
            cancellationToken);

        return recent
            .Where(x =>
                x.Severity == Severity.Critical
                || x.Category.Contains("crash", StringComparison.OrdinalIgnoreCase)
                || x.Category.Contains("runtime", StringComparison.OrdinalIgnoreCase)
                || x.Category.Contains("hang", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.Fingerprint, StringComparer.OrdinalIgnoreCase)
            .Select(group => new QaCrashClusterDto(
                group.Key,
                group.OrderByDescending(x => x.ObservedAtUtc).First().Category,
                group.Max(x => x.Severity).ToString(),
                group.OrderByDescending(x => x.ObservedAtUtc).First().Title,
                group.Count(),
                group.Min(x => x.ObservedAtUtc),
                group.Max(x => x.ObservedAtUtc),
                group.Count(
                    x => x.ReproductionStatus == ReproductionStatus.Reproduced),
                group.Select(x => x.SubjectKey)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .Take(20)
                    .ToArray()))
            .OrderByDescending(x => x.Occurrences)
            .ThenByDescending(x => x.LastSeenAtUtc)
            .Take(requested)
            .ToArray();
    }

    private static QaRunResultDto Map(
        QaRun run,
        IReadOnlyCollection<QaFinding> findings,
        Guid? pipelineRunId) =>
        new(
            run.Id,
            run.Status.ToString(),
            run.Language.Value,
            run.FindingCount,
            run.CriticalFindingCount,
            run.WarningFindingCount,
            findings.Select(x => new QaFindingDto(
                x.Id,
                x.Category,
                x.Severity.ToString(),
                x.Tool,
                x.Title,
                x.Message,
                x.Fingerprint,
                x.FilePath,
                x.Line,
                x.ReproductionStatus.ToString(),
                x.ReproducedAttempts,
                x.TotalAttempts)).ToArray(),
            $"api/v1/qa/runs/{run.Id}/artifacts",
            run.SummaryJson,
            pipelineRunId);
}
