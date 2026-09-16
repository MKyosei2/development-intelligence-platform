// Purpose: Provides the R ep ro du ct io nC oo rd in at or infrastructure implementation.
using System.Diagnostics;
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class ReproductionCoordinator(
    ProcessExecutor executor,
    EnvironmentSnapshotService snapshots,
    UniversalDiagnosticNormalizer normalizer,
    IOptions<QaExecutionOptions> options):IReproductionCoordinator
{
    private readonly QaExecutionOptions _options=options.Value;

    public async Task<ReproductionResult> ReproduceAsync(ReproductionScenario scenario,CancellationToken ct)
    {
        Validate(scenario);
        var results=new List<ReproductionAttemptResult>();
        for(var attempt=1;attempt<=Math.Clamp(scenario.AttemptCount,1,5);attempt++)
            results.Add(await RunAttemptAsync(scenario,attempt,ct));

        var reproduced=results.Count(x=>x.Reproduced);
        var required=Math.Max(1,(results.Count+1)/2); // majority for 3+ runs, one hit for 1-2
        var status=reproduced>=required
            ?ReproductionStatus.Reproduced
            :results.Any(x=>x.Status==ReproductionStatus.EnvironmentFailure)
                ?ReproductionStatus.EnvironmentFailure
                :results.All(x=>x.TimedOut)
                    ?ReproductionStatus.TimedOut
                    :ReproductionStatus.NotReproduced;
        return new(status,reproduced,results.Count,results);
    }

    private static void Validate(ReproductionScenario scenario)
    {
        if(scenario.ProjectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(scenario));
        if(scenario.FindingId==Guid.Empty)throw new ArgumentException("FindingId is required.",nameof(scenario));
        if(string.IsNullOrWhiteSpace(scenario.WorkspacePath)||!Directory.Exists(scenario.WorkspacePath))throw new DirectoryNotFoundException("Reproduction workspace does not exist.");
        if(string.IsNullOrWhiteSpace(scenario.QaRunArtifactRoot))throw new ArgumentException("QaRunArtifactRoot is required.",nameof(scenario));
        if(string.IsNullOrWhiteSpace(scenario.Executable)||scenario.Executable.Length>2000)throw new ArgumentException("Executable is invalid.",nameof(scenario));
        if(scenario.Arguments.Count>128||scenario.Arguments.Any(x=>x.Length>4096))throw new ArgumentException("Reproduction arguments exceed safety limits.",nameof(scenario));
        if(scenario.EnvironmentVariables.Count>128||scenario.EnvironmentVariables.Any(x=>x.Key.Length>256||x.Value.Length>8192))throw new ArgumentException("Reproduction environment exceeds safety limits.",nameof(scenario));
        if(string.IsNullOrWhiteSpace(scenario.ExpectedFingerprint)||scenario.ExpectedFingerprint.Length>512)throw new ArgumentException("ExpectedFingerprint is invalid.",nameof(scenario));
    }

    private async Task<ReproductionAttemptResult> RunAttemptAsync(ReproductionScenario scenario,int attempt,CancellationToken ct)
    {
        var baseRoot=Path.GetFullPath(scenario.QaRunArtifactRoot);
        var dir=Path.Combine(baseRoot,"reproduction",scenario.FindingId.ToString("N"),$"attempt-{attempt:000}");Directory.CreateDirectory(dir);
        var stdout=Path.Combine(dir,"stdout.log");var stderr=Path.Combine(dir,"stderr.log");var envPath=Path.Combine(dir,"environment.json");var resultPath=Path.Combine(dir,"result.json");
        var video=Path.Combine(dir,"reproduction.mp4");var audio=Path.Combine(dir,"system-audio.wav");var stopFile=Path.Combine(dir,"capture.stop");var dump=Path.Combine(dir,"crash.dmp");
        await snapshots.CaptureAsync(scenario.WorkspacePath,scenario.Executable,envPath,ct);
        Process? capture=null;
        if(OperatingSystem.IsWindows()&&scenario.RecordScreen)
            capture=StartCapture(video,scenario.RecordSystemAudio?audio:null,stopFile,scenario.TimeoutSeconds+15,scenario.WorkspacePath);

        var sw=Stopwatch.StartNew();ProcessExecutionResult execution;
        try
        {
            var (exe,args,useWatchdog)=BuildExecution(scenario,dump,stdout,stderr);
            execution=await executor.RunQaAsync(exe,args,scenario.WorkspacePath,scenario.EnvironmentVariables,TimeSpan.FromSeconds(Math.Clamp(scenario.TimeoutSeconds,5,1800)),stdout,stderr,ct,
                async(process,_)=>await TryCollectManagedEvidenceAsync(scenario.Language,process.Id,dump,scenario.WorkspacePath));
            if(useWatchdog)
            {
                var childOut=Path.Combine(dir,"child-stdout.log");var childErr=Path.Combine(dir,"child-stderr.log");
                if(File.Exists(childOut))execution=execution with{StandardOutput=await File.ReadAllTextAsync(childOut,ct),StandardOutputPath=childOut};
                if(File.Exists(childErr))execution=execution with{StandardError=await File.ReadAllTextAsync(childErr,ct),StandardErrorPath=childErr};
            }
        }
        catch(Exception ex)
        {
            execution=new(scenario.Executable,scenario.Arguments,null,false,sw.ElapsedMilliseconds,string.Empty,ex.ToString(),stdout,stderr);await File.WriteAllTextAsync(stderr,ex.ToString(),ct);
        }
        finally
        {
            try{await File.WriteAllTextAsync(stopFile,"stop",CancellationToken.None);}catch{}
            if(capture is not null)await FinalizeCaptureAsync(capture);
        }
        sw.Stop();
        var diagnostics=normalizer.NormalizeText(scenario.Language,"reproduction",execution.StandardOutput,execution.StandardError,execution.TimedOut);
        var matches=diagnostics.Any(x=>QaFingerprint.Create(scenario.Language,x)==scenario.ExpectedFingerprint);
        var crashed=!execution.TimedOut&&diagnostics.Any(x=>x.Category is "runtime" or "native-crash" or "memory-safety" or "undefined-behavior");
        var status=execution.TimedOut?(matches?ReproductionStatus.Reproduced:ReproductionStatus.TimedOut):matches?ReproductionStatus.Reproduced:ReproductionStatus.NotReproduced;
        var payload=new{attempt,reproduced=matches,crashed,timedOut=execution.TimedOut,execution.ExitCode,durationMs=execution.DurationMs,video=File.Exists(video)?video:null,audio=File.Exists(audio)?audio:null,dump=File.Exists(dump)?dump:null,environment=envPath,stdout=execution.StandardOutputPath,stderr=execution.StandardErrorPath,expectedFingerprint=scenario.ExpectedFingerprint};
        var json=JsonSerializer.Serialize(payload,new JsonSerializerOptions{WriteIndented=true});await File.WriteAllTextAsync(resultPath,json,ct);
        return new(attempt,status,matches,crashed,execution.TimedOut,execution.ExitCode,execution.DurationMs,dir,File.Exists(video)?video:null,File.Exists(audio)?audio:null,File.Exists(dump)?dump:null,json);
    }

    private (string Executable,IReadOnlyList<string> Arguments,bool Watchdog) BuildExecution(ReproductionScenario scenario,string dump,string stdout,string stderr)
    {
        if(scenario.Language==LanguageId.Python&&scenario.Executable.EndsWith(".py",StringComparison.OrdinalIgnoreCase))
        {
            var python=ProcessExecutor.FindExecutable("python")??ProcessExecutor.FindExecutable("python3")??throw new FileNotFoundException("Python executable not found for reproduction.");
            return(python,new[]{scenario.Executable}.Concat(scenario.Arguments).ToArray(),false);
        }
        if(scenario.Language==LanguageId.CSharp&&scenario.Executable.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))
        {
            var dotnet=ProcessExecutor.FindExecutable("dotnet")??throw new FileNotFoundException("dotnet executable not found for reproduction.");
            return(dotnet,new[]{scenario.Executable}.Concat(scenario.Arguments).ToArray(),false);
        }
        if(OperatingSystem.IsWindows()&&scenario.Language==LanguageId.Cpp)
        {
            var watchdog=ResolveConfigured(_options.CrashWatchdogPath);
            if(File.Exists(watchdog))
            {
                var dir=Path.GetDirectoryName(stdout)!;var childOut=Path.Combine(dir,"child-stdout.log");var childErr=Path.Combine(dir,"child-stderr.log");
                var args=new List<string>{"--dump",dump,"--stdout",childOut,"--stderr",childErr,"--",scenario.Executable};args.AddRange(scenario.Arguments);return(watchdog,args,true);
            }
        }
        return(scenario.Executable,scenario.Arguments,false);
    }

    private Process? StartCapture(string video,string? audio,string stopFile,int maxSeconds,string workspace)
    {
        try
        {
            var path=ResolveConfigured(_options.CaptureAgentPath);if(!File.Exists(path))return null;
            var psi=new ProcessStartInfo{FileName=path,WorkingDirectory=workspace,UseShellExecute=false,CreateNoWindow=true};
            psi.ArgumentList.Add("--video");psi.ArgumentList.Add(video);if(audio is not null){psi.ArgumentList.Add("--audio");psi.ArgumentList.Add(audio);}psi.ArgumentList.Add("--stop-file");psi.ArgumentList.Add(stopFile);psi.ArgumentList.Add("--max-seconds");psi.ArgumentList.Add(maxSeconds.ToString());
            return Process.Start(psi);
        }catch{return null;}
    }

    private async Task FinalizeCaptureAsync(Process capture)
    {
        try{using(capture){using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(8));await capture.WaitForExitAsync(cts.Token);}}catch{try{capture.Kill(true);}catch{}}
    }

    private async Task TryCollectManagedEvidenceAsync(LanguageId language,int pid,string dump,string workspace)
    {
        if(language!=LanguageId.CSharp)return;var root=Path.GetDirectoryName(dump)!;
        var stackTool=ProcessExecutor.FindExecutable("dotnet-stack");
        if(stackTool is not null)
        {
            var tmp=Path.Combine(root,"dotnet-stack");Directory.CreateDirectory(tmp);
            var stack=await executor.RunAsync(stackTool,["report","--process-id",pid.ToString()],workspace,null,TimeSpan.FromSeconds(12),Path.Combine(tmp,"stdout.log"),Path.Combine(tmp,"stderr.log"),CancellationToken.None);
            if(stack.ExitCode==0)await File.WriteAllTextAsync(Path.Combine(root,"managed-stack.txt"),stack.StandardOutput,CancellationToken.None);
        }
        var tool=ProcessExecutor.FindExecutable("dotnet-dump");if(tool is null)return;
        var dumpDir=Path.Combine(root,"dotnet-dump");Directory.CreateDirectory(dumpDir);
        await executor.RunAsync(tool,["collect","--process-id",pid.ToString(),"--output",dump,"--type","Full"],workspace,null,TimeSpan.FromSeconds(20),Path.Combine(dumpDir,"stdout.log"),Path.Combine(dumpDir,"stderr.log"),CancellationToken.None);
    }

    private static string ResolveConfigured(string value)=>Path.IsPathRooted(value)?value:Path.GetFullPath(value);
}
