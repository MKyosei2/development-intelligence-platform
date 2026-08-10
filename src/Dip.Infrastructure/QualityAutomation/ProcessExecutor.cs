using System.Diagnostics;
using System.Text;

namespace Dip.Infrastructure.QualityAutomation;

public sealed record ProcessExecutionResult(
    string Executable,
    IReadOnlyList<string> Arguments,
    int? ExitCode,
    bool TimedOut,
    long DurationMs,
    string StandardOutput,
    string StandardError,
    string StandardOutputPath,
    string StandardErrorPath);

public sealed class ProcessExecutor
{
    public async Task<ProcessExecutionResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string,string>? environment,
        TimeSpan timeout,
        string stdoutPath,
        string stderrPath,
        CancellationToken cancellationToken,
        Func<Process,CancellationToken,Task>? beforeTimeoutKill = null)
    {
        var args=arguments.ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(stdoutPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(stderrPath)!);
        var psi=new ProcessStartInfo
        {
            FileName=executable,
            WorkingDirectory=workingDirectory,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            UseShellExecute=false,
            CreateNoWindow=true
        };
        foreach(var arg in args)psi.ArgumentList.Add(arg);
        if(environment is not null)foreach(var pair in environment)psi.Environment[pair.Key]=pair.Value;
        using var process=new Process{StartInfo=psi,EnableRaisingEvents=true};
        var stopwatch=Stopwatch.StartNew();
        try
        {
            if(!process.Start())throw new InvalidOperationException($"Failed to start {executable}.");
        }
        catch(Exception ex)
        {
            await File.WriteAllTextAsync(stderrPath,ex.ToString(),cancellationToken);
            return new(executable,args,null,false,stopwatch.ElapsedMilliseconds,string.Empty,ex.ToString(),stdoutPath,stderrPath);
        }

        var stdoutTask=process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask=process.StandardError.ReadToEndAsync(cancellationToken);
        var timedOut=false;
        using var timeoutCts=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try{await process.WaitForExitAsync(timeoutCts.Token);}
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested)
        {
            timedOut=true;
            if(beforeTimeoutKill is not null)
            {
                try{await beforeTimeoutKill(process,CancellationToken.None);}catch{}
            }
            try{process.Kill(entireProcessTree:true);}catch{}
            try{await process.WaitForExitAsync(CancellationToken.None);}catch{}
        }
        var stdout=await stdoutTask;var stderr=await stderrTask;stopwatch.Stop();
        await File.WriteAllTextAsync(stdoutPath,stdout,cancellationToken);
        await File.WriteAllTextAsync(stderrPath,stderr,cancellationToken);
        return new(executable,args,timedOut?null:process.ExitCode,timedOut,stopwatch.ElapsedMilliseconds,stdout,stderr,stdoutPath,stderrPath);
    }

    public static string? FindExecutable(string name)
    {
        if(Path.IsPathRooted(name)&&File.Exists(name))return name;
        var candidates=new List<string>{name};
        if(OperatingSystem.IsWindows()&&Path.GetExtension(name).Length==0)candidates.AddRange([name+".exe",name+".cmd",name+".bat"]);
        foreach(var directory in (Environment.GetEnvironmentVariable("PATH")??string.Empty).Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries))
            foreach(var candidate in candidates){var path=Path.Combine(directory.Trim('"'),candidate);if(File.Exists(path))return path;}
        return null;
    }
}
