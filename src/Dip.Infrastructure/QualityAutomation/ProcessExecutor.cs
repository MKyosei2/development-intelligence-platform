using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Dip.Application.Abstractions.Observability;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.QualityAutomation;

public sealed record ProcessSandboxReport(bool Enabled,bool WorkspaceBoundaryEnforced,bool EnvironmentSanitized,bool CpuLimitApplied,bool MemoryLimitApplied,bool NetworkIsolationVerified,long PeakWorkingSetMb,bool OutputTruncated,string Detail);
public sealed record ProcessExecutionResult(string Executable,IReadOnlyList<string> Arguments,int? ExitCode,bool TimedOut,long DurationMs,string StandardOutput,string StandardError,string StandardOutputPath,string StandardErrorPath,ProcessSandboxReport? Sandbox=null,long CpuTimeMs=0);

public sealed class ProcessExecutor
{
    private static readonly HashSet<string> ExternalQaTools=new(new[]{"dotnet","python","python3","py","pytest","ruff","mypy","cmake","ctest","ninja","msbuild","clang","clang++","clang-tidy","cppcheck","gcc","g++","node","npm","npx","pnpm","yarn","tsc","go","cargo","rustc","javac","java","kotlinc","ruby","php","bash","pwsh","powershell","luac","lua","swiftc","sqlfluff","glslangvalidator","dxc","fxc"},StringComparer.OrdinalIgnoreCase);
    private readonly QaExecutionOptions _options;
    private readonly IDipOperationalMetrics? _metrics;
    public ProcessExecutor(IOptions<QaExecutionOptions>? options=null,IDipOperationalMetrics? metrics=null){_options=options?.Value??new QaExecutionOptions();_metrics=metrics;}

    public Task<ProcessExecutionResult> RunAsync(string executable,IEnumerable<string> arguments,string workingDirectory,IReadOnlyDictionary<string,string>? environment,TimeSpan timeout,string stdoutPath,string stderrPath,CancellationToken cancellationToken,Func<Process,CancellationToken,Task>? beforeTimeoutKill=null)
        =>RunCoreAsync(executable,arguments,workingDirectory,environment,timeout,stdoutPath,stderrPath,cancellationToken,beforeTimeoutKill,false);

    public Task<ProcessExecutionResult> RunQaAsync(string executable,IEnumerable<string> arguments,string workingDirectory,IReadOnlyDictionary<string,string>? environment,TimeSpan timeout,string stdoutPath,string stderrPath,CancellationToken cancellationToken,Func<Process,CancellationToken,Task>? beforeTimeoutKill=null)
        =>RunCoreAsync(executable,arguments,workingDirectory,environment,timeout,stdoutPath,stderrPath,cancellationToken,beforeTimeoutKill,_options.SandboxEnabled);

