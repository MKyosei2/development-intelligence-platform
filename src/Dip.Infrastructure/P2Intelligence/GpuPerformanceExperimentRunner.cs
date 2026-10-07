// Purpose: Runs deterministic GPU performance experiments and fails closed when engine GPU telemetry is unavailable.
using System.Globalization;
using System.Text.Json;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

public sealed record GpuPerformanceCommand(
    ExperimentTarget Target,
    decimal WarningP95GpuFrameMs = 15m,
    decimal FailureP95GpuFrameMs = 16.6667m,
    long WarningVramMb = 0,
    long FailureVramMb = 0,
    decimal HitchThresholdMs = 33.333m,
    int MaximumHitches = 0);

public sealed record GpuPerformanceSample(
    long Sequence,
    decimal GpuFrameMs,
    long? VramMb,
    long? DrawCalls,
    long? Triangles,
    long? ShaderVariants);

public sealed record GpuPerformanceResult(
    string Status,
    int Samples,
    decimal P50GpuFrameMs,
    decimal P95GpuFrameMs,
    decimal P99GpuFrameMs,
    decimal MaxGpuFrameMs,
    long? PeakVramMb,
    long? PeakDrawCalls,
    long? PeakTriangles,
    long? PeakShaderVariants,
    int Hitches,
    string ArtifactReference,
    string Summary);

public interface IGpuPerformanceExperimentRunner
{
    Task<GpuPerformanceResult> RunAsync(GpuPerformanceCommand command, CancellationToken ct);
}

