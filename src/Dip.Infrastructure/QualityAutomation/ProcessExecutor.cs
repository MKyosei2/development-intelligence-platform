// Purpose: Owns process lifecycle while delegating validation, ephemeral worker isolation, environment, output, and resource-limit policy.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Dip.Application.Abstractions.Observability;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.QualityAutomation;

public sealed record ProcessSandboxReport(
    bool Enabled,
    bool WorkspaceBoundaryEnforced,
    bool EnvironmentSanitized,
    bool CpuLimitApplied,
    bool MemoryLimitApplied,
    bool NetworkIsolationVerified,
    long PeakWorkingSetMb,
    bool OutputTruncated,
    string Detail,
    string Backend = "host-process",
    bool Ephemeral = false,
    bool AttestationVerified = false,
    string? AttestationId = null);

public sealed record ProcessExecutionResult(
    string Executable,
    IReadOnlyList<string> Arguments,
    int? ExitCode,
    bool TimedOut,
    long DurationMs,
    string StandardOutput,
    string StandardError,
    string StandardOutputPath,
    string StandardErrorPath,
    ProcessSandboxReport? Sandbox = null,
    long CpuTimeMs = 0,
    string ProcessTreeJson = "[]");

public sealed class ProcessExecutor
{
    private readonly QaExecutionOptions _options;
    private readonly IDipOperationalMetrics? _metrics;
    private readonly IQaWorkerSandboxRunner _sandboxRunner;

    public ProcessExecutor(IOptions<QaExecutionOptions>? options = null, IDipOperationalMetrics? metrics = null)
        : this(options, metrics, new QaWorkerSandboxRunner())
    {
    }

    public ProcessExecutor(
        IOptions<QaExecutionOptions>? options,
        IDipOperationalMetrics? metrics,
        IQaWorkerSandboxRunner sandboxRunner)
    {
        _options = options?.Value ?? new QaExecutionOptions();
        _metrics = metrics;
        _sandboxRunner = sandboxRunner ?? throw new ArgumentNullException(nameof(sandboxRunner));
    }

