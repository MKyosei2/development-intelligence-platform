// Purpose: Persists benchmark artifacts, evidence, incidents, and terminal state while preserving persistence failures.
using System.Text.Json;
using Dip.Application.Features.Artifacts;
using Dip.Application.Features.Performance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Benchmarks;

public sealed partial class BenchmarkOrchestrationService
{
    private sealed record BenchmarkState(
        bool Failed,
        bool Warning,
        bool SampleFailure,
        bool BudgetFailed,
        bool BudgetWarning,
        BenchmarkRunStatus RunStatus,
        EvidenceOutcome Outcome);

    private sealed record BenchmarkArtifacts(
        ArtifactRecord Summary,
        ArtifactRecord Samples,
        ArtifactRecord Execution,
        ArtifactRecord Comparison);

    private static BenchmarkState Analyze(
        BenchmarkExecutionResult execution,
        IReadOnlyList<BenchmarkComparison> comparisons,
        PerformanceBudgetEvaluationDto? budget)
    {
        var failed = comparisons.Any(x => x.Verdict == BenchmarkVerdict.Failed);
        var warning = comparisons.Any(x => x.Verdict == BenchmarkVerdict.Warning);
        var sampleFailure = execution.Samples.Any(x => !x.Passed);
        var budgetFailed = budget?.Verdict == PerformanceBudgetVerdict.Failed.ToString();
        var budgetWarning = budget?.Verdict == PerformanceBudgetVerdict.Warning.ToString();
        var runStatus = sampleFailure ? BenchmarkRunStatus.Failed : BenchmarkRunStatus.Succeeded;
        var outcome = sampleFailure || failed || budgetFailed
            ? EvidenceOutcome.Failed
            : warning || budgetWarning
                ? EvidenceOutcome.Warning
                : EvidenceOutcome.Succeeded;
        return new(failed, warning, sampleFailure, budgetFailed, budgetWarning, runStatus, outcome);
    }

    private static string BuildSummary(
        BenchmarkDefinition definition,
        IReadOnlyList<BenchmarkMetric> metrics,
        BenchmarkRun? baseline,
        BenchmarkState state,
        PerformanceBudgetEvaluationDto? budget)
    {
        var p95 = metrics.First(x => x.Name == "latency" && x.Aggregation == "p95").Value;
        var errorRate = metrics.First(x => x.Name == "error_rate").Value;
        var comparison = baseline is null ? "baseline unavailable" : state.Failed ? "failed" : state.Warning ? "warning" : "passed";
        var budgetText = budget is null ? "not selected" : budget.Verdict;
        return $"Benchmark {definition.Key}: {p95:0.###} ms p95, {errorRate:0.###}% errors; comparison {comparison}; budget {budgetText}.";
    }

    private async Task<BenchmarkArtifacts> StoreArtifactsAsync(
        DurableJob job,
        BenchmarkJobRequest request,
        BenchmarkDefinition definition,
        BenchmarkRun run,
        BenchmarkExecutionResult execution,
        IReadOnlyList<BenchmarkMetric> metrics,
        IReadOnlyList<BenchmarkComparison> comparisons,
        BenchmarkRun? baseline,
        PerformanceBudgetEvaluationDto? budget,
        string summary,
        CancellationToken ct)
    {
        var links = new ArtifactLinkContext(job.Id, null, null, run.Id);
        var summaryArtifact = await _artifacts.StoreJsonAsync(
            request.ProjectId, "benchmark.summary", "benchmark-summary.json",
            new { definition = new { definition.Id, definition.Key, definition.Name }, request, runId = run.Id, metrics, comparisons, budget, baselineRunId = baseline?.Id, summary },
            links, "benchmark", ct);
        var samplesArtifact = await _artifacts.StoreJsonAsync(
            request.ProjectId, "benchmark.samples", "raw-samples.json", execution.Samples, links, "benchmark", ct);
        using var executionDocument = JsonDocument.Parse(execution.DetailJson);
        var executionArtifact = await _artifacts.StoreJsonAsync(
            request.ProjectId, "benchmark.execution", "execution-context.json", executionDocument.RootElement.Clone(), links, "benchmark", ct);
        var comparisonArtifact = await _artifacts.StoreJsonAsync(
            request.ProjectId, "benchmark.comparison", "comparison.json",
            new { baselineRunId = baseline?.Id, currentRunId = run.Id, comparisons, budget },
            links, "benchmark", ct);
        return new(summaryArtifact, samplesArtifact, executionArtifact, comparisonArtifact);
    }

