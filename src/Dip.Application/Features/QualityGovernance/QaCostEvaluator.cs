// Purpose: Measures QA run execution cost and summarizes persisted QA cost history.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;

namespace Dip.Application.Features.QualityGovernance;

public sealed record QaRunCostMeasurement(
    long WallDurationMs,
    long ToolDurationMs,
    long CpuTimeMs,
    long ArtifactBytes,
    decimal CpuSeconds,
    string BudgetVerdict);

public sealed class QaCostEvaluator(IQaGovernanceRepository repository)
{
    public QaRunCostMeasurement Measure(QaRun run, QaPolicyRules rules)
    {
        var toolDuration = ReadDuration(run.SummaryJson, "DurationMs", "durationMs");
        var cpuDuration = ReadDuration(run.SummaryJson, "CpuTimeMs", "cpuTimeMs");
        var wallDuration =
            run.StartedAtUtc.HasValue && run.FinishedAtUtc.HasValue
                ? Math.Max(0L, (long)(run.FinishedAtUtc.Value - run.StartedAtUtc.Value).TotalMilliseconds)
                : toolDuration;
        var artifactBytes = DirectorySize(run.ArtifactRoot);
        var cpuSeconds = decimal.Round(cpuDuration / 1000m, 3);
        var overBudget =
            (rules.MaxRunDurationMs > 0 && wallDuration > rules.MaxRunDurationMs)
            || (rules.MaxCpuSeconds > 0 && cpuSeconds > rules.MaxCpuSeconds)
            || (rules.MaxArtifactBytes > 0 && artifactBytes > rules.MaxArtifactBytes);

        return new(
            wallDuration,
            toolDuration,
            cpuDuration,
            artifactBytes,
            cpuSeconds,
            overBudget ? "OverBudget" : "WithinBudget");
    }

    public async Task<QaCostSummaryDto> GetSummaryAsync(Guid projectId, int take, CancellationToken ct)
    {
        var rows = await repository.ListCostsAsync(projectId, take, ct);
        return new(
            rows.Count,
            rows.Sum(x => x.WallDurationMs),
            rows.Sum(x => x.ToolDurationMs),
            rows.Sum(x => x.ArtifactBytes),
            rows.Sum(x => x.EstimatedCpuSeconds),
            rows.Count(x => x.BudgetVerdict == "OverBudget"));
    }

    private static long ReadDuration(string? json, string primaryProperty, string alternateProperty)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("tools", out var tools)
                || tools.ValueKind != JsonValueKind.Array)
                return 0;

            return tools.EnumerateArray().Sum(tool =>
                tool.TryGetProperty(primaryProperty, out var primary)
                && primary.TryGetInt64(out var value)
                    ? value
                    : tool.TryGetProperty(alternateProperty, out var alternate)
                      && alternate.TryGetInt64(out var alternateValue)
                        ? alternateValue : 0);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"QA summary JSON is invalid; {primaryProperty} cannot be treated as zero.",
                ex);
        }
    }

    private static long DirectorySize(string path)
    {
        try
        {
            return Directory.Exists(path)
                ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    .Select(x => new FileInfo(x).Length).Sum()
                : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                "QA artifact size could not be measured; the cost budget cannot be evaluated safely.",
                ex);
        }
    }
}
