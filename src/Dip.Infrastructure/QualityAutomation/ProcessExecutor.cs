// Purpose: Owns process lifecycle while delegating validation, sandbox, environment, output, and resource-limit policy.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
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
    string Detail);

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
    private static readonly HashSet<string> ExternalQaTools = new(
        new[]
        {
            "dotnet","python","python3","py","pytest","ruff","mypy","cmake","ctest","ninja","msbuild",
            "clang","clang++","clang-tidy","cppcheck","gcc","g++","node","npm","npx","pnpm","yarn","tsc",
            "go","cargo","rustc","javac","java","kotlinc","ruby","php","bash","pwsh","powershell","luac",
            "lua","swiftc","sqlfluff","glslangvalidator","dxc","fxc","semgrep","eslint","shellcheck","rubocop",
            "rspec","phpstan","psalm","phpunit","bats","busted","swift","godot","godot4","gradle","mvn",
            "mvnw","gradlew","cargo-fuzz"
        },
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> UnsafeEnvironmentNames = new(
        new[]
        {
            "LD_PRELOAD","LD_LIBRARY_PATH","PYTHONPATH","PYTHONHOME","NODE_OPTIONS","DOTNET_STARTUP_HOOKS",
            "DOTNET_ADDITIONAL_DEPS","MSBuildSDKsPath","BASH_ENV","ENV","PROMPT_COMMAND","RUBYOPT","PERL5OPT",
            "JAVA_TOOL_OPTIONS","_JAVA_OPTIONS","COR_ENABLE_PROFILING","CORECLR_ENABLE_PROFILING",
            "COMPLUS_ProfAPI_Profiler","COMPLUS_Profiler","CORECLR_PROFILER","GIT_CONFIG_GLOBAL","GIT_CONFIG_SYSTEM"
        },
        StringComparer.OrdinalIgnoreCase);

    private readonly QaExecutionOptions _options;
    private readonly IDipOperationalMetrics? _metrics;

    public ProcessExecutor(IOptions<QaExecutionOptions>? options = null, IDipOperationalMetrics? metrics = null)
    {
        _options = options?.Value ?? new QaExecutionOptions();
        _metrics = metrics;
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

        var networkIsolated = ProcessSandboxInspector.NetworkIsolationVerified();
        var filesystemIsolated = ProcessSandboxInspector.FilesystemIsolationVerified();
        try
        {
            if (sandbox)
            {
                ProcessCommandValidator.ValidateExecutable(executable, workspace);
                ProcessCommandValidator.ValidatePathArguments(args, workspace);
            }
            if (sandbox && _options.RequireExternalNetworkIsolation && !networkIsolated)
            {
                throw new InvalidOperationException(
                    "QA requires verified network isolation. DIP_QA_NETWORK_ISOLATED=1 and a runtime with no non-loopback Linux routes (or an equivalent externally attested Windows sandbox) are required.");
            }
            if (sandbox && _options.RequireExternalFilesystemIsolation && !filesystemIsolated)
            {
                throw new InvalidOperationException(
                    "QA requires verified filesystem isolation. DIP_QA_FILESYSTEM_ISOLATED=1 must be supplied only by an external runner that exposes the QA workspace without worker/application secrets.");
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
                    cpuApplied, memoryApplied, networkIsolated, peakMb, false, "Process start failed."),
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
                : "Sandbox policy applied.";
        var report = new ProcessSandboxReport(
            sandbox,
            sandbox,
            sandbox && _options.SanitizeEnvironment,
            cpuApplied,
            memoryApplied,
            networkIsolated,
            peakMb,
            truncated,
            detail);
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
