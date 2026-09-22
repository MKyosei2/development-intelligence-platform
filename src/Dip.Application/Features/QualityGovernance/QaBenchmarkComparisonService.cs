// Purpose: Builds descriptive cross-project QA benchmark comparisons from equivalent benchmark definitions.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;

namespace Dip.Application.Features.QualityGovernance;

public sealed class QaBenchmarkComparisonService(
    IBenchmarkRepository benchmarks,
    IProjectRepository projects)
{
    public async Task<CrossProjectBenchmarkComparisonDto> CompareProjectsAsync(
        string definitionKey,
        string metricName,
        string aggregation,
        int take,
        CancellationToken ct)
    {
        var definition = await benchmarks.GetDefinitionByKeyAsync(definitionKey, ct)
            ?? throw new KeyNotFoundException($"Benchmark definition '{definitionKey}' not found.");
        var runs = await benchmarks.ListSucceededRunsAsync(
            definition.Id, Math.Clamp(take * 20, 20, 5000), ct);
        var latest = runs.GroupBy(x => x.ProjectId)
            .Select(group => group.OrderByDescending(x => x.FinishedAtUtc).First())
            .Take(Math.Clamp(take, 1, 100))
            .ToArray();

        var rows = new List<(BenchmarkRun Run, decimal Value, string Unit)>();
        foreach (var run in latest)
        {
            var metrics = await benchmarks.ListMetricsAsync(run.Id, ct);
            var metric = metrics.FirstOrDefault(x =>
                x.Name.Equals(metricName, StringComparison.OrdinalIgnoreCase)
                && x.Aggregation.Equals(aggregation, StringComparison.OrdinalIgnoreCase));
            if (metric is not null)
                rows.Add((run, metric.Value, metric.Unit));
        }

        if (rows.Count == 0)
            return new(
                definitionKey,
                $"{metricName}.{aggregation}",
                [],
                "No comparable successful benchmark measurements exist.");

        var higherIsBetter = metricName.Equals("throughput", StringComparison.OrdinalIgnoreCase);
        var best = higherIsBetter ? rows.Max(x => x.Value) : rows.Min(x => x.Value);
        var result = new List<CrossProjectBenchmarkMetricDto>();

        foreach (var row in rows)
        {
            var project = await projects.GetByIdAsync(row.Run.ProjectId, ct);
            decimal difference;
            if (best == 0)
                difference = row.Value == 0 ? 0m : 100m;
            else
                difference = higherIsBetter
                    ? (best - row.Value) / Math.Abs(best) * 100m
                    : (row.Value - best) / Math.Abs(best) * 100m;

            result.Add(new(
                row.Run.ProjectId,
                project?.Name ?? row.Run.ProjectId.ToString("N"),
                row.Run.Id,
                row.Run.CommitSha,
                row.Run.EnvironmentFingerprint,
                row.Value,
                row.Unit,
                aggregation,
                decimal.Round(difference, 2)));
        }

        return new(
            definitionKey,
            $"{metricName}.{aggregation}",
            result.OrderBy(x => x.DifferenceFromBestPercent).ToArray(),
            "Cross-project results are descriptive only. Compare identical benchmark definitions/configuration and equivalent hardware environments before drawing performance conclusions.");
    }
}
