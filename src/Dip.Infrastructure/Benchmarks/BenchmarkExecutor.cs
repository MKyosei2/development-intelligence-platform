// Purpose: Executes sequential performance benchmarks and bounded concurrent load runs with explicit, non-overlapping semantics.
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Dip.Application.Features.Benchmarks;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.Benchmarks;

public sealed class BenchmarkExecutor(ProcessExecutor processExecutor, IQaWorkspaceResolver workspaceResolver) : IBenchmarkExecutor, IDisposable
{
    private readonly HttpClient _http = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        MaxConnectionsPerServer = 64,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public async Task<BenchmarkExecutionResult> ExecuteAsync(BenchmarkExecutionSpec spec, CancellationToken ct)
    {
        var semantics = ParseSemantics(spec.Mode);
        if (semantics.Transport == "http")
            ValidateHttpTarget(spec.Target);
        var warmups = Math.Clamp(spec.WarmupIterations, 0, 20);
        var measurements = Math.Clamp(spec.MeasurementIterations, 3, 500);
        var samples = new List<BenchmarkSample>(measurements);

        for (var i = 0; i < warmups; i++)
            await ExecuteOneAsync(spec, semantics.Transport, -1, ct);

        if (semantics.Kind == "load")
        {
            var concurrency = ResolveLoadConcurrency(measurements);
            using var gate = new SemaphoreSlim(concurrency, concurrency);
            var tasks = Enumerable.Range(1, measurements).Select(async iteration =>
            {
                await gate.WaitAsync(ct);
                try { return await ExecuteOneAsync(spec, semantics.Transport, iteration, ct); }
                finally { gate.Release(); }
            }).ToArray();
            samples.AddRange(await Task.WhenAll(tasks));
            samples.Sort((a, b) => a.Iteration.CompareTo(b.Iteration));
        }
        else
        {
            for (var i = 0; i < measurements; i++)
                samples.Add(await ExecuteOneAsync(spec, semantics.Transport, i + 1, ct));
        }

        var environment = EnvironmentDescriptor();
        var summary = JsonSerializer.Serialize(new
        {
            spec,
            semantics = new
            {
                semantics.Kind,
                semantics.Transport,
                concurrency = semantics.Kind == "load" ? ResolveLoadConcurrency(measurements) : 1,
                latencyMeaning = semantics.Kind == "load"
                    ? "per-operation latency while bounded concurrency is active"
                    : "isolated sequential operation latency"
            },
            environment,
            samples
        });
        return new(samples, environment.Fingerprint, summary);
    }

