// Purpose: Contains deterministic benchmark metric aggregation, comparison, validation, and mapping helpers.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Benchmarks;

public sealed partial class BenchmarkOrchestrationService
{
    private async Task<List<BenchmarkComparison>> CompareAsync(
        Guid currentRunId,
        Guid baselineRunId,
        IReadOnlyList<BenchmarkMetric> current,
        BenchmarkExecutionSpec spec,
        CancellationToken ct)
    {
        var baseline = (await _repo.ListMetricsAsync(baselineRunId, ct))
            .ToDictionary(x => $"{x.Name}:{x.Aggregation}", StringComparer.OrdinalIgnoreCase);
        var list = new List<BenchmarkComparison>();
        foreach (var metric in current.Where(x => x.Name is "latency" or "error_rate" or "throughput"))
        {
            if (!baseline.TryGetValue($"{metric.Name}:{metric.Aggregation}", out var old))
                continue;
            decimal regression;
            if (old.Value == 0)
            {
                if (metric.Name != "error_rate")
                    continue;
                regression = metric.Value <= 0 ? 0m : 100m;
            }
            else
            {
                var raw = (metric.Value - old.Value) / Math.Abs(old.Value) * 100m;
                regression = metric.Name == "throughput" ? -raw : raw;
            }
            var verdict = regression >= spec.FailureRegressionPercent
                ? BenchmarkVerdict.Failed
                : regression >= spec.WarningRegressionPercent
                    ? BenchmarkVerdict.Warning
                    : BenchmarkVerdict.Passed;
            list.Add(new BenchmarkComparison(
                Guid.NewGuid(), currentRunId, baselineRunId, $"{metric.Name}.{metric.Aggregation}",
                old.Value, metric.Value, regression, verdict,
                $"{metric.Name} changed by {regression:+0.###;-0.###;0}% relative to the matching environment/configuration baseline."));
        }
        return list;
    }

    private static List<BenchmarkMetric> BuildMetrics(Guid runId, IReadOnlyList<BenchmarkSample> samples)
    {
        var durations = samples.Where(x => x.Passed)
            .Select(x => (decimal)x.DurationMicroseconds / 1000m)
            .OrderBy(x => x)
            .ToArray();
        var total = Math.Max(1, samples.Count);
        var errors = (decimal)samples.Count(x => !x.Passed) / total * 100m;
        var totalMeasuredMs = samples.Sum(x => (decimal)x.DurationMicroseconds / 1000m);
        var throughput = totalMeasuredMs <= 0 ? 0m : decimal.Round(samples.Count * 1000m / totalMeasuredMs, 4);
        return
        [
            new(Guid.NewGuid(), runId, "latency", Percentile(durations, .50m), "ms", "p50"),
            new(Guid.NewGuid(), runId, "latency", Percentile(durations, .95m), "ms", "p95"),
            new(Guid.NewGuid(), runId, "latency", Percentile(durations, .99m), "ms", "p99"),
            new(Guid.NewGuid(), runId, "error_rate", decimal.Round(errors, 4), "percent", "rate"),
            new(Guid.NewGuid(), runId, "throughput", throughput, "ops/s", "observed")
        ];
    }

    private static decimal Percentile(decimal[] sorted, decimal percentile)
    {
        if (sorted.Length == 0)
            return 0m;
        var index = (sorted.Length - 1) * percentile;
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper)
            return decimal.Round(sorted[lower], 4);
        var fraction = index - lower;
        return decimal.Round(sorted[lower] + (sorted[upper] - sorted[lower]) * fraction, 4);
    }

    private static void Validate(BenchmarkExecutionSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.Target))
            throw new ArgumentException("Benchmark target is required.");
        if (spec.Mode is null || (!spec.Mode.Equals("http", StringComparison.OrdinalIgnoreCase) && !spec.Mode.Equals("process", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Benchmark mode must be http or process.");
        if (spec.MeasurementIterations < 3)
            throw new ArgumentOutOfRangeException(nameof(spec.MeasurementIterations));
        if (spec.FailureRegressionPercent < spec.WarningRegressionPercent)
            throw new ArgumentException("Failure regression threshold must be >= warning threshold.");
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static BenchmarkDefinitionDto Map(BenchmarkDefinition entity) =>
        new(entity.Id, entity.Key, entity.Name, entity.Kind, entity.Enabled, JsonSerializer.Deserialize<BenchmarkExecutionSpec>(entity.ConfigurationJson)!);

    private static BenchmarkMetricDto Map(BenchmarkMetric metric) =>
        new(metric.Name, metric.Value, metric.Unit, metric.Aggregation);

    private static BenchmarkComparisonDto Map(BenchmarkComparison comparison) =>
        new(comparison.MetricName, comparison.BaselineValue, comparison.CurrentValue, comparison.DifferencePercent, comparison.Verdict.ToString(), comparison.Explanation);
}
