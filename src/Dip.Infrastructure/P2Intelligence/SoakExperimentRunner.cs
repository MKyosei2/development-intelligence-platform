// Purpose: Runs sustained soak experiments and captures runtime drift telemetry.
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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
    
            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var samples = new List<SoakTelemetryPoint>();
            var fpsSamples = new ConcurrentQueue<(int Second, decimal Fps, decimal? Frame)>();
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var stopwatch = Stopwatch.StartNew();
            if (!process.Start())
            {
                await File.WriteAllTextAsync(stderrPath, "Failed to start soak target.", ct);
                return new("InfrastructureFailure", 0, 0, 0, null, null, null, false, false, [], RelativeArtifact(root));
            }
    
            var outTask = ReadLinesAsync(process.StandardOutput, stdout, fpsSamples, stopwatch, ct);
            var errTask = ReadLinesAsync(process.StandardError, stderr, null, stopwatch, ct);
            var duration = Math.Clamp(command.DurationSeconds, 5, 86400);
            var interval = Math.Clamp(command.SampleIntervalSeconds, 1, 60);
            var startMemory = Math.Max(0, process.WorkingSet64 / 1024 / 1024);
            var lastCpu = process.TotalProcessorTime;
            var lastAt = stopwatch.Elapsed;
            var terminatedByHarness = false;
    
            while (!process.HasExited && stopwatch.Elapsed < TimeSpan.FromSeconds(duration))
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), ct);
                if (process.HasExited)
                    break;
                try
                {
                    process.Refresh();
                    var nowCpu = process.TotalProcessorTime;
                    var nowAt = stopwatch.Elapsed;
                    var wall = Math.Max(.001, (nowAt - lastAt).TotalSeconds);
                    var cpu = decimal.Round((decimal)((nowCpu - lastCpu).TotalSeconds / wall / Math.Max(1, Environment.ProcessorCount) * 100d), 2);
                    var second = (int)stopwatch.Elapsed.TotalSeconds;
                    var nearest = fpsSamples.Where(x => Math.Abs(x.Second - second) <= interval * 2).LastOrDefault();
                    samples.Add(new(second, Math.Max(0, process.WorkingSet64 / 1024 / 1024), cpu, nearest.Fps == 0 ? null : nearest.Fps, nearest.Frame));
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
            var allFps = fpsSamples.Select(x => x.Fps).Where(x => x > 0).ToArray();
            decimal? startFps = allFps.Length > 0 ? allFps.Take(Math.Max(1, allFps.Length / 4)).Average() : null;
            decimal? endFps = allFps.Length > 0 ? allFps.Skip(Math.Max(0, allFps.Length * 3 / 4)).DefaultIfEmpty().Average() : null;
            decimal? drift = startFps is > 0m && endFps is not null
                ? decimal.Round((startFps.Value - endFps.Value) / startFps.Value * 100m, 2)
                : null;
            var status = crashed
                ? "Failed"
                : memoryGrowth >= command.MemoryGrowthWarningPercent || drift >= command.PerformanceDriftWarningPercent
                    ? "Warning"
                    : "Passed";
            return new(status, startMemory, endMemory, memoryGrowth, startFps, endFps, drift, crashed, false, samples, RelativeArtifact(root));
        }
}