public sealed class GpuPerformanceExperimentRunner(
    ProcessExecutor executor,
    IQaWorkspaceResolver workspaceResolver) : IGpuPerformanceExperimentRunner
{
    public async Task<GpuPerformanceResult> RunAsync(GpuPerformanceCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var workspace = workspaceResolver.Resolve(command.Target.WorkspaceRelativePath);
        var executable = ResolveExecutable(workspace, command.Target.Executable);
        var root = Path.Combine(
            Path.GetFullPath("artifacts"),
            "p2",
            command.Target.ProjectId.ToString("N"),
            "gpu-performance",
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var stdout = Path.Combine(root, "stdout.log");
        var stderr = Path.Combine(root, "stderr.log");
        var environment = new Dictionary<string, string>
        {
            ["DIP_GPU_QA"] = "1",
            ["DIP_GPU_METRIC_FORMAT"] = "v1"
        };

        var result = await executor.RunQaAsync(
            executable,
            command.Target.Arguments ?? Array.Empty<string>(),
            workspace,
            environment,
            TimeSpan.FromSeconds(Math.Clamp(command.Target.TimeoutSeconds, 1, 7200)),
            stdout,
            stderr,
            ct);

        if (result.TimedOut || result.ExitCode is not 0)
        {
            var status = result.TimedOut ? "Failed" : result.ExitCode is null ? "InfrastructureFailure" : "Failed";
            var summary = result.TimedOut
                ? "GPU performance target timed out."
                : $"GPU performance target exited with code {(result.ExitCode?.ToString() ?? "n/a")}.";
            await WriteEvidenceAsync(root, command, result, Array.Empty<GpuPerformanceSample>(), status, summary, ct);
            return new(status, 0, 0, 0, 0, 0, null, null, null, null, 0, Relative(root), summary);
        }

        var samples = ParseSamples(result.StandardOutput)
            .Concat(ParseSamples(result.StandardError))
            .GroupBy(x => x.Sequence)
            .Select(x => x.Last())
            .OrderBy(x => x.Sequence)
            .ToArray();
        if (samples.Length == 0)
        {
            const string summary = "Target completed without DIP_GPU_METRIC telemetry; GPU performance is NOT_EVALUATED.";
            await WriteEvidenceAsync(root, command, result, samples, "InfrastructureFailure", summary, ct);
            return new("InfrastructureFailure", 0, 0, 0, 0, 0, null, null, null, null, 0, Relative(root), summary);
        }

        var frame = samples.Select(x => x.GpuFrameMs).OrderBy(x => x).ToArray();
        var p50 = Percentile(frame, 0.50m);
        var p95 = Percentile(frame, 0.95m);
        var p99 = Percentile(frame, 0.99m);
        var max = frame[^1];
        var peakVram = MaxNullable(samples.Select(x => x.VramMb));
        var peakDrawCalls = MaxNullable(samples.Select(x => x.DrawCalls));
        var peakTriangles = MaxNullable(samples.Select(x => x.Triangles));
        var peakShaderVariants = MaxNullable(samples.Select(x => x.ShaderVariants));
        var hitches = samples.Count(x => x.GpuFrameMs >= command.HitchThresholdMs);

        var failed = p95 > command.FailureP95GpuFrameMs
                     || command.FailureVramMb > 0 && peakVram > command.FailureVramMb
                     || hitches > Math.Max(0, command.MaximumHitches);
        var warning = !failed && (
            p95 > command.WarningP95GpuFrameMs
            || command.WarningVramMb > 0 && peakVram > command.WarningVramMb);
        var status = failed ? "Failed" : warning ? "Warning" : "Passed";
        var summaryText =
            $"GPU p95={p95:0.###}ms, p99={p99:0.###}ms, max={max:0.###}ms, "
            + $"VRAM={(peakVram?.ToString(CultureInfo.InvariantCulture) ?? "n/a")}MiB, hitches={hitches}/{samples.Length}.";
        await WriteEvidenceAsync(root, command, result, samples, status, summaryText, ct);
        return new(
            status,
            samples.Length,
            p50,
            p95,
            p99,
            max,
            peakVram,
            peakDrawCalls,
            peakTriangles,
            peakShaderVariants,
            hitches,
            Relative(root),
            summaryText);
    }

    private static IReadOnlyList<GpuPerformanceSample> ParseSamples(string text)
    {
        var output = new List<GpuPerformanceSample>();
        long autoSequence = 0;
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains("DIP_GPU_METRIC", StringComparison.OrdinalIgnoreCase))
                continue;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in line.Split([' ', ';', ','], StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split('=', 2);
                if (parts.Length == 2)
                    values[parts[0].Trim()] = parts[1].Trim();
            }
            if (!TryDecimal(values, "gpu_frame_ms", out var gpuFrame) || gpuFrame < 0)
                continue;
            var sequence = TryLong(values, "frame", out var explicitFrame) ? explicitFrame : autoSequence++;
            output.Add(new(
                sequence,
                gpuFrame,
                NullableLong(values, "vram_mb"),
                NullableLong(values, "draw_calls"),
                NullableLong(values, "triangles"),
                NullableLong(values, "shader_variants")));
        }
        return output;
    }

    private static decimal Percentile(decimal[] sorted, decimal percentile)
    {
        if (sorted.Length == 0) return 0m;
        if (sorted.Length == 1) return sorted[0];
        var position = (sorted.Length - 1) * percentile;
        var lo = (int)Math.Floor(position);
        var hi = (int)Math.Ceiling(position);
        if (lo == hi) return sorted[lo];
        var fraction = position - lo;
        return decimal.Round(sorted[lo] + (sorted[hi] - sorted[lo]) * fraction, 4);
    }

    private static long? MaxNullable(IEnumerable<long?> values)
    {
        var actual = values.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        return actual.Length == 0 ? null : actual.Max();
    }

    private static bool TryDecimal(IReadOnlyDictionary<string, string> values, string key, out decimal result)
        => values.TryGetValue(key, out var text)
           && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static bool TryLong(IReadOnlyDictionary<string, string> values, string key, out long result)
        => values.TryGetValue(key, out var text)
           && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static long? NullableLong(IReadOnlyDictionary<string, string> values, string key)
        => TryLong(values, key, out var value) ? value : null;

    private static string ResolveExecutable(string workspace, string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
            throw new ArgumentException("GPU performance executable is required.", nameof(executable));
        if (Path.IsPathRooted(executable))
            return executable;
        var local = Path.GetFullPath(Path.Combine(workspace, executable));
        return File.Exists(local) ? local : executable;
    }

    private static async Task WriteEvidenceAsync(
        string root,
        GpuPerformanceCommand command,
        ProcessExecutionResult execution,
        IReadOnlyList<GpuPerformanceSample> samples,
        string status,
        string summary,
        CancellationToken ct)
    {
        var payload = new
        {
            schema = "dip.gpu-performance.v1",
            status,
            summary,
            command = new
            {
                command.WarningP95GpuFrameMs,
                command.FailureP95GpuFrameMs,
                command.WarningVramMb,
                command.FailureVramMb,
                command.HitchThresholdMs,
                command.MaximumHitches
            },
            execution = new
            {
                execution.ExitCode,
                execution.TimedOut,
                execution.DurationMs,
                execution.CpuTimeMs,
                execution.Sandbox
            },
            samples
        };
        await File.WriteAllTextAsync(
            Path.Combine(root, "gpu-performance.json"),
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
            ct);
    }

    private static string Relative(string full)
        => Path.GetRelativePath(Path.GetFullPath("."), full).Replace('\\', '/');
}
