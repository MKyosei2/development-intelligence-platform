// Purpose: Implements Minimum Hardware Runner infrastructure behavior for this repository area.
using System.Globalization;
using System.Text.Json;
using Dip.Application.Features.Hardware;
using Dip.Application.Features.Intelligence;
using Dip.Application.Features.QualityAutomation;

namespace Dip.Infrastructure.Intelligence;

public sealed class MinimumHardwareRunner : IMinimumHardwareRunner
{
    private readonly IQaWorkspaceResolver _workspace;
    private readonly IPerformanceSimulationRunner _simulation;

    public MinimumHardwareRunner(
        IQaWorkspaceResolver workspace,
        IPerformanceSimulationRunner simulation)
    {
        _workspace = workspace;
        _simulation = simulation;
    }

    public async Task<MinimumHardwareResult> FindAsync(
        MinimumHardwareRequest request,
        string artifactRoot,
        CancellationToken cancellationToken)
    {
        var workspace = _workspace.Resolve(request.WorkspaceRelativePath);
        if (Path.IsPathRooted(request.RuntimeExecutable))
            throw new InvalidOperationException("Runtime executable must be workspace-relative.");

        var executable = Path.GetFullPath(Path.Combine(workspace, request.RuntimeExecutable));
        var prefix = workspace.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!executable.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(executable))
        {
            throw new InvalidOperationException(
                "Runtime executable escapes the configured workspace or does not exist.");
        }

        Directory.CreateDirectory(artifactRoot);
        var tested = new List<object>();
        var confidenceSamples = new List<decimal>();
        var iterations = Math.Clamp(request.MaxIterations, 3, 12);

        async Task<bool> ProbeAsync(
            string dimension,
            decimal cpu,
            decimal gpu,
            decimal memory,
            decimal storage)
        {
            var memoryBudgetMb = Math.Max(
                128,
                (long)decimal.Round(
                    request.MaximumMemoryMb * Math.Clamp(memory, 1m, 100m) / 100m,
                    0));

            var metadata = JsonSerializer.Serialize(new
            {
                kind = "minimum-hardware-search",
                CpuPercent = cpu,
                GpuPercent = gpu,
                MemoryBandwidthPercent = memory,
                StoragePercent = storage,
                simulated = true,
                realDevice = false
            });

            var profile = new HardwareProfileDto(
                Guid.NewGuid(),
                $"P2 Minimum Hardware {dimension}",
                "Estimated",
                "SimulationTarget",
                100m,
                100m,
                100m,
                100m,
                100m,
                100m,
                memoryBudgetMb,
                Math.Clamp(request.TargetFps, 1, 1000),
                metadata);

            var probeId = $"{dimension}-{cpu:0.##}-{gpu:0.##}-{memory:0.##}-{storage:0.##}"
                .Replace('.', '_');
            var probeDirectory = Path.Combine(artifactRoot, probeId);
            Directory.CreateDirectory(probeDirectory);

            var measurement = await _simulation.RunAsync(
                profile,
                workspace,
                executable,
                request.RuntimeArguments,
                20,
                false,
                probeDirectory,
                cancellationToken);

            confidenceSamples.Add(measurement.ConfidencePercent);
            var constraintApplied = ConstraintWasApplied(
                dimension,
                cpu,
                gpu,
                memory,
                storage,
                measurement.ResultJson);

            tested.Add(new
            {
                dimension,
                cpuPercent = cpu,
                gpuPercent = gpu,
                memoryPercent = memory,
                storagePercent = storage,
                memoryBudgetMb,
                measurement.Status,
                measurement.ConfidencePercent,
                measurement.AverageFps,
                measurement.P95FrameTimeMs,
                measurement.PeakWorkingSetMb,
                constraintApplied,
                artifactDirectory = probeDirectory
            });

            if (!constraintApplied)
            {
                throw new PlatformNotSupportedException(
                    $"The requested {dimension} constraint was not applied. " +
                    "No minimum-hardware conclusion can be drawn from this run.");
            }

            return measurement.Status switch
            {
                "Passed" => true,
                "Failed" => false,
                _ => throw new InvalidOperationException(
                    $"The {dimension} probe returned '{measurement.Status}'. " +
                    "A boundary search requires an explicit pass/fail product result.")
            };
        }

