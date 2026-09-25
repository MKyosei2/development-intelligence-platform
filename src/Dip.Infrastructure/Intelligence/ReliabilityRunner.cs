// Purpose: Implements R el ia bi li ty Ru nn er infrastructure behavior for i nt el li ge nc e.
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Features.Intelligence;
using Dip.Application.Features.QualityAutomation;

namespace Dip.Infrastructure.Intelligence;

public sealed partial class ReliabilityRunner : IReliabilityRunner
{
    private readonly IQaWorkspaceResolver _workspace;

    public ReliabilityRunner(IQaWorkspaceResolver workspace)
    {
        _workspace = workspace;
    }

    public async Task<ReliabilityResult> RunAsync(
        ReliabilityRequest request,
        string artifactRoot,
        CancellationToken cancellationToken)
    {
        var workspace = _workspace.Resolve(request.WorkspaceRelativePath);
        if (Path.IsPathRooted(request.Executable))
            throw new InvalidOperationException("Executable must be workspace-relative.");

        var executable = Path.GetFullPath(Path.Combine(workspace, request.Executable));
        var prefix = workspace.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!executable.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(executable))
        {
            throw new InvalidOperationException(
                "Reliability executable escapes the configured workspace or does not exist.");
        }

        Directory.CreateDirectory(artifactRoot);

        var soakMode = request.SoakMinutes > 0;
        var repeat = soakMode ? 1 : Math.Clamp(request.RepeatCount, 3, 200);
        var soakWindow = TimeSpan.FromMinutes(Math.Clamp(request.SoakMinutes, 1, 720));
        var normalTimeout = TimeSpan.FromMinutes(10);

        var passes = 0;
        var failures = 0;
        var timeouts = 0;
        var durations = new List<double>();
        var allWorkingSetSamplesMb = new List<decimal>();
        var allPrivateMemorySamplesMb = new List<decimal>();
        var fpsSamples = new List<decimal>();
        var frameSamples = new List<decimal>();
        var runDetails = new List<object>();

        for (var runIndex = 0; runIndex < repeat; runIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var psi = new ProcessStartInfo(executable)
            {
                WorkingDirectory = workspace,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in request.Arguments)
                psi.ArgumentList.Add(argument);

            using var process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };
            if (!process.Start())
                throw new InvalidOperationException("Target unavailable.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var stopwatch = Stopwatch.StartNew();
            var deadline = DateTimeOffset.UtcNow + (soakMode ? soakWindow : normalTimeout);
            var workingSetSamplesMb = new List<decimal>();
            var privateMemorySamplesMb = new List<decimal>();
            var observationWindowCompleted = false;
            var timedOut = false;
            int? exitCode = null;

            try
            {
                while (!process.HasExited)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        process.Refresh();
                        workingSetSamplesMb.Add(
                            decimal.Round(process.WorkingSet64 / 1024m / 1024m, 2));
                        privateMemorySamplesMb.Add(
                            decimal.Round(process.PrivateMemorySize64 / 1024m / 1024m, 2));
                    }
                    catch
                    {
                        // Sampling is best effort; process liveness remains authoritative.
                    }

                    if (DateTimeOffset.UtcNow >= deadline)
                    {
                        if (soakMode)
                        {
                            observationWindowCompleted = true;
                        }
                        else
                        {
                            timedOut = true;
                        }

                        try { process.Kill(true); } catch {
                            // Intentionally ignored to preserve the existing best-effort or fallback behavior.
                        }
                        try { await process.WaitForExitAsync(CancellationToken.None); } catch {
                            // Intentionally ignored to preserve the existing best-effort or fallback behavior.
                        }
                        break;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(true); } catch {
                    // Intentionally ignored to preserve the existing best-effort or fallback behavior.
                }
                try { await process.WaitForExitAsync(CancellationToken.None); } catch {
                    // Intentionally ignored to preserve the existing best-effort or fallback behavior.
                }
                throw;
            }

            if (process.HasExited && !observationWindowCompleted && !timedOut)
                exitCode = process.ExitCode;

            stopwatch.Stop();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            durations.Add(stopwatch.Elapsed.TotalMilliseconds);
            allWorkingSetSamplesMb.AddRange(workingSetSamplesMb);
            allPrivateMemorySamplesMb.AddRange(privateMemorySamplesMb);

            var runFps = ParseNumbers(FpsRegex(), stdout);
            var runFrames = ParseNumbers(FrameTimeRegex(), stdout);
            fpsSamples.AddRange(runFps);
            frameSamples.AddRange(runFrames);

            var completedSoak = soakMode && observationWindowCompleted;
            var exitedEarlyDuringSoak = soakMode && !observationWindowCompleted;

            if (completedSoak)
            {
                passes++;
            }
            else if (timedOut)
            {
                timeouts++;
            }
            else if (exitCode == 0 && !exitedEarlyDuringSoak)
            {
                passes++;
            }
            else
            {
                failures++;
            }

            var stdoutPath = Path.Combine(artifactRoot, $"run-{runIndex + 1:000}-stdout.log");
            var stderrPath = Path.Combine(artifactRoot, $"run-{runIndex + 1:000}-stderr.log");
            await File.WriteAllTextAsync(stdoutPath, stdout, cancellationToken);
            await File.WriteAllTextAsync(stderrPath, stderr, cancellationToken);

