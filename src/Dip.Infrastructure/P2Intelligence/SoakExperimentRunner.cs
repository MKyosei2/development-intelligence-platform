// Purpose: Runs sustained soak experiments and records CPU/RAM plus opt-in GPU, VRAM, GC, and network telemetry emitted by the target.
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

internal sealed class SoakExperimentRunner(ProcessExecutor executor, IQaWorkspaceResolver workspaceResolver)
    : ExperimentExecutionSupport(executor, workspaceResolver)
{
    public async Task<SoakResult> RunSoakAsync(SoakCommand command, CancellationToken ct)
    {
        var workspace = workspaceResolver.Resolve(command.Target.WorkspaceRelativePath);
        var executable = ResolveTargetExecutable(workspace, command.Target.Executable);
        var root = ArtifactRoot(command.Target.ProjectId, "soak");
        var stdoutPath = Path.Combine(root, "stdout.log");
        var stderrPath = Path.Combine(root, "stderr.log");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in command.Target.Arguments ?? Array.Empty<string>())
            startInfo.ArgumentList.Add(argument);
        startInfo.Environment["DIP_SOAK"] = "1";
        startInfo.Environment["DIP_TELEMETRY_SCHEMA"] = "dip.soak.telemetry.v2";

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var samples = new List<SoakTelemetryPoint>();
        var runtimeMetrics = new ConcurrentQueue<ExtendedRuntimeMetric>();
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stopwatch = Stopwatch.StartNew();
        if (!process.Start())
        {
            await File.WriteAllTextAsync(stderrPath, "Failed to start soak target.", ct);
            return new("InfrastructureFailure", 0, 0, 0, null, null, null, false, false, [], RelativeArtifact(root));
        }

        var outTask = ReadTelemetryLinesAsync(process.StandardOutput, stdout, runtimeMetrics, stopwatch, ct);
        var errTask = ReadTelemetryLinesAsync(process.StandardError, stderr, null, stopwatch, ct);
        var duration = Math.Clamp(command.DurationSeconds, 5, 86400);
        var interval = Math.Clamp(command.SampleIntervalSeconds, 1, 60);
        var startMemory = Math.Max(0, process.WorkingSet64 / 1024 / 1024);
        var lastCpu = process.TotalProcessorTime;
        var lastAt = stopwatch.Elapsed;
        var terminatedByHarness = false;

        while (!process.HasExited && stopwatch.Elapsed < TimeSpan.FromSeconds(duration))
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), ct);
            if (process.HasExited) break;
            try
            {
                process.Refresh();
                var nowCpu = process.TotalProcessorTime;
                var nowAt = stopwatch.Elapsed;
                var wall = Math.Max(.001, (nowAt - lastAt).TotalSeconds);
                var cpu = decimal.Round((decimal)((nowCpu - lastCpu).TotalSeconds / wall / Math.Max(1, Environment.ProcessorCount) * 100d), 2);
                var second = (int)stopwatch.Elapsed.TotalSeconds;
                var nearest = runtimeMetrics
                    .Where(x => Math.Abs(x.Second - second) <= interval * 2)
                    .OrderBy(x => Math.Abs(x.Second - second))
                    .FirstOrDefault();
                samples.Add(new(
                    second,
                    Math.Max(0, process.WorkingSet64 / 1024 / 1024),
                    cpu,
                    nearest?.Fps,
                    nearest?.FrameTimeMs));
                lastCpu = nowCpu;
                lastAt = nowAt;
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                break;
            }
        }

        if (!process.HasExited && stopwatch.Elapsed >= TimeSpan.FromSeconds(duration))
        {
            terminatedByHarness = true;
            await TerminateProcessAsync(process);
        }
        if (!process.HasExited)
            await process.WaitForExitAsync(CancellationToken.None);

        await Task.WhenAll(outTask, errTask);
        stopwatch.Stop();
        await File.WriteAllTextAsync(stdoutPath, stdout.ToString(), CancellationToken.None);
        await File.WriteAllTextAsync(stderrPath, stderr.ToString(), CancellationToken.None);

        var exitCode = process.HasExited ? process.ExitCode : (int?)null;
        var crashed = !terminatedByHarness && exitCode is not null && exitCode.Value != 0;
        var endMemory = samples.Count > 0 ? samples[^1].WorkingSetMb : startMemory;
        var memoryGrowth = startMemory <= 0 ? 0m : decimal.Round((decimal)(endMemory - startMemory) / startMemory * 100m, 2);

        var emitted = runtimeMetrics.ToArray();
        var allFps = emitted.Select(x => x.Fps).Where(x => x is > 0m).Select(x => x!.Value).ToArray();
        decimal? startFps = allFps.Length > 0 ? allFps.Take(Math.Max(1, allFps.Length / 4)).Average() : null;
        decimal? endFps = allFps.Length > 0 ? allFps.Skip(Math.Max(0, allFps.Length * 3 / 4)).DefaultIfEmpty().Average() : null;
        decimal? drift = startFps is > 0m && endFps is not null
            ? decimal.Round((startFps.Value - endFps.Value) / startFps.Value * 100m, 2)
            : null;

        var gpuValues = emitted.Select(x => x.GpuFrameTimeMs).Where(x => x is > 0m).Select(x => x!.Value).ToArray();
        var vramValues = emitted.Select(x => x.VramMb).Where(x => x is >= 0m).Select(x => x!.Value).ToArray();
        var gcValues = emitted.Select(x => x.GcAllocMb).Where(x => x is >= 0m).Select(x => x!.Value).ToArray();
        var networkRx = emitted.Select(x => x.NetworkRxKbps).Where(x => x is >= 0m).Select(x => x!.Value).ToArray();
        var networkTx = emitted.Select(x => x.NetworkTxKbps).Where(x => x is >= 0m).Select(x => x!.Value).ToArray();
        var vramGrowth = vramValues.Length >= 2 && vramValues[0] > 0m
            ? decimal.Round((vramValues[^1] - vramValues[0]) / vramValues[0] * 100m, 2)
            : (decimal?)null;
        var gpuDrift = gpuValues.Length >= 2 && gpuValues[0] > 0m
            ? decimal.Round((gpuValues[^1] - gpuValues[0]) / gpuValues[0] * 100m, 2)
            : (decimal?)null;

        var extendedArtifact = Path.Combine(root, "soak-telemetry-v2.json");
        await File.WriteAllTextAsync(
            extendedArtifact,
            JsonSerializer.Serialize(new
            {
                schema = "dip.soak.telemetry.v2",
                durationSeconds = stopwatch.Elapsed.TotalSeconds,
                process = new { startMemoryMb = startMemory, endMemoryMb = endMemory, memoryGrowthPercent = memoryGrowth },
                fps = new { start = startFps, end = endFps, driftPercent = drift },
                gpu = new
                {
                    sampleCount = gpuValues.Length,
                    averageFrameTimeMs = gpuValues.Length == 0 ? (decimal?)null : decimal.Round(gpuValues.Average(), 3),
                    maxFrameTimeMs = gpuValues.Length == 0 ? (decimal?)null : gpuValues.Max(),
                    driftPercent = gpuDrift
                },
                vram = new
                {
                    sampleCount = vramValues.Length,
                    startMb = vramValues.FirstOrDefault(),
                    endMb = vramValues.LastOrDefault(),
                    maxMb = vramValues.Length == 0 ? (decimal?)null : vramValues.Max(),
                    growthPercent = vramGrowth
                },
                gc = new
                {
                    sampleCount = gcValues.Length,
                    maxAllocMb = gcValues.Length == 0 ? (decimal?)null : gcValues.Max(),
                    averageAllocMb = gcValues.Length == 0 ? (decimal?)null : decimal.Round(gcValues.Average(), 4)
                },
                network = new
                {
                    rxSampleCount = networkRx.Length,
                    txSampleCount = networkTx.Length,
                    averageRxKbps = networkRx.Length == 0 ? (decimal?)null : decimal.Round(networkRx.Average(), 3),
                    averageTxKbps = networkTx.Length == 0 ? (decimal?)null : decimal.Round(networkTx.Average(), 3)
                },
                telemetry = emitted.Take(20000).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true }),
            CancellationToken.None);

        var telemetryDriftWarning = gpuDrift >= command.PerformanceDriftWarningPercent
                                    || vramGrowth >= command.MemoryGrowthWarningPercent;
        var status = crashed
            ? "Failed"
            : memoryGrowth >= command.MemoryGrowthWarningPercent
              || drift >= command.PerformanceDriftWarningPercent
              || telemetryDriftWarning
                ? "Warning"
                : "Passed";
        return new(status, startMemory, endMemory, memoryGrowth, startFps, endFps, drift, crashed, false, samples, RelativeArtifact(root));
    }

    private static async Task ReadTelemetryLinesAsync(
        StreamReader reader,
        StringBuilder sink,
        ConcurrentQueue<ExtendedRuntimeMetric>? metrics,
        Stopwatch stopwatch,
        CancellationToken ct)
    {
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            sink.AppendLine(line);
            if (metrics is not null && TryExtendedMetric(line, (int)stopwatch.Elapsed.TotalSeconds, out var metric))
                metrics.Enqueue(metric);
        }
    }

    private static bool TryExtendedMetric(string line, int second, out ExtendedRuntimeMetric metric)
    {
        metric = new ExtendedRuntimeMetric(second, null, null, null, null, null, null, null, null, null);
        if (!line.Contains("DIP_METRIC", StringComparison.OrdinalIgnoreCase)) return false;
        decimal? fps = null, frame = null, gpu = null, vram = null, gc = null, rx = null, tx = null, rtt = null, packetLoss = null;
        foreach (var token in line.Split([' ', ';', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split('=', 2);
            if (parts.Length != 2 || !decimal.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                continue;
            switch (parts[0].Trim().ToLowerInvariant())
            {
                case "fps": fps = value; break;
                case "frame_time_ms": frame = value; break;
                case "gpu_ms":
                case "gpu_frame_time_ms": gpu = value; break;
                case "vram_mb": vram = value; break;
                case "gc_alloc_mb":
                case "gc_alloc_mb_per_frame": gc = value; break;
                case "net_rx_kbps": rx = value; break;
                case "net_tx_kbps": tx = value; break;
                case "network_rtt_ms":
                case "rtt_ms": rtt = value; break;
                case "packet_loss_percent": packetLoss = value; break;
            }
        }
        metric = new ExtendedRuntimeMetric(second, fps, frame, gpu, vram, gc, rx, tx, rtt, packetLoss);
        return fps is not null || frame is not null || gpu is not null || vram is not null || gc is not null || rx is not null || tx is not null || rtt is not null || packetLoss is not null;
    }

    private sealed record ExtendedRuntimeMetric(
        int Second,
        decimal? Fps,
        decimal? FrameTimeMs,
        decimal? GpuFrameTimeMs,
        decimal? VramMb,
        decimal? GcAllocMb,
        decimal? NetworkRxKbps,
        decimal? NetworkTxKbps,
        decimal? NetworkRttMs,
        decimal? PacketLossPercent);
}
