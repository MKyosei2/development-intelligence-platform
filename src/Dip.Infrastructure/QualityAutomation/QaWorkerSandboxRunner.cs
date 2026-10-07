// Purpose: Executes untrusted QA commands in ephemeral OCI containers or through a signed external container/VM runner protocol.
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Dip.Infrastructure.QualityAutomation;

public sealed record QaWorkerSandboxRequest(
    string Executable,
    IReadOnlyList<string> Arguments,
    string Workspace,
    IReadOnlyDictionary<string, string>? Environment,
    TimeSpan Timeout,
    string StandardOutputPath,
    string StandardErrorPath,
    int MemoryLimitMb,
    int CpuPercent);

public interface IQaWorkerSandboxRunner
{
    bool IsConfigured { get; }
    Task<ProcessExecutionResult> RunAsync(QaWorkerSandboxRequest request, CancellationToken ct);
}

/// <summary>
/// Provides a hard execution boundary for QA workloads. Two backends are supported:
/// 1) an operator-provided external runner implementing dip.qa-worker.v1 (container or VM), and
/// 2) a built-in OCI backend using Docker/Podman with no network, a read-only rootfs,
///    dropped capabilities and a disposable copy of the source workspace.
/// The source workspace is never mounted read/write into the OCI workload.
/// </summary>
public sealed class QaWorkerSandboxRunner : IQaWorkerSandboxRunner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public bool IsConfigured => ResolveExternalRunner() is not null || ResolveOciBackend() is not null;

    public async Task<ProcessExecutionResult> RunAsync(QaWorkerSandboxRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var workspace = Path.GetFullPath(request.Workspace);
        if (!Directory.Exists(workspace))
            throw new DirectoryNotFoundException(workspace);

        if (ResolveExternalRunner() is { } external)
            return await RunExternalAsync(external, request with { Workspace = workspace }, ct);
        if (ResolveOciBackend() is { } oci)
            return await RunOciAsync(oci.Runtime, oci.Image, request with { Workspace = workspace }, ct);

        throw new InvalidOperationException(
            "No ephemeral QA worker is configured. Configure DIP_QA_SANDBOX_RUNNER for a signed container/VM runner, "
            + "or DIP_QA_SANDBOX_IMAGE for Docker/Podman execution.");
    }

    private static async Task<ProcessExecutionResult> RunExternalAsync(
        string runner,
        QaWorkerSandboxRequest request,
        CancellationToken ct)
    {
        var root = Path.Combine(Path.GetTempPath(), "dip-qa-worker", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var requestPath = Path.Combine(root, "request.json");
        var resultPath = Path.Combine(root, "result.json");
        var runnerStdout = Path.Combine(root, "runner.stdout.log");
        var runnerStderr = Path.Combine(root, "runner.stderr.log");
        var sandboxStdout = Path.Combine(root, "sandbox.stdout.log");
        var sandboxStderr = Path.Combine(root, "sandbox.stderr.log");

        try
        {
            var payload = new ExternalRunnerRequest(
                "dip.qa-worker.v1",
                Guid.NewGuid().ToString("N"),
                request.Executable,
                request.Arguments,
                request.Workspace,
                ProcessSandboxInspector.WorkspaceIdentity(request.Workspace),
                SanitizeEnvironment(request.Environment),
                Math.Clamp((long)request.Timeout.TotalMilliseconds, 1000, 86_400_000),
                Math.Clamp(request.MemoryLimitMb, 128, 262_144),
                Math.Clamp(request.CpuPercent, 1, 100),
                sandboxStdout,
                sandboxStderr,
                true,
                true,
                true);
            await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(payload, Json), ct);

            var psi = MinimalProcessStartInfo(runner, Path.GetDirectoryName(runner) ?? request.Workspace);
            psi.ArgumentList.Add("--request");
            psi.ArgumentList.Add(requestPath);
            psi.ArgumentList.Add("--result");
            psi.ArgumentList.Add(resultPath);
            ForwardAttestationKey(psi);

            var execution = await ExecuteHostProcessAsync(
                psi,
                request.Timeout + TimeSpan.FromSeconds(30),
                runnerStdout,
                runnerStderr,
                ct);
            if (execution.TimedOut)
                throw new TimeoutException("External QA sandbox runner timed out.");
            if (execution.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"External QA sandbox runner failed with exit code {execution.ExitCode}. "
                    + execution.StandardError);
            }
            if (!File.Exists(resultPath))
                throw new InvalidDataException("External QA sandbox runner did not produce result.json.");

            var resultInfo = new FileInfo(resultPath);
            if (resultInfo.Length > 1_048_576)
                throw new InvalidDataException("External QA sandbox result.json exceeds the 1 MiB protocol limit.");

            var result = JsonSerializer.Deserialize<ExternalRunnerResult>(
                await File.ReadAllTextAsync(resultPath, ct),
                Json)
                ?? throw new InvalidDataException("External QA sandbox result is invalid.");
            if (!string.Equals(result.Schema, "dip.qa-worker-result.v1", StringComparison.Ordinal))
                throw new InvalidDataException($"Unsupported QA worker result schema '{result.Schema}'.");

            var verification = ProcessSandboxInspector.VerifyAttestation(result.Attestation, request.Workspace);
            if (!verification.Verified)
                throw new InvalidOperationException("External sandbox attestation was rejected: " + verification.Reason);
            if (result.Attestation is not { CpuLimitApplied: true, MemoryLimitApplied: true })
            {
                throw new InvalidOperationException(
                    "External sandbox attestation did not prove both CPU and memory resource limits.");
            }

            var stdout = await ReadOptionalFileCappedAsync(sandboxStdout, 2_000_000, ct);
            var stderr = await ReadOptionalFileCappedAsync(sandboxStderr, 2_000_000, ct);
            await WriteResultLogsAsync(request, stdout, stderr);

            var report = new ProcessSandboxReport(
                true,
                true,
                true,
                result.Attestation?.CpuLimitApplied == true,
                result.Attestation?.MemoryLimitApplied == true,
                verification.NetworkIsolated,
                Math.Max(0, result.PeakWorkingSetMb),
                false,
                "Ephemeral external container/VM worker completed with a verified signed attestation.",
                verification.Backend ?? "external",
                verification.Ephemeral,
                true,
                verification.AttestationId);
            return new(
                request.Executable,
                request.Arguments,
                result.ExitCode,
                result.TimedOut,
                Math.Max(0, result.DurationMs),
                stdout,
                stderr,
                request.StandardOutputPath,
                request.StandardErrorPath,
                report,
                Math.Max(0, result.CpuTimeMs),
                string.IsNullOrWhiteSpace(result.ProcessTreeJson) ? "[]" : result.ProcessTreeJson);
        }
        finally
        {
            DeleteDirectoryBestEffort(root, "external-runner-workspace");
        }
    }

    private static async Task<ProcessExecutionResult> RunOciAsync(
        string runtime,
        string image,
        QaWorkerSandboxRequest request,
        CancellationToken ct)
    {
        var root = Path.Combine(Path.GetTempPath(), "dip-qa-oci", Guid.NewGuid().ToString("N"));
        var sandboxWorkspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(root);
        try
        {
            CopyWorkspace(request.Workspace, sandboxWorkspace);
            var executable = MapExecutableForContainer(request.Executable, request.Workspace);
            var arguments = request.Arguments
                .Select(x => MapArgumentForContainer(x, request.Workspace))
                .ToArray();
            var psi = MinimalProcessStartInfo(runtime, request.Workspace);
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--rm");
            psi.ArgumentList.Add("--network");
            psi.ArgumentList.Add("none");
            psi.ArgumentList.Add("--read-only");
            psi.ArgumentList.Add("--cap-drop");
            psi.ArgumentList.Add("ALL");
            psi.ArgumentList.Add("--security-opt");
            psi.ArgumentList.Add("no-new-privileges:true");
            psi.ArgumentList.Add("--pids-limit");
            psi.ArgumentList.Add("512");
            psi.ArgumentList.Add("--memory");
            psi.ArgumentList.Add($"{Math.Clamp(request.MemoryLimitMb, 128, 262_144)}m");
            psi.ArgumentList.Add("--cpus");
            var cpuQuota = Math.Clamp(request.CpuPercent, 1, 100)
                           / 100d
                           * Math.Max(1, Environment.ProcessorCount);
            psi.ArgumentList.Add(cpuQuota.ToString("0.###", CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("--tmpfs");
            psi.ArgumentList.Add("/tmp:rw,nosuid,nodev,noexec,size=256m");
            psi.ArgumentList.Add("--mount");
            psi.ArgumentList.Add($"type=bind,src={sandboxWorkspace},dst=/workspace,rw");
            psi.ArgumentList.Add("--workdir");
            psi.ArgumentList.Add("/workspace");
            foreach (var pair in SanitizeEnvironment(request.Environment))
            {
                psi.ArgumentList.Add("--env");
                psi.ArgumentList.Add($"{pair.Key}={pair.Value}");
            }
            psi.ArgumentList.Add(image);
            psi.ArgumentList.Add(executable);
            foreach (var argument in arguments)
                psi.ArgumentList.Add(argument);

            var hostStdout = Path.Combine(root, "oci.stdout.log");
            var hostStderr = Path.Combine(root, "oci.stderr.log");
            var execution = await ExecuteHostProcessAsync(
                psi,
                request.Timeout,
                hostStdout,
                hostStderr,
                ct);
            await WriteResultLogsAsync(request, execution.StandardOutput, execution.StandardError);

            var attestationVerified = false;
            string? attestationId = null;
            var key = Environment.GetEnvironmentVariable("DIP_QA_SANDBOX_ATTESTATION_KEY_BASE64");
            if (!string.IsNullOrWhiteSpace(key))
            {
                var signed = ProcessSandboxInspector.SignAttestation(
                    "oci:" + Path.GetFileNameWithoutExtension(runtime),
                    Environment.MachineName,
                    request.Workspace,
                    filesystemIsolated: true,
                    networkIsolated: true,
                    cpuLimitApplied: true,
                    memoryLimitApplied: true,
                    key);
                var verification = ProcessSandboxInspector.VerifyAttestation(signed, request.Workspace, key);
                attestationVerified = verification.Verified;
                attestationId = verification.AttestationId;
            }
            else if (string.Equals(
                         Environment.GetEnvironmentVariable("DIP_QA_REQUIRE_SIGNED_ATTESTATION"),
                         "1",
                         StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Signed sandbox attestation is required but DIP_QA_SANDBOX_ATTESTATION_KEY_BASE64 is not configured.");
            }

            var report = new ProcessSandboxReport(
                true,
                true,
                true,
                true,
                true,
                true,
                execution.PeakWorkingSetMb,
                execution.OutputTruncated,
                "Ephemeral OCI worker used a disposable workspace copy, no network, read-only rootfs, "
                + "dropped capabilities and explicit CPU/memory limits.",
                "oci:" + Path.GetFileNameWithoutExtension(runtime),
                true,
                attestationVerified,
                attestationId);
            return new(
                request.Executable,
                request.Arguments,
                execution.ExitCode,
                execution.TimedOut,
                execution.DurationMs,
                execution.StandardOutput,
                execution.StandardError,
                request.StandardOutputPath,
                request.StandardErrorPath,
                report,
                execution.CpuTimeMs,
                execution.ProcessTreeJson);
        }
        finally
        {
            DeleteDirectoryBestEffort(root, "oci-workspace");
        }
    }

    private static (string Runtime, string Image)? ResolveOciBackend()
    {
        var image = Environment.GetEnvironmentVariable("DIP_QA_SANDBOX_IMAGE")?.Trim();
        if (string.IsNullOrWhiteSpace(image))
            return null;
        if (string.Equals(
                Environment.GetEnvironmentVariable("DIP_QA_REQUIRE_PINNED_IMAGE"),
                "1",
                StringComparison.Ordinal)
            && !image.Contains("@sha256:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "DIP_QA_REQUIRE_PINNED_IMAGE=1 requires DIP_QA_SANDBOX_IMAGE to use an immutable @sha256 digest.");
        }

        var requested = Environment.GetEnvironmentVariable("DIP_QA_OCI_RUNTIME")?.Trim();
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var explicitRuntime = Path.IsPathRooted(requested)
                ? (File.Exists(requested) ? requested : null)
                : ProcessCommandValidator.FindExecutable(requested);
            if (explicitRuntime is not null)
                return (explicitRuntime, image);
        }

        foreach (var candidate in new[] { "docker", "podman" })
        {
            var resolved = ProcessCommandValidator.FindExecutable(candidate);
            if (resolved is not null)
                return (resolved, image);
        }
        return null;
    }

    private static string? ResolveExternalRunner()
    {
        var configured = Environment.GetEnvironmentVariable("DIP_QA_SANDBOX_RUNNER")?.Trim();
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathRooted(configured))
            return null;
        var full = Path.GetFullPath(configured);
        return File.Exists(full) ? full : null;
    }

    private static ProcessStartInfo MinimalProcessStartInfo(string executable, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var inheritedPath = Environment.GetEnvironmentVariable("PATH");
        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        var home = Environment.GetEnvironmentVariable(
            OperatingSystem.IsWindows() ? "USERPROFILE" : "HOME");
        psi.Environment.Clear();
        if (!string.IsNullOrWhiteSpace(inheritedPath))
            psi.Environment["PATH"] = inheritedPath;
        if (!string.IsNullOrWhiteSpace(systemRoot))
            psi.Environment["SystemRoot"] = systemRoot;
        if (!string.IsNullOrWhiteSpace(home))
            psi.Environment[OperatingSystem.IsWindows() ? "USERPROFILE" : "HOME"] = home;
        return psi;
    }

    private static void ForwardAttestationKey(ProcessStartInfo psi)
    {
        var key = Environment.GetEnvironmentVariable("DIP_QA_SANDBOX_ATTESTATION_KEY_BASE64");
        if (!string.IsNullOrWhiteSpace(key))
            psi.Environment["DIP_QA_SANDBOX_ATTESTATION_KEY_BASE64"] = key;
    }

    private static IReadOnlyDictionary<string, string> SanitizeEnvironment(
        IReadOnlyDictionary<string, string>? supplied)
    {
        if (supplied is null || supplied.Count == 0)
            return new Dictionary<string, string>();
        return supplied
            .Where(x => !ProcessEnvironmentSanitizer.IsSecretName(x.Key)
                        && !ProcessEnvironmentSanitizer.IsUnsafeEnvironmentName(x.Key)
                        && x.Key.Length <= 128
                        && x.Value.Length <= 8192)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<HostExecution> ExecuteHostProcessAsync(
        ProcessStartInfo psi,
        TimeSpan timeout,
        string stdoutPath,
        string stderrPath,
        CancellationToken ct)
    {
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stopwatch = Stopwatch.StartNew();
        if (!process.Start())
            throw new InvalidOperationException($"Failed to start sandbox backend '{psi.FileName}'.");

        var stdoutTask = ReadCappedAsync(process.StandardOutput, 2_000_000, ct);
        var stderrTask = ReadCappedAsync(process.StandardError, 2_000_000, ct);
        var timedOut = false;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            timedOut = true;
            TryKillProcessTree(process);
            await TryWaitForExitAfterKillAsync(process);
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        stopwatch.Stop();
        await File.WriteAllTextAsync(stdoutPath, stdout.Text, CancellationToken.None);
        await File.WriteAllTextAsync(stderrPath, stderr.Text, CancellationToken.None);

        var exitCode = timedOut ? null : TryReadExitCode(process);
        var cpuTimeMs = TryReadCpuTimeMs(process);
        var peakWorkingSetMb = TryReadPeakWorkingSetMb(process);
        return new(
            exitCode,
            timedOut,
            stopwatch.ElapsedMilliseconds,
            stdout.Text,
            stderr.Text,
            cpuTimeMs,
            peakWorkingSetMb,
            stdout.Truncated || stderr.Truncated,
            "[]");
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            Trace.TraceWarning(
                "QA sandbox timed out but process-tree termination could not be confirmed: {0}: {1}",
                ex.GetType().Name,
                ex.Message);
        }
    }

    private static async Task TryWaitForExitAfterKillAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            Trace.TraceWarning(
                "QA sandbox process exit could not be observed after termination: {0}: {1}",
                ex.GetType().Name,
                ex.Message);
        }
    }

    private static int? TryReadExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch (InvalidOperationException ex)
        {
            Trace.TraceWarning("QA sandbox exit code unavailable: {0}", ex.Message);
            return null;
        }
    }

    private static long TryReadCpuTimeMs(Process process)
    {
        try
        {
            return Math.Max(0, (long)process.TotalProcessorTime.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            Trace.TraceWarning("QA sandbox CPU time unavailable: {0}: {1}", ex.GetType().Name, ex.Message);
            return 0;
        }
    }

    private static long TryReadPeakWorkingSetMb(Process process)
    {
        try
        {
            return Math.Max(0, process.PeakWorkingSet64 / 1024 / 1024);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            Trace.TraceWarning(
                "QA sandbox peak working set unavailable: {0}: {1}",
                ex.GetType().Name,
                ex.Message);
            return 0;
        }
    }

    private static async Task<(string Text, bool Truncated)> ReadCappedAsync(
        StreamReader reader,
        int limit,
        CancellationToken ct)
    {
        var buffer = new char[8192];
        var builder = new StringBuilder(Math.Min(limit, 65536));
        var truncated = false;
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read <= 0)
                break;
            var remaining = limit - builder.Length;
            if (remaining > 0)
                builder.Append(buffer, 0, Math.Min(remaining, read));
            if (read > remaining)
                truncated = true;
        }
        return (builder.ToString(), truncated);
    }

    private static async Task WriteResultLogsAsync(
        QaWorkerSandboxRequest request,
        string stdout,
        string stderr)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(request.StandardOutputPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(request.StandardErrorPath)!);
        await File.WriteAllTextAsync(request.StandardOutputPath, stdout, CancellationToken.None);
        await File.WriteAllTextAsync(request.StandardErrorPath, stderr, CancellationToken.None);
    }

    private static async Task<string> ReadOptionalFileCappedAsync(
        string path,
        int maximumCharacters,
        CancellationToken ct)
    {
        if (!File.Exists(path))
            return string.Empty;
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            8192,
            true);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[8192];
        var builder = new StringBuilder(Math.Min(maximumCharacters, 65536));
        while (builder.Length < maximumCharacters)
        {
            var read = await reader.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, maximumCharacters - builder.Length)),
                ct);
            if (read <= 0)
                break;
            builder.Append(buffer, 0, read);
        }
        if (!reader.EndOfStream)
            builder.Append("\n[DIP: output truncated by sandbox protocol]\n");
        return builder.ToString();
    }

    private static string MapExecutableForContainer(string executable, string workspace)
    {
        if (Path.IsPathRooted(executable))
        {
            var full = Path.GetFullPath(executable);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            var root = Path.GetFullPath(workspace);
            if (!ProcessCommandValidator.IsInside(root, full, comparison))
            {
                throw new InvalidOperationException(
                    "OCI sandbox cannot execute a host binary outside the copied QA workspace. "
                    + "Put the tool in the image or use the external VM runner protocol.");
            }
            return "/workspace/" + Path.GetRelativePath(root, full).Replace('\\', '/');
        }

        if (executable.Contains(Path.DirectorySeparatorChar)
            || executable.Contains(Path.AltDirectorySeparatorChar))
        {
            return "/workspace/" + executable.TrimStart('.', '/', '\\').Replace('\\', '/');
        }
        return executable;
    }

    private static string MapArgumentForContainer(string argument, string workspace)
    {
        if (string.IsNullOrWhiteSpace(argument))
            return argument;
        var root = Path.GetFullPath(workspace);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (Path.IsPathFullyQualified(argument))
        {
            var full = Path.GetFullPath(argument);
            if (ProcessCommandValidator.IsInside(root, full, comparison))
                return "/workspace/" + Path.GetRelativePath(root, full).Replace('\\', '/');
            return argument;
        }

        var equals = argument.IndexOf('=');
        if (equals > 0 && equals < argument.Length - 1)
        {
            var value = argument[(equals + 1)..].Trim('"');
            if (Path.IsPathFullyQualified(value))
            {
                var full = Path.GetFullPath(value);
                if (ProcessCommandValidator.IsInside(root, full, comparison))
                {
                    return argument[..(equals + 1)]
                           + "/workspace/"
                           + Path.GetRelativePath(root, full).Replace('\\', '/');
                }
            }
        }
        return argument;
    }

    private static void CopyWorkspace(string source, string target)
    {
        const int maximumFiles = 50_000;
        const long maximumBytes = 10L * 1024 * 1024 * 1024;
        var ignored = new HashSet<string>(
            [".git", ".vs", "bin", "obj", "artifacts", ".dipqa", "Library", "Temp", "Logs", "node_modules"],
            StringComparer.OrdinalIgnoreCase);
        var filesCopied = 0;
        long bytesCopied = 0;
        Directory.CreateDirectory(target);
        var stack = new Stack<(string Source, string Target)>();
        stack.Push((source, target));
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var file in Directory.EnumerateFiles(current.Source))
            {
                var info = new FileInfo(file);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                filesCopied++;
                bytesCopied += Math.Max(0, info.Length);
                if (filesCopied > maximumFiles || bytesCopied > maximumBytes)
                {
                    throw new InvalidOperationException(
                        "QA workspace exceeds the ephemeral runner copy budget (50,000 files / 10 GiB).");
                }
                File.Copy(file, Path.Combine(current.Target, info.Name), overwrite: true);
            }

            foreach (var directory in Directory.EnumerateDirectories(current.Source))
            {
                var info = new DirectoryInfo(directory);
                if (ignored.Contains(info.Name) || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                var child = Path.Combine(current.Target, info.Name);
                Directory.CreateDirectory(child);
                stack.Push((directory, child));
            }
        }
    }

    private static void DeleteDirectoryBestEffort(string path, string purpose)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning(
                "QA sandbox cleanup failed for {0} '{1}': {2}: {3}",
                purpose,
                path,
                ex.GetType().Name,
                ex.Message);
        }
    }

    private sealed record ExternalRunnerRequest(
        string Schema,
        string ExecutionId,
        string Executable,
        IReadOnlyList<string> Arguments,
        string Workspace,
        string WorkspaceIdentitySha256,
        IReadOnlyDictionary<string, string> Environment,
        long TimeoutMs,
        int MemoryLimitMb,
        int CpuPercent,
        string StandardOutputPath,
        string StandardErrorPath,
        bool RequireEphemeral,
        bool RequireNetworkIsolation,
        bool RequireFilesystemIsolation);

    private sealed record ExternalRunnerResult(
        string Schema,
        int? ExitCode,
        bool TimedOut,
        long DurationMs,
        long CpuTimeMs,
        long PeakWorkingSetMb,
        string? ProcessTreeJson,
        SandboxIsolationAttestation? Attestation);

    private sealed record HostExecution(
        int? ExitCode,
        bool TimedOut,
        long DurationMs,
        string StandardOutput,
        string StandardError,
        long CpuTimeMs,
        long PeakWorkingSetMb,
        bool OutputTruncated,
        string ProcessTreeJson);
}
