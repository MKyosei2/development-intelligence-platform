using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Dip.Infrastructure.Hardware;

public sealed record ConstrainedProcessResult(
    int? ExitCode,bool ObservationWindowCompleted,bool TimedOut,long DurationMs,long PeakWorkingSetMb,string StandardOutput,string StandardError,
    string StandardOutputPath,string StandardErrorPath,bool CpuLimitApplied,bool MemoryLimitApplied,bool ThermalCurveApplied);

public sealed class HardwareConstraintRunner
{
    public async Task<ConstrainedProcessResult> RunAsync(string executable,IReadOnlyList<string> args,string workspace,TimeSpan observationWindow,
        decimal cpuPercent,long memoryBudgetMb,decimal thermalRetentionPercent,string stdoutPath,string stderrPath,CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(stdoutPath)!);var psi=new ProcessStartInfo{FileName=executable,WorkingDirectory=workspace,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};foreach(var arg in args)psi.ArgumentList.Add(arg);
        using var process=new Process{StartInfo=psi,EnableRaisingEvents=true};var sw=Stopwatch.StartNew();if(!process.Start())throw new InvalidOperationException("Failed to start constrained process.");
        var outTask=process.StandardOutput.ReadToEndAsync(ct);var errTask=process.StandardError.ReadToEndAsync(ct);IntPtr job=IntPtr.Zero;var cpuApplied=false;var memoryApplied=false;
        if(OperatingSystem.IsWindows())
        {
            job=CreateJobObject(IntPtr.Zero,null);if(job!=IntPtr.Zero){cpuApplied=ApplyCpuLimit(job,cpuPercent);memoryApplied=ApplyMemoryLimit(job,memoryBudgetMb);AssignProcessToJobObject(job,process.Handle);}
        }
        long peak=0;var windowCompleted=false;var timedOut=false;using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct);linked.CancelAfter(observationWindow);
        var thermalTask=job!=IntPtr.Zero&&thermalRetentionPercent<99m?ApplyThermalCurveAsync(job,cpuPercent,thermalRetentionPercent,observationWindow,linked.Token):Task.CompletedTask;
        try
        {
            while(!process.HasExited)
            {
                linked.Token.ThrowIfCancellationRequested();try{peak=Math.Max(peak,process.WorkingSet64/1024/1024);}catch{}await Task.Delay(250,linked.Token);
            }
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested)
        {
            windowCompleted=true;try{process.Kill(true);}catch{}try{await process.WaitForExitAsync(CancellationToken.None);}catch{}
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){timedOut=true;try{process.Kill(true);}catch{}throw;}
        try{await thermalTask;}catch{}
        var stdout=await outTask;var stderr=await errTask;sw.Stop();await File.WriteAllTextAsync(stdoutPath,stdout,CancellationToken.None);await File.WriteAllTextAsync(stderrPath,stderr,CancellationToken.None);
        var exitCode=windowCompleted?0:process.ExitCode;if(job!=IntPtr.Zero)CloseHandle(job);
        return new(exitCode,windowCompleted,timedOut,sw.ElapsedMilliseconds,peak,stdout,stderr,stdoutPath,stderrPath,cpuApplied,memoryApplied,job!=IntPtr.Zero&&thermalRetentionPercent<99m);
    }

    private static async Task ApplyThermalCurveAsync(IntPtr job,decimal initialPercent,decimal retention,TimeSpan duration,CancellationToken ct)
    {
        var steps=new[]{0.85m,0.70m,Math.Clamp(retention/100m,0.20m,1m)};var delay=TimeSpan.FromMilliseconds(Math.Max(500,duration.TotalMilliseconds/(steps.Length+1)));
        foreach(var factor in steps){await Task.Delay(delay,ct);ApplyCpuLimit(job,Math.Clamp(initialPercent*factor,1m,100m));}
    }
    private static bool ApplyCpuLimit(IntPtr job,decimal percent)
    {
        if(!OperatingSystem.IsWindows())return false;var info=new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION{ControlFlags=JOB_OBJECT_CPU_RATE_CONTROL_ENABLE|JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP,CpuRate=(uint)(Math.Clamp(percent,1m,100m)*100m)};
        var size=Marshal.SizeOf(info);var ptr=Marshal.AllocHGlobal(size);try{Marshal.StructureToPtr(info,ptr,false);return SetInformationJobObject(job,JobObjectCpuRateControlInformation,ptr,(uint)size);}finally{Marshal.FreeHGlobal(ptr);}
    }
    private static bool ApplyMemoryLimit(IntPtr job,long memoryMb)
    {
        if(!OperatingSystem.IsWindows()||memoryMb<=0)return false;var info=new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();info.BasicLimitInformation.LimitFlags=JOB_OBJECT_LIMIT_PROCESS_MEMORY;var memoryBytes=checked((ulong)memoryMb*1024UL*1024UL);info.ProcessMemoryLimit=checked((UIntPtr)memoryBytes);
        var size=Marshal.SizeOf(info);var ptr=Marshal.AllocHGlobal(size);try{Marshal.StructureToPtr(info,ptr,false);return SetInformationJobObject(job,JobObjectExtendedLimitInformation,ptr,(uint)size);}finally{Marshal.FreeHGlobal(ptr);}
    }
    private const uint JOB_OBJECT_CPU_RATE_CONTROL_ENABLE=0x1,JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP=0x4,JOB_OBJECT_LIMIT_PROCESS_MEMORY=0x100;private const int JobObjectExtendedLimitInformation=9,JobObjectCpuRateControlInformation=15;
    [StructLayout(LayoutKind.Sequential)]private struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION{public uint ControlFlags;public uint CpuRate;}
    [StructLayout(LayoutKind.Sequential)]private struct JOBOBJECT_BASIC_LIMIT_INFORMATION{public long PerProcessUserTimeLimit;public long PerJobUserTimeLimit;public uint LimitFlags;public UIntPtr MinimumWorkingSetSize;public UIntPtr MaximumWorkingSetSize;public uint ActiveProcessLimit;public long Affinity;public uint PriorityClass;public uint SchedulingClass;}
    [StructLayout(LayoutKind.Sequential)]private struct IO_COUNTERS{public ulong ReadOperationCount,WriteOperationCount,OtherOperationCount,ReadTransferCount,WriteTransferCount,OtherTransferCount;}
    [StructLayout(LayoutKind.Sequential)]private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION{public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;public IO_COUNTERS IoInfo;public UIntPtr ProcessMemoryLimit;public UIntPtr JobMemoryLimit;public UIntPtr PeakProcessMemoryUsed;public UIntPtr PeakJobMemoryUsed;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes,string? lpName);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern bool SetInformationJobObject(IntPtr hJob,int infoClass,IntPtr lpJobObjectInfo,uint cbJobObjectInfoLength);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll")]private static extern bool CloseHandle(IntPtr handle);
}