            runDetails.Add(new
            {
                run = runIndex + 1,
                soakMode,
                observationWindowCompleted,
                timedOut,
                exitCode,
                durationMs = decimal.Round((decimal)stopwatch.Elapsed.TotalMilliseconds, 2),
                samples = workingSetSamplesMb.Count,
                peakWorkingSetMb = workingSetSamplesMb.Count == 0 ? 0m : workingSetSamplesMb.Max(),
                peakPrivateMemoryMb = privateMemorySamplesMb.Count == 0 ? 0m : privateMemorySamplesMb.Max(),
                fpsSamples = runFps.Count,
                frameTimeSamples = runFrames.Count,
                stdoutPath,
                stderrPath
            });
        }

        var totalRuns = repeat;
        var failureRate = decimal.Round(
            100m * (failures + timeouts) / Math.Max(1, totalRuns),
            2);
        var flakyScore = !soakMode && failureRate is > 0m and < 100m
            ? Math.Min(100m, decimal.Round(failureRate * 1.5m, 2))
            : 0m;

        var memoryGrowthMb = CalculateGrowth(allPrivateMemorySamplesMb);
        if (allPrivateMemorySamplesMb.Count < 4)
            memoryGrowthMb = CalculateGrowth(allWorkingSetSamplesMb);

        var performanceDriftPercent = CalculatePerformanceDrift(
            fpsSamples,
            frameSamples);

        var payload = JsonSerializer.Serialize(
            new
            {
                runs = totalRuns,
                passes,
                failures,
                timeouts,
                soakMode,
                requestedSoakMinutes = soakMode ? request.SoakMinutes : 0,
                failureRate,
                flakyScore,
                memoryGrowthMb,
                performanceDriftPercent,
                targetProcessMetrics = new
                {
                    workingSetSamples = allWorkingSetSamplesMb.Count,
                    privateMemorySamples = allPrivateMemorySamplesMb.Count,
                    peakWorkingSetMb = allWorkingSetSamplesMb.Count == 0 ? 0m : allWorkingSetSamplesMb.Max(),
                    peakPrivateMemoryMb = allPrivateMemorySamplesMb.Count == 0 ? 0m : allPrivateMemorySamplesMb.Max(),
                    fpsSamples = fpsSamples.Count,
                    frameTimeSamples = frameSamples.Count
                },
                durationsMs = durations,
                runDetails,
                interpretation = soakMode
                    ? "Reaching the requested soak observation window while the target is still alive is a successful observation, not a timeout."
                    : "Timeouts represent runs that exceeded the bounded per-run execution window.",
                limitation = fpsSamples.Count == 0 && frameSamples.Count == 0
                    ? "The target emitted no parseable FPS/frame-time metrics, so performance drift remains 0 rather than being inferred from unrelated host metrics."
                    : null
            },
            new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(
            Path.Combine(artifactRoot, "reliability.json"),
            payload,
            cancellationToken);

        return new ReliabilityResult(
            totalRuns,
            passes,
            failures,
            timeouts,
            failureRate,
            flakyScore,
            memoryGrowthMb,
            performanceDriftPercent,
            payload);
    }

    private static decimal CalculateGrowth(IReadOnlyList<decimal> samples)
    {
        if (samples.Count < 4) return 0m;

        var window = Math.Max(1, samples.Count / 5);
        var first = samples.Take(window).Average();
        var last = samples.Skip(samples.Count - window).Average();
        return decimal.Round(Math.Max(0m, last - first), 2);
    }

    private static decimal CalculatePerformanceDrift(
        IReadOnlyList<decimal> fps,
        IReadOnlyList<decimal> frameTimes)
    {
        if (fps.Count >= 4)
        {
            var window = Math.Max(1, fps.Count / 4);
            var first = fps.Take(window).Average();
            var last = fps.Skip(fps.Count - window).Average();
            if (first <= 0m) return 0m;
            return decimal.Round(
                Math.Max(0m, (first - last) / first * 100m),
                2);
        }

        if (frameTimes.Count >= 4)
        {
            var window = Math.Max(1, frameTimes.Count / 4);
            var first = frameTimes.Take(window).Average();
            var last = frameTimes.Skip(frameTimes.Count - window).Average();
            if (first <= 0m) return 0m;
            return decimal.Round(
                Math.Max(0m, (last - first) / first * 100m),
                2);
        }

        return 0m;
    }

    private static List<decimal> ParseNumbers(Regex regex, string text) =>
        regex.Matches(text)
            .Select(match => decimal.TryParse(
                match.Groups[1].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value) ? (decimal?)value : null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Where(value => value >= 0m && value < 100000m)
            .ToList();

    [GeneratedRegex(@"(?im)\bfps\s*[:=]\s*([0-9]+(?:\.[0-9]+)?)")]
    private static partial Regex FpsRegex();

    [GeneratedRegex(@"(?im)\b(?:frame[_ ]?time(?:_ms)?|frametime)\s*[:=]\s*([0-9]+(?:\.[0-9]+)?)")]
    private static partial Regex FrameTimeRegex();
}