    private async Task RecordEvidenceAndIncidentsAsync(
        DurableJob job,
        BenchmarkJobRequest request,
        BenchmarkDefinition definition,
        BenchmarkRun run,
        IReadOnlyList<BenchmarkMetric> metrics,
        IReadOnlyList<BenchmarkComparison> comparisons,
        PerformanceBudgetEvaluationDto? budget,
        BenchmarkState state,
        BenchmarkArtifacts artifacts,
        string summary,
        CancellationToken ct)
    {
        var artifactIds = new[] { artifacts.Summary.Id, artifacts.Samples.Id, artifacts.Execution.Id, artifacts.Comparison.Id };
        await _evidence.AddAsync(
            new EvidenceRecord(
                Guid.NewGuid(), request.ProjectId, null, request.SubjectKey, EvidenceSourceType.Performance,
                $"benchmark.{definition.Key}", state.Outcome, $"Benchmark {definition.Key}: {state.Outcome}", summary,
                comparisons.Count == 0 ? EvidenceConfidence.Medium : EvidenceConfidence.High,
                artifacts.Summary.Id.ToString(),
                JsonSerializer.Serialize(new { run.Id, artifactIds, comparisons, budget }),
                _clock.UtcNow, _clock.UtcNow),
            ct);

        if (state.Failed || state.Warning)
        {
            foreach (var comparison in comparisons.Where(x => x.Verdict is BenchmarkVerdict.Failed or BenchmarkVerdict.Warning))
            {
                var incident = await _incidents.ObserveBenchmarkRegressionAsync(
                    request.ProjectId,
                    request.SubjectKey,
                    $"benchmark:{definition.Key}:{comparison.MetricName}",
                    $"Benchmark regression: {definition.Key} / {comparison.MetricName}",
                    comparison.Verdict == BenchmarkVerdict.Failed ? Severity.Critical : Severity.Warning,
                    job.Id,
                    run.Id,
                    comparison,
                    ct);
                foreach (var artifact in new[] { artifacts.Summary, artifacts.Samples, artifacts.Execution, artifacts.Comparison })
                    await _incidents.AttachArtifactAsync(incident.Id, artifact.Id, artifact.FileName, ct);
            }
        }

        if (state.BudgetFailed || state.BudgetWarning)
        {
            var incident = await _incidents.ObserveSyntheticAsync(
                request.ProjectId,
                request.SubjectKey,
                $"performance-budget:benchmark:{request.PerformanceBudgetProfileId}:{definition.Key}",
                $"Performance budget regression: {definition.Key}",
                state.BudgetFailed ? Severity.Critical : Severity.Warning,
                JsonSerializer.Serialize(new { run.Id, budget }),
                job.Id,
                ct);
            foreach (var artifact in new[] { artifacts.Summary, artifacts.Samples, artifacts.Execution, artifacts.Comparison })
                await _incidents.AttachArtifactAsync(incident.Id, artifact.Id, artifact.FileName, ct);
        }
    }

    private async Task PersistTerminalStateAsync(
        BenchmarkRun run,
        BenchmarkRunStatus status,
        string message,
        Exception? original)
    {
        if (run.Status != BenchmarkRunStatus.Running)
            return;
        run.Complete(status, message, _clock.UtcNow);
        try
        {
            await _uow.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception persistenceException)
        {
            if (original is null)
                throw;
            throw new AggregateException(
                "Benchmark execution failed and its terminal state could not be persisted.",
                original,
                persistenceException);
        }
    }
}