    private async Task<BenchmarkSample> ExecuteOneAsync(
        BenchmarkExecutionSpec spec,
        string transport,
        int iteration,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (transport == "http")
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linked.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(spec.TimeoutSeconds, 1, 600)));
                using var request = new HttpRequestMessage(HttpMethod.Get, spec.Target);
                request.Headers.TryAddWithoutValidation("X-DIP-Benchmark-Iteration", iteration.ToString(System.Globalization.CultureInfo.InvariantCulture));
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, linked.Token);
                _ = await response.Content.ReadAsByteArrayAsync(linked.Token);
                sw.Stop();
                return new(
                    iteration,
                    response.IsSuccessStatusCode,
                    false,
                    (int)response.StatusCode,
                    ToMicroseconds(sw.Elapsed),
                    response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}");
            }

            var workspace = workspaceResolver.Resolve(spec.WorkspaceRelativePath);
            var executable = ResolveProcessTarget(workspace, spec.Target);
            var root = Path.Combine(Path.GetFullPath("artifacts/benchmark-exec"), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var result = await processExecutor.RunAsync(
                executable,
                spec.Arguments ?? Array.Empty<string>(),
                workspace,
                new Dictionary<string, string>
                {
                    ["DIP_BENCHMARK"] = "1",
                    ["DIP_BENCHMARK_ITERATION"] = iteration.ToString(System.Globalization.CultureInfo.InvariantCulture)
                },
                TimeSpan.FromSeconds(Math.Clamp(spec.TimeoutSeconds, 1, 600)),
                Path.Combine(root, "stdout.log"),
                Path.Combine(root, "stderr.log"),
                ct);
            sw.Stop();
            return new(
                iteration,
                result.ExitCode == 0 && !result.TimedOut,
                result.TimedOut,
                result.ExitCode,
                ToMicroseconds(sw.Elapsed),
                result.ExitCode == 0 && !result.TimedOut ? null : result.StandardError);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return new(iteration, false, true, null, ToMicroseconds(sw.Elapsed), "Benchmark/load operation timed out.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            return new(iteration, false, false, null, ToMicroseconds(sw.Elapsed), ex.Message);
        }
    }

    private static (string Kind, string Transport) ParseSemantics(string? mode)
    {
        var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "http" or "benchmark-http" => ("benchmark", "http"),
            "process" or "benchmark-process" => ("benchmark", "process"),
            "load-http" => ("load", "http"),
            "load-process" => ("load", "process"),
            _ => throw new InvalidOperationException("Benchmark mode must be http, process, benchmark-http, benchmark-process, load-http, or load-process.")
        };
    }

    private static int ResolveLoadConcurrency(int measurements)
    {
        var configured = Environment.GetEnvironmentVariable("DIP_BENCHMARK_LOAD_CONCURRENCY");
        if (int.TryParse(configured, out var parsed))
            return Math.Clamp(parsed, 1, Math.Min(64, Math.Max(1, measurements)));
        return Math.Clamp(Environment.ProcessorCount, 2, Math.Min(16, Math.Max(2, measurements)));
    }


    private static void ValidateHttpTarget(string target)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("HTTP benchmark target must be an absolute http/https URI.");

        // Remote HTTP load generation is deliberately opt-in. Autonomous QA can
        // therefore not turn an untrusted BenchmarkExecutionSpec into arbitrary
        // network traffic merely by selecting load-http/benchmark-http.
        var allowRemote = string.Equals(
            Environment.GetEnvironmentVariable("DIP_BENCHMARK_ALLOW_REMOTE_HTTP"),
            "1",
            StringComparison.Ordinal);
        if (allowRemote || uri.IsLoopback)
            return;

        if (IPAddress.TryParse(uri.Host, out var address)
            && (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)))
            return;

        throw new InvalidOperationException(
            "Remote HTTP benchmarking is disabled by default. Use a loopback target or explicitly set DIP_BENCHMARK_ALLOW_REMOTE_HTTP=1 in a trusted runner.");
    }

    private static string ResolveProcessTarget(string workspace, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("Benchmark process target is required.", nameof(target));
        if (Path.IsPathRooted(target))
            throw new InvalidOperationException("Benchmark process targets must be workspace-relative.");
        var root = Path.GetFullPath(workspace);
        var local = Path.GetFullPath(Path.Combine(root, target));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!local.StartsWith(prefix, comparison) || !File.Exists(local))
            throw new FileNotFoundException("Benchmark process target was not found inside the configured workspace.", local);
        return local;
    }

    private static long ToMicroseconds(TimeSpan elapsed)
        => (long)Math.Round(elapsed.TotalMilliseconds * 1000d, MidpointRounding.AwayFromZero);

    private static BenchmarkEnvironment EnvironmentDescriptor()
    {
        var machine = Environment.MachineName;
        var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        var osArch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
        var processArch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
        var framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        var cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown";
        var processors = Environment.ProcessorCount;
        var text = $"{machine}|{os}|{osArch}|{processArch}|{framework}|{cpu}|{processors}";
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        return new(fingerprint, machine, os, osArch, processArch, framework, cpu, processors);
    }

    private sealed record BenchmarkEnvironment(
        string Fingerprint,
        string Machine,
        string Os,
        string OsArchitecture,
        string ProcessArchitecture,
        string Framework,
        string CpuIdentifier,
        int ProcessorCount);

    public void Dispose() => _http.Dispose();
}
