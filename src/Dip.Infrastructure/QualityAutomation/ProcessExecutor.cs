// Purpose: Owns process lifecycle while delegating validation, sandbox, environment, output, and resource-limit policy.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
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
    long CpuTimeMs = 0);

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
        IReadOnlyDictionary<string,string>? environment,
        TimeSpan timeout,
        string stdoutPath,
        string stderrPath,
        CancellationToken cancellationToken,
        Func<Process,CancellationToken,Task>? beforeTimeoutKill = null)
        => RunCoreAsync(
            executable, arguments, workingDirectory, environment, timeout,
            stdoutPath, stderrPath, cancellationToken, beforeTimeoutKill, false);

    public Task<ProcessExecutionResult> RunQaAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string,string>? environment,
        TimeSpan timeout,
        string stdoutPath,
        string stderrPath,
        CancellationToken cancellationToken,
        Func<Process,CancellationToken,Task>? beforeTimeoutKill = null)
        => RunCoreAsync(
            executable, arguments, workingDirectory, environment, timeout,
            stdoutPath, stderrPath, cancellationToken, beforeTimeoutKill, _options.SandboxEnabled);

    private async Task<ProcessExecutionResult> RunCoreAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string,string>? environment,
        TimeSpan timeout,
        string stdoutPath,
        string stderrPath,
        CancellationToken cancellationToken,
        Func<Process,CancellationToken,Task>? beforeTimeoutKill,
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

        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Failed to start {executable}.");
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
                0);
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
                        try { process.Kill(true); } catch {}
                        break;
                    }
                }
                catch {}
                await Task.Delay(100, timeoutCts.Token);
            }
            if (!process.HasExited)
                await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            if (beforeTimeoutKill is not null)
                try { await beforeTimeoutKill(process, CancellationToken.None); } catch {}
            try { process.Kill(true); } catch {}
            try { await process.WaitForExitAsync(CancellationToken.None); } catch {}
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch {}
            try { await process.WaitForExitAsync(CancellationToken.None); } catch {}
            throw;
        }

        var outResult = await stdoutTask;
        var errResult = await stderrTask;
        truncated = outResult.Truncated || errResult.Truncated;
        sw.Stop();
        await File.WriteAllTextAsync(stdoutPath, outResult.Text, CancellationToken.None);
        await File.WriteAllTextAsync(stderrPath, errResult.Text, CancellationToken.None);
        int? exit = null;
        try { exit = timedOut || resourceExceeded ? null : process.ExitCode; } catch {}
        long cpuTimeMs = 0;
        try { cpuTimeMs = Math.Max(0L, (long)process.TotalProcessorTime.TotalMilliseconds); } catch {}
        if (job != IntPtr.Zero)
            ProcessResourceLimiter.CloseHandle(job);

        var detail = resourceExceeded
            ? $"Memory budget exceeded ({peakMb} MiB > {_options.SandboxMemoryLimitMb} MiB)."
            : timedOut
                ? "Execution timed out and the complete process tree was terminated."
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
            outResult.Text, errResult.Text, stdoutPath, stderrPath, report, cpuTimeMs);
    }

    public static string? FindExecutable(string name) => ProcessCommandValidator.FindExecutable(name);
}
