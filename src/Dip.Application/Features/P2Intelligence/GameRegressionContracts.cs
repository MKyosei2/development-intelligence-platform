// Purpose: Defines game-performance regression metrics and deterministic evaluation against frame-time and network envelopes.
namespace Dip.Application.Features.P2Intelligence;

public sealed record GameMetricSample(
    string Name,
    decimal Value,
    string Unit);

public sealed record GameRegressionCommand(
    Guid ProjectId,
    string SubjectKey,
    IReadOnlyList<GameMetricSample> Baseline,
    IReadOnlyList<GameMetricSample> Current,
    int TargetFps = 60,
    decimal WarningThresholdPercent = 8m,
    decimal FailureThresholdPercent = 15m);

public sealed record GameMetricRegression(
    string Name,
    decimal? Baseline,
    decimal Current,
    decimal? ChangePercent,
    string Direction,
    string Status,
    string Explanation);

public sealed record GameRegressionResult(
    string Status,
    decimal RiskScore,
    IReadOnlyList<GameMetricRegression> Metrics,
    IReadOnlyList<string> Blockers,
    string Summary);

public sealed class GameRegressionEvaluator
{
    private static readonly string[] HigherIsBetterTokens =
        ["fps", "throughput", "ops", "score"];

    private static readonly string[] LowerIsBetterTokens =
    [
        "frame_time", "frametime", "cpu_ms", "gpu_ms", "latency",
        "memory", "gc", "draw", "triangle", "size", "duration",
        "load", "error", "crash"
    ];

    public GameRegressionResult Evaluate(GameRegressionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var baseline = command.Baseline
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

        var current = command.Current
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

        var metrics = new List<GameMetricRegression>();
        var blockers = new List<string>();
        decimal risk = 0m;

        foreach (var pair in current.OrderBy(
                     x => x.Key,
                     StringComparer.OrdinalIgnoreCase))
        {
            baseline.TryGetValue(pair.Key, out var baselineMetric);

            var lower = IsLowerBetter(pair.Key);
            var higher = IsHigherBetter(pair.Key);
            decimal? change = null;
            decimal regression = 0m;

            if (baselineMetric is not null && baselineMetric.Value != 0m)
            {
                var raw = (pair.Value.Value - baselineMetric.Value)
                          / Math.Abs(baselineMetric.Value) * 100m;
                change = decimal.Round(raw, 2);
                regression = lower ? raw : higher ? -raw : Math.Abs(raw);
            }

            var status = "Passed";
            var explanation = baselineMetric is null
                ? "No baseline is available; current value is recorded without a regression claim."
                : $"Changed {change:+0.##;-0.##;0}% from baseline.";

            if (regression >= command.FailureThresholdPercent)
            {
                status = "Failed";
                risk = Math.Max(risk, 90m);
            }
            else if (regression >= command.WarningThresholdPercent)
            {
                status = "Warning";
                risk = Math.Max(risk, 60m);
            }

            if (IsFrameTime(pair.Key))
            {
                var budget = 1000m / Math.Clamp(command.TargetFps, 1, 1000);
                if (pair.Value.Value > budget)
                {
                    status = pair.Value.Value > budget * 1.15m
                        ? "Failed"
                        : "Warning";
                    blockers.Add(
                        $"{pair.Key} {pair.Value.Value:0.###}{pair.Value.Unit} exceeds "
                        + $"{command.TargetFps} FPS frame budget ({budget:0.###} ms).");
                    risk = Math.Max(risk, status == "Failed" ? 95m : 75m);
                    explanation += $" Target frame budget is {budget:0.###} ms.";
                }
            }

            if (IsFps(pair.Key) && pair.Value.Value < command.TargetFps)
            {
                status = pair.Value.Value < command.TargetFps * .85m
                    ? "Failed"
                    : "Warning";
                blockers.Add(
                    $"{pair.Key} {pair.Value.Value:0.##} is below target "
                    + $"{command.TargetFps} FPS.");
                risk = Math.Max(risk, status == "Failed" ? 95m : 75m);
            }

            if (IsPacketLoss(pair.Key) && pair.Value.Value > 2m)
            {
                status = pair.Value.Value > 5m ? "Failed" : "Warning";
                blockers.Add(
                    $"{pair.Key} {pair.Value.Value:0.##}% exceeds the 2% "
                    + "online-play warning envelope.");
                risk = Math.Max(risk, status == "Failed" ? 90m : 70m);
            }

            if (IsJitter(pair.Key) && pair.Value.Value > 30m)
            {
                status = pair.Value.Value > 60m ? "Failed" : "Warning";
                blockers.Add(
                    $"{pair.Key} {pair.Value.Value:0.##} ms exceeds the 30 ms "
                    + "jitter warning envelope.");
                risk = Math.Max(risk, status == "Failed" ? 90m : 70m);
            }

            if (IsNetworkLatency(pair.Key) && pair.Value.Value > 150m)
            {
                status = pair.Value.Value > 250m ? "Failed" : "Warning";
                blockers.Add(
                    $"{pair.Key} {pair.Value.Value:0.##} ms exceeds the 150 ms "
                    + "latency warning envelope.");
                risk = Math.Max(risk, status == "Failed" ? 90m : 70m);
            }

            metrics.Add(
                new(
                    pair.Key,
                    baselineMetric?.Value,
                    pair.Value.Value,
                    change,
                    lower
                        ? "lower-is-better"
                        : higher
                            ? "higher-is-better"
                            : "absolute-change",
                    status,
                    explanation));
        }

        var failures = metrics.Count(x => x.Status == "Failed");
        var warnings = metrics.Count(x => x.Status == "Warning");
        var overall = failures > 0
            ? "Failed"
            : warnings > 0
                ? "Warning"
                : "Passed";

        if (metrics.Count == 0)
        {
            overall = "InfrastructureFailure";
            blockers.Add("No current game metrics were supplied.");
        }

        return new(
            overall,
            decimal.Round(risk, 2),
            metrics,
            blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            $"Game regression: {failures} failed, {warnings} warning, "
            + $"{metrics.Count - failures - warnings} passed metric(s).");
    }

    private static bool IsLowerBetter(string name) =>
        LowerIsBetterTokens.Any(
            token => name.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static bool IsHigherBetter(string name) =>
        HigherIsBetterTokens.Any(
            token => name.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static bool IsFrameTime(string name) =>
        name.Contains("frame_time", StringComparison.OrdinalIgnoreCase)
        || name.Contains("frametime", StringComparison.OrdinalIgnoreCase);

    private static bool IsFps(string name) =>
        name.Equals("fps", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".fps", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith("_fps", StringComparison.OrdinalIgnoreCase);

    private static bool IsPacketLoss(string name) =>
        name.Contains("packet_loss", StringComparison.OrdinalIgnoreCase)
        || name.Contains("packetloss", StringComparison.OrdinalIgnoreCase);

    private static bool IsJitter(string name) =>
        name.Contains("jitter", StringComparison.OrdinalIgnoreCase);

    private static bool IsNetworkLatency(string name) =>
        (
            name.Contains("network", StringComparison.OrdinalIgnoreCase)
            || name.Contains("rtt", StringComparison.OrdinalIgnoreCase)
            || name.Contains("ping", StringComparison.OrdinalIgnoreCase)
        ) && name.Contains("latency", StringComparison.OrdinalIgnoreCase)
        || name.Equals("rtt_ms", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ping_ms", StringComparison.OrdinalIgnoreCase);
}
