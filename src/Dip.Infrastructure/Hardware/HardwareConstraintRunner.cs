// Purpose: Runs a process under optional Windows CPU and memory constraints while distinguishing observation completion, cancellation, and cleanup failures.
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dip.Infrastructure.Hardware;

public sealed record ConstrainedProcessResult(
    int? ExitCode,
    bool ObservationWindowCompleted,
    bool TimedOut,
    long DurationMs,
    long PeakWorkingSetMb,
    string StandardOutput,
    string StandardError,
    string StandardOutputPath,
    string StandardErrorPath,
    bool CpuLimitApplied,
    bool MemoryLimitApplied,
    bool ThermalCurveApplied);

public sealed class HardwareConstraintRunner
{
    public async Task<ConstrainedProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> args,
        string workspace,
        TimeSpan observationWindow,
        decimal cpuPercent,
        long memoryBudgetMb,
        decimal thermalRetentionPercent,
        string stdoutPath,
        string stderrPath,
        CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(stdoutPath)!);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stopwatch = Stopwatch.StartNew();
        if (!process.Start())
            throw new InvalidOperationException("Failed to start constrained process.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        var job = IntPtr.Zero;
        var cpuApplied = false;
        var memoryApplied = false;
        var thermalApplied = false;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                job = CreateJobObject(IntPtr.Zero, null);
                if (job != IntPtr.Zero)
                {
                    cpuApplied = ApplyCpuLimit(job, cpuPercent);
                    memoryApplied = ApplyMemoryLimit(job, memoryBudgetMb);
                    if (!AssignProcessToJobObject(job, process.Handle))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to assign the process to the Windows job object.");
                }
            }

            long peakWorkingSetMb = 0;
            var observationWindowCompleted = false;
            var callerCancelled = false;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(observationWindow);

            Task thermalTask = Task.CompletedTask;
            if (job != IntPtr.Zero && thermalRetentionPercent < 99m)
            {
                thermalApplied = true;
                thermalTask = ApplyThermalCurveAsync(
                    job,
                    cpuPercent,
                    thermalRetentionPercent,
                    observationWindow,
                    linked.Token);
            }

            try
            {
                while (!process.HasExited)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    try
                    {
                        process.Refresh();
                        peakWorkingSetMb = Math.Max(peakWorkingSetMb, process.WorkingSet64 / 1024 / 1024);
                    }
                    catch (InvalidOperationException) when (process.HasExited)
                    {
                        break;
                    }

                    await Task.Delay(250, linked.Token);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                observationWindowCompleted = true;
                await TerminateProcessAsync(process);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                callerCancelled = true;
                await TerminateProcessAsync(process);
                throw;
            }
            finally
            {
                try
                {
                    await thermalTask;
                }
                catch (OperationCanceledException) when (linked.IsCancellationRequested)
                {
                    // Expected when the observation window or caller cancellation ends the thermal curve.
                }
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            stopwatch.Stop();
            await File.WriteAllTextAsync(stdoutPath, stdout, CancellationToken.None);
            await File.WriteAllTextAsync(stderrPath, stderr, CancellationToken.None);

            int? exitCode = observationWindowCompleted
                ? 0
                : process.HasExited ? process.ExitCode : null;

            return new(
                exitCode,
                observationWindowCompleted,
                callerCancelled,
                stopwatch.ElapsedMilliseconds,
                peakWorkingSetMb,
                stdout,
                stderr,
                stdoutPath,
                stderrPath,
                cpuApplied,
                memoryApplied,
                thermalApplied);
        }
        finally
        {
            if (job != IntPtr.Zero)
                CloseHandle(job);
        }
    }

    private static async Task TerminateProcessAsync(Process process)
    {
        if (process.HasExited)
            return;

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            return;
        }
        catch (Win32Exception) when (process.HasExited)
        {
            return;
        }

        try
        {
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // Process exit won the race with WaitForExitAsync.
        }
    }

    private static async Task ApplyThermalCurveAsync(
        IntPtr job,
        decimal initialPercent,
        decimal retention,
        TimeSpan duration,
        CancellationToken ct)
    {
        var steps = new[] { 0.85m, 0.70m, Math.Clamp(retention / 100m, 0.20m, 1m) };
        var delay = TimeSpan.FromMilliseconds(Math.Max(500, duration.TotalMilliseconds / (steps.Length + 1)));
        foreach (var factor in steps)
        {
            await Task.Delay(delay, ct);
            if (!ApplyCpuLimit(job, Math.Clamp(initialPercent * factor, 1m, 100m)))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to apply the simulated thermal CPU cap.");
        }
    }

    private static bool ApplyCpuLimit(IntPtr job, decimal percent)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var info = new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
        {
            ControlFlags = JOB_OBJECT_CPU_RATE_CONTROL_ENABLE | JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP,
            CpuRate = (uint)(Math.Clamp(percent, 1m, 100m) * 100m)
        };
        var size = Marshal.SizeOf(info);
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(info, pointer, false);
            return SetInformationJobObject(job, JobObjectCpuRateControlInformation, pointer, (uint)size);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private static bool ApplyMemoryLimit(IntPtr job, long memoryMb)
    {
        if (!OperatingSystem.IsWindows() || memoryMb <= 0)
            return false;

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_PROCESS_MEMORY
            },
            ProcessMemoryLimit = checked((UIntPtr)((ulong)memoryMb * 1024UL * 1024UL))
        };
        var size = Marshal.SizeOf(info);
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(info, pointer, false);
            return SetInformationJobObject(job, JobObjectExtendedLimitInformation, pointer, (uint)size);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private const uint JOB_OBJECT_CPU_RATE_CONTROL_ENABLE = 0x1;
    private const uint JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP = 0x4;
    private const uint JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x100;
    private const int JobObjectExtendedLimitInformation = 9;
    private const int JobObjectCpuRateControlInformation = 15;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
    {
        public uint ControlFlags;
        public uint CpuRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public long Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int infoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}