    private async Task<ProcessExecutionResult> RunCoreAsync(string executable,IEnumerable<string> arguments,string workingDirectory,IReadOnlyDictionary<string,string>? environment,TimeSpan timeout,string stdoutPath,string stderrPath,CancellationToken cancellationToken,Func<Process,CancellationToken,Task>? beforeTimeoutKill,bool sandbox)
    {
        var args=arguments.ToArray();var workspace=Path.GetFullPath(workingDirectory);if(!Directory.Exists(workspace))throw new DirectoryNotFoundException(workspace);
        try{if(sandbox){ValidateExecutable(executable,workspace);ValidatePathArguments(args,workspace);}if(sandbox&&_options.RequireExternalNetworkIsolation&&!NetworkIsolationVerified())throw new InvalidOperationException("QA requires network isolation, but DIP_QA_NETWORK_ISOLATED=1 was not provided by the external sandbox/container runtime.");}
        catch(InvalidOperationException ex) when(sandbox){_metrics?.GovernanceSandboxDenied(ClassifySandboxDenial(ex.Message));throw;}
        Directory.CreateDirectory(Path.GetDirectoryName(stdoutPath)!);Directory.CreateDirectory(Path.GetDirectoryName(stderrPath)!);var psi=new ProcessStartInfo{FileName=executable,WorkingDirectory=workspace,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};foreach(var arg in args)psi.ArgumentList.Add(arg);
        if(sandbox&&_options.SanitizeEnvironment)SanitizeEnvironment(psi.Environment,environment);else if(environment is not null)foreach(var pair in environment)psi.Environment[pair.Key]=pair.Value;
        using var process=new Process{StartInfo=psi,EnableRaisingEvents=true};var sw=Stopwatch.StartNew();IntPtr job=IntPtr.Zero;var cpuApplied=false;var memoryApplied=false;long peakMb=0;var truncated=false;
        try{if(!process.Start())throw new InvalidOperationException($"Failed to start {executable}.");if(sandbox&&OperatingSystem.IsWindows()){job=CreateJobObject(IntPtr.Zero,null);if(job!=IntPtr.Zero){cpuApplied=ApplyCpuLimit(job,_options.SandboxCpuPercent);memoryApplied=ApplyMemoryAndKillOnClose(job,_options.SandboxMemoryLimitMb);AssignProcessToJobObject(job,process.Handle);}}}
        catch(Exception ex){if(job!=IntPtr.Zero)CloseHandle(job);await File.WriteAllTextAsync(stderrPath,ex.ToString(),cancellationToken);return new(executable,args,null,false,sw.ElapsedMilliseconds,"",ex.ToString(),stdoutPath,stderrPath,new(sandbox,sandbox,sandbox&&_options.SanitizeEnvironment,cpuApplied,memoryApplied,NetworkIsolationVerified(),peakMb,false,"Process start failed."),0);}
        var stdoutTask=ReadCappedAsync(process.StandardOutput,Math.Max(8192,_options.MaximumOutputCharacters),cancellationToken);var stderrTask=ReadCappedAsync(process.StandardError,Math.Max(8192,_options.MaximumOutputCharacters),cancellationToken);var timedOut=false;var resourceExceeded=false;using var timeoutCts=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);timeoutCts.CancelAfter(timeout);
        try
        {
            while(!process.HasExited){timeoutCts.Token.ThrowIfCancellationRequested();try{peakMb=Math.Max(peakMb,process.WorkingSet64/1024/1024);if(sandbox&&!OperatingSystem.IsWindows()&&_options.SandboxMemoryLimitMb>0&&peakMb>_options.SandboxMemoryLimitMb){resourceExceeded=true;try{process.Kill(true);}catch{}break;}}catch{}await Task.Delay(100,timeoutCts.Token);}if(!process.HasExited)await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested){timedOut=true;if(beforeTimeoutKill is not null)try{await beforeTimeoutKill(process,CancellationToken.None);}catch{}try{process.Kill(true);}catch{}try{await process.WaitForExitAsync(CancellationToken.None);}catch{}}
        catch(OperationCanceledException){try{process.Kill(true);}catch{}try{await process.WaitForExitAsync(CancellationToken.None);}catch{}throw;}
        var outResult=await stdoutTask;var errResult=await stderrTask;truncated=outResult.Truncated||errResult.Truncated;sw.Stop();await File.WriteAllTextAsync(stdoutPath,outResult.Text,CancellationToken.None);await File.WriteAllTextAsync(stderrPath,errResult.Text,CancellationToken.None);int? exit=null;try{exit=timedOut||resourceExceeded?null:process.ExitCode;}catch{}
        long cpuTimeMs=0;try{cpuTimeMs=Math.Max(0L,(long)process.TotalProcessorTime.TotalMilliseconds);}catch{}if(job!=IntPtr.Zero)CloseHandle(job);var detail=resourceExceeded?$"Memory budget exceeded ({peakMb} MiB > {_options.SandboxMemoryLimitMb} MiB).":timedOut?"Execution timed out and the complete process tree was terminated.":"Sandbox policy applied.";var report=new ProcessSandboxReport(sandbox,sandbox,sandbox&&_options.SanitizeEnvironment,cpuApplied,memoryApplied,NetworkIsolationVerified(),peakMb,truncated,detail);return new(executable,args,exit,timedOut,sw.ElapsedMilliseconds,outResult.Text,errResult.Text,stdoutPath,stderrPath,report,cpuTimeMs);
    }

    private static void ValidateExecutable(string executable,string workspace)
    {
        var canonicalWorkspace=CanonicalDirectory(workspace);var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(Path.IsPathRooted(executable))
        {
            var full=Path.GetFullPath(executable);var canonical=CanonicalFile(full);if(IsInside(canonicalWorkspace,canonical,comparison))return;var name=Path.GetFileNameWithoutExtension(full);if(ExternalQaTools.Contains(name)){var resolved=FindExecutable(Path.GetFileName(full))??FindExecutable(name);if(!string.IsNullOrWhiteSpace(resolved)&&string.Equals(CanonicalFile(resolved),canonical,comparison))return;}throw new InvalidOperationException($"QA executable outside workspace is not the canonical trusted PATH tool: {full}");
        }
        if(executable.Contains(Path.DirectorySeparatorChar)||executable.Contains(Path.AltDirectorySeparatorChar)){var full=Path.GetFullPath(Path.Combine(workspace,executable));var canonical=CanonicalFile(full);if(!IsInside(canonicalWorkspace,canonical,comparison))throw new InvalidOperationException("QA executable escapes the workspace, including through a symbolic link.");return;}if(!ExternalQaTools.Contains(Path.GetFileNameWithoutExtension(executable)))throw new InvalidOperationException($"QA external tool '{executable}' is not allowlisted. Put custom runners inside the workspace or extend the trusted runner image.");
    }
    private static void ValidatePathArguments(IEnumerable<string> arguments,string workspace)
    {
        var canonicalWorkspace=CanonicalDirectory(workspace);var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;foreach(var argument in arguments){var candidate=ArgumentPathCandidate(argument);if(candidate is null)continue;var full=Path.GetFullPath(candidate);var canonical=File.Exists(full)?CanonicalFile(full):Directory.Exists(full)?CanonicalDirectory(full):full;if(!IsInside(canonicalWorkspace,canonical,comparison))throw new InvalidOperationException($"QA argument path escapes the workspace: {candidate}");}
    }
    private static string? ArgumentPathCandidate(string argument)
    {
        if(string.IsNullOrWhiteSpace(argument))return null;if(Path.IsPathFullyQualified(argument))return argument;var equals=argument.IndexOf('=');if(equals>0&&equals<argument.Length-1){var value=argument[(equals+1)..].Trim('"');if(Path.IsPathFullyQualified(value))return value;}return null;
    }
    private static string CanonicalFile(string path){var full=Path.GetFullPath(path);try{return new FileInfo(full).ResolveLinkTarget(true)?.FullName??full;}catch{return full;}}
    private static string CanonicalDirectory(string path){var full=Path.GetFullPath(path);try{return new DirectoryInfo(full).ResolveLinkTarget(true)?.FullName??full;}catch{return full;}}
    private static bool IsInside(string root,string path,StringComparison comparison)=>path.Equals(root,comparison)||path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,comparison);
    private static void SanitizeEnvironment(IDictionary<string,string?> target,IReadOnlyDictionary<string,string>? supplied){var source=target.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);target.Clear();foreach(var key in new[]{"PATH","SystemRoot","WINDIR","HOME","USERPROFILE","TEMP","TMP","TMPDIR","DOTNET_ROOT","DOTNET_ROOT_X64","JAVA_HOME","GOPATH","GOROOT","NUGET_PACKAGES"})if(source.TryGetValue(key,out var value)&&!string.IsNullOrWhiteSpace(value))target[key]=value;if(supplied is not null)foreach(var pair in supplied)if(!IsSecretName(pair.Key))target[pair.Key]=pair.Value;target["DIP_QA_SANDBOX"]="1";}
    private static bool IsSecretName(string key)=>key.Contains("TOKEN",StringComparison.OrdinalIgnoreCase)||key.Contains("SECRET",StringComparison.OrdinalIgnoreCase)||key.Contains("PASSWORD",StringComparison.OrdinalIgnoreCase)||key.Contains("API_KEY",StringComparison.OrdinalIgnoreCase)||key.Contains("CONNECTIONSTRING",StringComparison.OrdinalIgnoreCase);
    private static bool NetworkIsolationVerified()=>string.Equals(Environment.GetEnvironmentVariable("DIP_QA_NETWORK_ISOLATED"),"1",StringComparison.Ordinal);
    private static string ClassifySandboxDenial(string message)=>message.Contains("network isolation",StringComparison.OrdinalIgnoreCase)?"network-isolation":message.Contains("workspace",StringComparison.OrdinalIgnoreCase)?"workspace-boundary":message.Contains("allowlist",StringComparison.OrdinalIgnoreCase)||message.Contains("trusted PATH",StringComparison.OrdinalIgnoreCase)?"executable-policy":"sandbox-policy";
    private static async Task<(string Text,bool Truncated)> ReadCappedAsync(StreamReader reader,int max,CancellationToken ct){var sb=new StringBuilder(Math.Min(max,65536));var buffer=new char[8192];var truncated=false;while(true){var n=await reader.ReadAsync(buffer.AsMemory(0,buffer.Length),ct);if(n<=0)break;var remaining=max-sb.Length;if(remaining>0)sb.Append(buffer,0,Math.Min(remaining,n));if(n>remaining)truncated=true;}if(truncated)sb.Append("\n[DIP: output truncated by QA sandbox]\n");return(sb.ToString(),truncated);}

    public static string? FindExecutable(string name){if(Path.IsPathRooted(name)&&File.Exists(name))return name;var candidates=new List<string>{name};if(OperatingSystem.IsWindows()&&Path.GetExtension(name).Length==0)candidates.AddRange([name+".exe",name+".cmd",name+".bat"]);foreach(var directory in(Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries))foreach(var candidate in candidates){var path=Path.Combine(directory.Trim('"'),candidate);if(File.Exists(path))return path;}return null;}

    private static bool ApplyCpuLimit(IntPtr job,int percent){var info=new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION{ControlFlags=0x1|0x4,CpuRate=(uint)(Math.Clamp(percent,1,100)*100)};return SetJobInfo(job,15,info);}
    private static bool ApplyMemoryAndKillOnClose(IntPtr job,int memoryMb){var info=new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();info.BasicLimitInformation.LimitFlags=0x2000;if(memoryMb>0){info.BasicLimitInformation.LimitFlags|=0x100;info.ProcessMemoryLimit=(UIntPtr)((ulong)memoryMb*1024UL*1024UL);}var applied=SetJobInfo(job,9,info);return applied&&memoryMb>0;}
    private static bool SetJobInfo<T>(IntPtr job,int cls,T value) where T:struct{var size=Marshal.SizeOf<T>();var ptr=Marshal.AllocHGlobal(size);try{Marshal.StructureToPtr(value,ptr,false);return SetInformationJobObject(job,cls,ptr,(uint)size);}finally{Marshal.FreeHGlobal(ptr);}}
    [StructLayout(LayoutKind.Sequential)]private struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION{public uint ControlFlags;public uint CpuRate;}[StructLayout(LayoutKind.Sequential)]private struct JOBOBJECT_BASIC_LIMIT_INFORMATION{public long PerProcessUserTimeLimit;public long PerJobUserTimeLimit;public uint LimitFlags;public UIntPtr MinimumWorkingSetSize;public UIntPtr MaximumWorkingSetSize;public uint ActiveProcessLimit;public long Affinity;public uint PriorityClass;public uint SchedulingClass;}[StructLayout(LayoutKind.Sequential)]private struct IO_COUNTERS{public ulong ReadOperationCount,WriteOperationCount,OtherOperationCount,ReadTransferCount,WriteTransferCount,OtherTransferCount;}[StructLayout(LayoutKind.Sequential)]private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION{public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;public IO_COUNTERS IoInfo;public UIntPtr ProcessMemoryLimit;public UIntPtr JobMemoryLimit;public UIntPtr PeakProcessMemoryUsed;public UIntPtr PeakJobMemoryUsed;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes,string? lpName);[DllImport("kernel32.dll",SetLastError=true)]private static extern bool SetInformationJobObject(IntPtr hJob,int infoClass,IntPtr lpJobObjectInfo,uint cbJobObjectInfoLength);[DllImport("kernel32.dll",SetLastError=true)]private static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);[DllImport("kernel32.dll")]private static extern bool CloseHandle(IntPtr h);
}