    public Task<ProcessExecutionResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment,
        TimeSpan timeout,
        string stdoutPath,
        string stderrPath,
        CancellationToken cancellationToken,
        Func<Process, CancellationToken, Task>? beforeTimeoutKill = null)
        => RunCoreAsync(
            executable, arguments, workingDirectory, environment, timeout,
            stdoutPath, stderrPath, cancellationToken, beforeTimeoutKill, false);

    public Task<ProcessExecutionResult> RunQaAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment,
        TimeSpan timeout,
        string stdoutPath,
        string stderrPath,
        CancellationToken cancellationToken,
        Func<Process, CancellationToken, Task>? beforeTimeoutKill = null)
        => RunCoreAsync(
            executable, arguments, workingDirectory, environment, timeout,
            stdoutPath, stderrPath, cancellationToken, beforeTimeoutKill, _options.SandboxEnabled);

    private async Task<ProcessExecutionResult> RunCoreAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment,
        TimeSpan timeout,
        string stdoutPath,
        string stderrPath,
        CancellationToken cancellationToken,
        Func<Process, CancellationToken, Task>? beforeTimeoutKill,
        bool sandbox)
    {
        var args = arguments.ToArray();
        var workspace = Path.GetFullPath(workingDirectory);
        if (!Directory.Exists(workspace))
            throw new DirectoryNotFoundException(workspace);

        // A configured ephemeral worker is the preferred QA boundary. It receives the
        // untrusted command instead of running that command inside the API/worker host.
        // The runner protocol is responsible for its own executable/toolchain policy and
        // returns an isolation report/attestation; path arguments are still constrained
        // to the trusted source workspace before handoff.
        if (sandbox && _sandboxRunner.IsConfigured)
        {
            try
            {
                ProcessCommandValidator.ValidatePathArguments(args, workspace);
                var result = await _sandboxRunner.RunAsync(
                    new QaWorkerSandboxRequest(
                        executable,
                        args,
                        workspace,
                        environment,
                        timeout,
                        stdoutPath,
                        stderrPath,
                        _options.SandboxMemoryLimitMb,
                        _options.SandboxCpuPercent),
                    cancellationToken);
                if (result.Sandbox is not { Enabled: true, Ephemeral: true, WorkspaceBoundaryEnforced: true })
                    throw new InvalidOperationException("Ephemeral QA worker returned without a verified sandbox boundary.");
                if (_options.RequireExternalNetworkIsolation && result.Sandbox.NetworkIsolationVerified is false)
                    throw new InvalidOperationException("QA requires verified network isolation but the ephemeral worker did not attest it.");
                if (_options.RequireExternalFilesystemIsolation && result.Sandbox.WorkspaceBoundaryEnforced is false)
                    throw new InvalidOperationException("QA requires verified filesystem isolation but the ephemeral worker did not attest it.");
                return result;
            }
            catch (InvalidOperationException ex)
            {
                _metrics?.GovernanceSandboxDenied(ProcessSandboxInspector.ClassifySandboxDenial(ex.Message));
                throw;
            }
        }

        var networkIsolated = ProcessSandboxInspector.NetworkIsolationVerified(workspace);
        var filesystemIsolated = ProcessSandboxInspector.FilesystemIsolationVerified(workspace);
        try
        {
            if (sandbox)
            {
                ProcessCommandValidator.ValidateExecutable(executable, workspace);
                ProcessCommandValidator.ValidatePathArguments(args, workspace);
            }
            if (sandbox && ProcessSandboxInspector.SignedAttestationRequired()
                && (!networkIsolated || !filesystemIsolated))
            {
                throw new InvalidOperationException(
                    "QA requires a valid signed sandbox attestation bound to this workspace, but verification did not establish both network and filesystem isolation.");
            }
            if (sandbox && _options.RequireExternalNetworkIsolation && !networkIsolated)
            {
                throw new InvalidOperationException(
                    "QA requires verified network isolation. Use the ephemeral QA worker, a valid signed sandbox attestation, or a locally verifiable isolated namespace.");
            }
            if (sandbox && _options.RequireExternalFilesystemIsolation && !filesystemIsolated)
            {
                throw new InvalidOperationException(
                    "QA requires verified filesystem isolation. Use the ephemeral QA worker or provide a valid signed external sandbox attestation bound to this workspace.");
            }
        }
        catch (InvalidOperationException ex) when (sandbox)
        {
            _metrics?.GovernanceSandboxDenied(ProcessSandboxInspector.ClassifySandboxDenial(ex.Message));
            throw;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(stdoutPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(stderrPath)!);
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        if (sandbox && _options.SanitizeEnvironment)
            ProcessEnvironmentSanitizer.Sanitize(psi.Environment, environment);
        else if (environment is not null)
            foreach (var pair in environment)
                psi.Environment[pair.Key] = pair.Value;

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var sw = Stopwatch.StartNew();
        IntPtr job = IntPtr.Zero;
        var cpuApplied = false;
        var memoryApplied = false;
        long peakMb = 0;
        var truncated = false;
        var processTreeJson = "[]";

        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Failed to start {executable}.");

            processTreeJson = JsonSerializer.Serialize(new[]
            {
                new
                {
                    pid = process.Id,
                    parentPid = (int?)null,
                    name = SafeProcessName(process),
                    workingSetMb = SafeWorkingSetMb(process),
                    cpuTimeMs = SafeCpuTimeMs(process)
                }
            });

            if (sandbox && OperatingSystem.IsWindows())
            {
                job = ProcessResourceLimiter.CreateJobObject(IntPtr.Zero, null);
                if (job != IntPtr.Zero)
                {
                    cpuApplied = ProcessResourceLimiter.ApplyCpuLimit(job, _options.SandboxCpuPercent);
                    memoryApplied = ProcessResourceLimiter.ApplyMemoryAndKillOnClose(job, _options.SandboxMemoryLimitMb);
                    ProcessResourceLimiter.AssignProcessToJobObject(job, process.Handle);
                }
            }
        }
        catch (Exception ex)
        {
            if (job != IntPtr.Zero)
                ProcessResourceLimiter.CloseHandle(job);
            await File.WriteAllTextAsync(stderrPath, ex.ToString(), cancellationToken);
            return new(
                executable, args, null, false, sw.ElapsedMilliseconds, "", ex.ToString(), stdoutPath, stderrPath,
                new ProcessSandboxReport(
                    sandbox, sandbox, sandbox && _options.SanitizeEnvironment,
                    cpuApplied, memoryApplied, networkIsolated, peakMb, false, "Process start failed.",
                    "host-process", false, false, null),
                0,
                processTreeJson);
        }

        var stdoutTask = ProcessOutputCollector.ReadCappedAsync(
            process.StandardOutput,
            Math.Max(8192, _options.MaximumOutputCharacters),
            cancellationToken);
        var stderrTask = ProcessOutputCollector.ReadCappedAsync(
            process.StandardError,
            Math.Max(8192, _options.MaximumOutputCharacters),
            cancellationToken);
        var timedOut = false;
        var resourceExceeded = false;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            while (!process.HasExited)
            {
                timeoutCts.Token.ThrowIfCancellationRequested();
                try
                {
                    peakMb = Math.Max(peakMb, process.WorkingSet64 / 1024 / 1024);
                    if (sandbox
                        && !OperatingSystem.IsWindows()
                        && _options.SandboxMemoryLimitMb > 0
                        && peakMb > _options.SandboxMemoryLimitMb)
                    {
                        resourceExceeded = true;
                        processTreeJson = CaptureProcessTreeJson(process.Id);
                        try { process.Kill(true); }
                        catch
                        {
                            // Intentionally ignored; result remains a resource-policy failure.
                        }
                        break;
                    }
                }
                catch
                {
                    // Resource sampling is diagnostic only and must not change process semantics.
                }
                await Task.Delay(100, timeoutCts.Token);
            }
            if (!process.HasExited)
                await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            processTreeJson = CaptureProcessTreeJson(process.Id);
            if (beforeTimeoutKill is not null)
                try { await beforeTimeoutKill(process, CancellationToken.None); }
                catch
                {
                    // Diagnostic callback failure must not prevent process-tree termination.
                }
            try { process.Kill(true); }
            catch
            {
                // Best effort. WaitForExit below records whether the process actually ended.
            }
            try { await process.WaitForExitAsync(CancellationToken.None); }
            catch
            {
                // Preserve original timeout as the primary failure.
            }
        }
        catch (OperationCanceledException)
        {
            processTreeJson = CaptureProcessTreeJson(process.Id);
            try { process.Kill(true); }
            catch
            {
                // Best effort during caller cancellation.
            }
            try { await process.WaitForExitAsync(CancellationToken.None); }
            catch
            {
                // Caller cancellation remains authoritative.
            }
            throw;
        }

        var outResult = await stdoutTask;
        var errResult = await stderrTask;
        truncated = outResult.Truncated || errResult.Truncated;
        sw.Stop();
        await File.WriteAllTextAsync(stdoutPath, outResult.Text, CancellationToken.None);
        await File.WriteAllTextAsync(stderrPath, errResult.Text, CancellationToken.None);
        int? exit = null;
        try { exit = timedOut || resourceExceeded ? null : process.ExitCode; }
        catch
        {
            // Exit code can be unavailable after forced termination.
        }
        long cpuTimeMs = 0;
        try { cpuTimeMs = Math.Max(0L, (long)process.TotalProcessorTime.TotalMilliseconds); }
        catch
        {
            // CPU time is best-effort diagnostic evidence.
        }
        if (job != IntPtr.Zero)
            ProcessResourceLimiter.CloseHandle(job);

        var detail = resourceExceeded
            ? $"Memory budget exceeded ({peakMb} MiB > {_options.SandboxMemoryLimitMb} MiB)."
            : timedOut
                ? "Execution timed out; a pre-kill process-tree snapshot was captured and the complete process tree was terminated."
                : "Host-process sandbox policy applied.";
        var report = new ProcessSandboxReport(
            sandbox,
            sandbox,
            sandbox && _options.SanitizeEnvironment,
            cpuApplied,
            memoryApplied,
            networkIsolated,
            peakMb,
            truncated,
            detail,
            "host-process",
            false,
            false,
            null);
        return new(
            executable, args, exit, timedOut, sw.ElapsedMilliseconds,
            outResult.Text, errResult.Text, stdoutPath, stderrPath, report, cpuTimeMs, processTreeJson);
    }

    public static string? FindExecutable(string name) => ProcessCommandValidator.FindExecutable(name);

    // Captures the root process and all discoverable descendants without launching another
    // helper process. This keeps timeout diagnostics usable even when the QA sandbox blocks
    // PowerShell, WMIC, ps, or other external process-inspection commands.
    internal static string CaptureProcessTreeJson(int rootPid)
    {
        try
        {
            var processes = Process.GetProcesses();
            try
            {
                var nodes = new Dictionary<int, ProcessTreeNode>();
                foreach (var candidate in processes)
                {
                    try
                    {
                        var parent = TryGetParentProcessId(candidate);
                        nodes[candidate.Id] = new(
                            candidate.Id,
                            parent,
                            SafeProcessName(candidate),
                            SafeWorkingSetMb(candidate),
                            SafeCpuTimeMs(candidate));
                    }
                    catch
                    {
                        // Protected or exiting processes are simply omitted.
                    }
                }

                var selected = new List<ProcessTreeNode>();
                var queue = new Queue<int>();
                var seen = new HashSet<int>();
                queue.Enqueue(rootPid);
                while (queue.Count > 0)
                {
                    var pid = queue.Dequeue();
                    if (!seen.Add(pid))
                        continue;

                    if (nodes.TryGetValue(pid, out var node))
                        selected.Add(node);
                    else if (pid == rootPid)
                        selected.Add(new(pid, null, "<unavailable>", 0, 0));

                    foreach (var child in nodes.Values.Where(x => x.ParentPid == pid).OrderBy(x => x.Pid))
                        queue.Enqueue(child.Pid);
                }

                return JsonSerializer.Serialize(selected);
            }
            finally
            {
                foreach (var candidate in processes)
                    candidate.Dispose();
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new[]
            {
                new { pid = rootPid, captureError = ex.GetType().Name + ": " + ex.Message }
            });
        }
    }

    private static int? TryGetParentProcessId(Process process)
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                var stat = File.ReadAllText($"/proc/{process.Id}/stat");
                var closeParen = stat.LastIndexOf(')');
                if (closeParen < 0 || closeParen + 2 >= stat.Length)
                    return null;
                var fields = stat[(closeParen + 2)..]
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return fields.Length > 1 && int.TryParse(fields[1], out var ppid) ? ppid : null;
            }
            catch
            {
                return null;
            }
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var info = new ProcessBasicInformation();
                var status = NtQueryInformationProcess(
                    process.Handle,
                    0,
                    ref info,
                    Marshal.SizeOf<ProcessBasicInformation>(),
                    out _);
                if (status != 0)
                    return null;
                var value = info.InheritedFromUniqueProcessId.ToInt64();
                return value > 0 && value <= int.MaxValue ? (int)value : null;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static string SafeProcessName(Process process)
    {
        try { return process.ProcessName; }
        catch { return "<unavailable>"; }
    }

    private static long SafeWorkingSetMb(Process process)
    {
        try { return Math.Max(0L, process.WorkingSet64 / 1024 / 1024); }
        catch { return 0; }
    }

    private static long SafeCpuTimeMs(Process process)
    {
        try { return Math.Max(0L, (long)process.TotalProcessorTime.TotalMilliseconds); }
        catch { return 0; }
    }

    private sealed record ProcessTreeNode(
        int Pid,
        int? ParentPid,
        string Name,
        long WorkingSetMb,
        long CpuTimeMs);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);
}
