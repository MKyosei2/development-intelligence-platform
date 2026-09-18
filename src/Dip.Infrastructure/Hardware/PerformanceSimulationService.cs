// Purpose: Implements infrastructure support for P er fo rm an ce Si mu la ti on Se rv ic e.
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Features.Hardware;
using Dip.Infrastructure.QualityAutomation;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Hardware;

/// <summary>
/// Executes a target under calibrated synthetic constraints. The result is
/// deliberately marked as simulated evidence: it is useful for finding likely
/// low-end failures early, but it never substitutes for a real device.
/// </summary>
public sealed partial class PerformanceSimulationService(
    HardwareConstraintRunner constraints,
    IOptions<QaExecutionOptions> options) : IPerformanceSimulationRunner
{
    public async Task<PerformanceSimulationMeasurement> RunAsync(
        HardwareProfileDto profile,
        string workspace,
        string executable,
        IReadOnlyList<string> arguments,
        int durationSeconds,
        bool thermalSimulation,
        string artifactDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(artifactDirectory);
        var target = ReadRelativeTarget(profile.MetadataJson);
        var cpu = Math.Clamp(target.CpuPercent, 1m, 100m);
        var gpu = Math.Clamp(target.GpuPercent, 1m, 100m);
        var memoryBandwidth = Math.Clamp(target.MemoryBandwidthPercent, 1m, 100m);
        var storage = Math.Clamp(target.StoragePercent, 1m, 100m);

        var stopGpu = Path.Combine(artifactDirectory, "gpu-stress.stop");
        Process? gpuStress = StartGpuStress(100m - gpu, stopGpu, workspace);

        using var pressureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var memoryPressureApplied = memoryBandwidth < 99m;
        var storagePressureApplied = storage < 99m;
        Task? memoryPressure = memoryPressureApplied
            ? RunMemoryBandwidthPressureAsync(100m - memoryBandwidth, pressureCts.Token)
            : null;
        Task? storagePressure = storagePressureApplied
            ? RunStoragePressureAsync(100m - storage, artifactDirectory, pressureCts.Token)
            : null;

        var stdout = Path.Combine(artifactDirectory, "stdout.log");
        var stderr = Path.Combine(artifactDirectory, "stderr.log");
        ConstrainedProcessResult process;

        try
        {
            process = await constraints.RunAsync(
                executable,
                arguments,
                workspace,
                TimeSpan.FromSeconds(Math.Clamp(durationSeconds, 5, 3600)),
                cpu,
                profile.MemoryBudgetMb,
                thermalSimulation ? profile.ThermalScore : 100m,
                stdout,
                stderr,
                cancellationToken);
        }
        finally
        {
            pressureCts.Cancel();
            await IgnoreCancellationAsync(memoryPressure);
            await IgnoreCancellationAsync(storagePressure);

            try { await File.WriteAllTextAsync(stopGpu, "stop", CancellationToken.None); } catch { }
            if (gpuStress is not null)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await gpuStress.WaitForExitAsync(cts.Token);
                }
                catch
                {
                    try { gpuStress.Kill(true); } catch { }
                }
                finally
                {
                    gpuStress.Dispose();
                }
            }
        }

        var fps = ParseNumbers(FpsRegex(), process.StandardOutput);
        var frame = ParseNumbers(FrameTimeRegex(), process.StandardOutput);
        decimal? averageFps = fps.Count > 0
            ? decimal.Round(fps.Average(), 2)
            : null;
        decimal? p95 = frame.Count > 0
            ? Percentile(frame, 0.95m)
            : null;

        var memoryFail = process.PeakWorkingSetMb > profile.MemoryBudgetMb;
        var fpsFail = averageFps.HasValue && averageFps.Value < profile.TargetFps;
        var frameBudget = 1000m / profile.TargetFps;
        var frameFail = p95.HasValue && p95.Value > frameBudget;
        var processFail = process.ExitCode.HasValue && process.ExitCode.Value != 0;

        var status = processFail || memoryFail || fpsFail || frameFail
            ? "Failed"
            : averageFps is null && p95 is null
                ? "Warning"
                : "Passed";

        var confidence = 40m
            + (process.CpuLimitApplied ? 15m : 0m)
            + (process.MemoryLimitApplied ? 10m : 0m)
            + (gpuStress is not null ? 10m : 0m)
            + (memoryPressureApplied ? 5m : 0m)
            + (storagePressureApplied ? 5m : 0m)
            + (averageFps is not null || p95 is not null ? 5m : 0m);
        confidence = Math.Min(85m, confidence);

        var result = new
        {
            status,
            confidencePercent = confidence,
            simulated = true,
            realDevice = false,
            target = new
            {
                profile.Id,
                profile.Name,
                profile.Platform,
                cpuPercent = cpu,
                gpuPercent = gpu,
                memoryBandwidthPercent = memoryBandwidth,
                storagePercent = storage,
                profile.MemoryBudgetMb,
                profile.TargetFps,
                profile.ThermalScore
            },
            process = new
            {
                process.ExitCode,
                process.ObservationWindowCompleted,
                process.DurationMs,
                process.PeakWorkingSetMb,
                process.CpuLimitApplied,
                process.MemoryLimitApplied,
                process.ThermalCurveApplied,
                gpuStressApplied = gpuStress is not null,
                memoryBandwidthContentionApplied = memoryPressureApplied,
                storageContentionApplied = storagePressureApplied
            },
            metrics = new
            {
                averageFps,
                p95FrameTimeMs = p95,
                frameBudgetMs = frameBudget
            },
            limitations = new[]
            {
                "CPU limiting uses a process budget; it does not emulate another CPU microarchitecture or ISA.",
                "GPU limiting uses synthetic contention, not GPU architecture or driver emulation.",
                "Memory-bandwidth and storage limiting use synthetic contention and are approximate.",
                "OS, driver, mobile power-management and mobile thermal behavior are not perfectly reproduced.",
                "Real-device evidence is still required for release certification."
            }
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        await File.WriteAllTextAsync(
            Path.Combine(artifactDirectory, "performance.json"),
            json,
            cancellationToken);

        return new(
            status,
            confidence,
            process.TimedOut,
            process.ExitCode,
            process.DurationMs,
            process.PeakWorkingSetMb,
            averageFps,
            p95,
            artifactDirectory,
            json);
    }

    private Process? StartGpuStress(decimal load, string stopFile, string workspace)
    {
        if (!OperatingSystem.IsWindows() || load < 1m) return null;
        try
        {
            var configured = options.Value.HardwareStressAgentPath;
            var path = Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(configured);
            if (!File.Exists(path)) return null;

            var psi = new ProcessStartInfo
            {
                FileName = path,
                WorkingDirectory = workspace,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--load-percent");
            psi.ArgumentList.Add(
                Math.Clamp(load, 1m, 95m).ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("--stop-file");
            psi.ArgumentList.Add(stopFile);
            return Process.Start(psi);
        }
        catch
        {
            return null;
        }
    }

    private static async Task RunMemoryBandwidthPressureAsync(
        decimal loadPercent,
        CancellationToken cancellationToken)
    {
        var load = Math.Clamp((double)loadPercent, 1d, 90d);
        var source = new byte[16 * 1024 * 1024];
        var target = new byte[source.Length];
        Random.Shared.NextBytes(source);

        while (!cancellationToken.IsCancellationRequested)
        {
            var cycle = Stopwatch.StartNew();
            var busy = TimeSpan.FromMilliseconds(load);
            while (cycle.Elapsed < busy && !cancellationToken.IsCancellationRequested)
                Buffer.BlockCopy(source, 0, target, 0, source.Length);

            var rest = Math.Max(
                1d,
                100d - load - cycle.Elapsed.TotalMilliseconds);
            await Task.Delay(TimeSpan.FromMilliseconds(rest), cancellationToken);
        }
    }

    private static async Task RunStoragePressureAsync(
        decimal loadPercent,
        string artifactDirectory,
        CancellationToken cancellationToken)
    {
        var load = Math.Clamp((double)loadPercent, 1d, 80d);
        var path = Path.Combine(artifactDirectory, "storage-contention.tmp");
        var buffer = new byte[1024 * 1024];
        Random.Shared.NextBytes(buffer);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var cycle = Stopwatch.StartNew();
                var busy = TimeSpan.FromMilliseconds(load);
                while (cycle.Elapsed < busy && !cancellationToken.IsCancellationRequested)
                {
                    await using (var write = new FileStream(
                        path,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read,
                        buffer.Length,
                        FileOptions.Asynchronous | FileOptions.WriteThrough))
                    {
                        for (var i = 0; i < 8; i++)
                            await write.WriteAsync(buffer, cancellationToken);
                    }

                    await using var read = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite,
                        buffer.Length,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    while (await read.ReadAsync(buffer, cancellationToken) > 0) { }
                }

                var rest = Math.Max(
                    1d,
                    100d - load - cycle.Elapsed.TotalMilliseconds);
                await Task.Delay(TimeSpan.FromMilliseconds(rest), cancellationToken);
            }
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static async Task IgnoreCancellationAsync(Task? task)
    {
        if (task is null) return;
        try { await task; }
        catch (OperationCanceledException) { }
        catch { }
    }

    private static RelativeTarget ReadRelativeTarget(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            decimal Get(string first, string second, decimal fallback)
            {
                if (root.TryGetProperty(first, out var p1)
                    && p1.TryGetDecimal(out var n1))
                    return n1;
                if (root.TryGetProperty(second, out var p2)
                    && p2.TryGetDecimal(out var n2))
                    return n2;
                return fallback;
            }

            var common = Get("percent", "Percent", 100m);
            return new(
                Get("CpuPercent", "cpuPercent", common),
                Get("GpuPercent", "gpuPercent", common),
                Get("MemoryBandwidthPercent", "memoryBandwidthPercent", common),
                Get("StoragePercent", "storagePercent", common));
        }
        catch
        {
            return new(100m, 100m, 100m, 100m);
        }
    }

    private static List<decimal> ParseNumbers(Regex regex, string text) =>
        regex.Matches(text)
            .Select(m => decimal.TryParse(
                m.Groups[1].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var n) ? (decimal?)n : null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Where(x => x >= 0 && x < 100000)
            .ToList();

    private static decimal Percentile(List<decimal> values, decimal p)
    {
        var ordered = values.OrderBy(x => x).ToArray();
        var index = (int)Math.Ceiling((double)(p * (ordered.Length - 1)));
        return decimal.Round(
            ordered[Math.Clamp(index, 0, ordered.Length - 1)],
            2);
    }

    private sealed record RelativeTarget(
        decimal CpuPercent,
        decimal GpuPercent,
        decimal MemoryBandwidthPercent,
        decimal StoragePercent);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    [GeneratedRegex(@"(?im)\bfps\s*[:=]\s*([0-9]+(?:\.[0-9]+)?)")]
    private static partial Regex FpsRegex();

    [GeneratedRegex(@"(?im)\b(?:frame[_ ]?time(?:_ms)?|frametime)\s*[:=]\s*([0-9]+(?:\.[0-9]+)?)")]
    private static partial Regex FrameTimeRegex();
}