        async Task<decimal> SearchAsync(
            string dimension,
            Func<decimal, Task<bool>> probe)
        {
            if (!await probe(100m))
            {
                throw new InvalidOperationException(
                    $"The target does not pass the baseline 100% {dimension} probe. " +
                    "Fix the baseline before estimating a lower hardware boundary.");
            }

            decimal low = 10m;
            decimal high = 100m;

            if (await probe(low))
                return low;

            for (var i = 0; i < iterations; i++)
            {
                var mid = decimal.Round((low + high) / 2m, 2);
                if (await probe(mid))
                    high = mid;
                else
                    low = mid;
            }

            return decimal.Round(high, 2);
        }

        var cpuBoundary = await SearchAsync(
            "cpu",
            value => ProbeAsync("cpu", value, 100m, 100m, 100m));
        var gpuBoundary = await SearchAsync(
            "gpu",
            value => ProbeAsync("gpu", 100m, value, 100m, 100m));
        var memoryBoundary = await SearchAsync(
            "memory",
            value => ProbeAsync("memory", 100m, 100m, value, 100m));
        var storageBoundary = await SearchAsync(
            "storage",
            value => ProbeAsync("storage", 100m, 100m, 100m, value));

        static decimal Safe(decimal value) =>
            Math.Min(100m, decimal.Round(value * 1.15m + 5m, 2));

        var confidence = confidenceSamples.Count == 0
            ? 0m
            : Math.Min(80m, decimal.Round(confidenceSamples.Average(), 2));

        var payload = JsonSerializer.Serialize(
            new
            {
                method = "p1-performance-simulation-bounded-search",
                simulated = true,
                realDevice = false,
                targetFps = request.TargetFps,
                maximumMemoryMb = request.MaximumMemoryMb,
                iterations,
                boundaries = new
                {
                    cpu = cpuBoundary,
                    gpu = gpuBoundary,
                    memory = memoryBoundary,
                    storage = storageBoundary
                },
                safeMargins = new
                {
                    cpu = Safe(cpuBoundary),
                    gpu = Safe(gpuBoundary),
                    memory = Safe(memoryBoundary),
                    storage = Safe(storageBoundary)
                },
                confidencePercent = confidence,
                tested,
                limitation = "Simulation-derived boundary only. Real-device evidence is required before publishing a certified minimum specification."
            },
            new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(
            Path.Combine(artifactRoot, "minimum-hardware.json"),
            payload,
            cancellationToken);

        return new MinimumHardwareResult(
            cpuBoundary,
            gpuBoundary,
            memoryBoundary,
            storageBoundary,
            Safe(cpuBoundary),
            Safe(gpuBoundary),
            Safe(memoryBoundary),
            Safe(storageBoundary),
            confidence,
            payload);
    }

    private static bool ConstraintWasApplied(
        string dimension,
        decimal cpu,
        decimal gpu,
        decimal memory,
        decimal storage,
        string resultJson)
    {
        if (dimension == "cpu" && cpu >= 99m) return true;
        if (dimension == "gpu" && gpu >= 99m) return true;
        if (dimension == "memory" && memory >= 99m) return true;
        if (dimension == "storage" && storage >= 99m) return true;

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            if (!doc.RootElement.TryGetProperty("process", out var process))
                return false;

            return dimension switch
            {
                "cpu" => ReadBoolean(process, "CpuLimitApplied", "cpuLimitApplied"),
                "gpu" => ReadBoolean(process, "gpuStressApplied", "GpuStressApplied"),
                "memory" => ReadBoolean(process, "memoryBandwidthContentionApplied", "MemoryBandwidthContentionApplied"),
                "storage" => ReadBoolean(process, "storageContentionApplied", "StorageContentionApplied"),
                _ => false
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ReadBoolean(
        JsonElement element,
        string first,
        string second)
    {
        if (element.TryGetProperty(first, out var p1)
            && p1.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return p1.GetBoolean();
        if (element.TryGetProperty(second, out var p2)
            && p2.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return p2.GetBoolean();
        return false;
    }
}
