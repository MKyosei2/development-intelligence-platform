using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

public sealed class P2ExperimentRunner(
    ProcessExecutor executor,
    IQaWorkspaceResolver workspaceResolver) : IP2ExperimentRunner
{
    public async Task<FlakyResult> RunRepeatedAsync(RepeatedRunCommand command,CancellationToken ct)
    {
        var workspace=workspaceResolver.Resolve(command.Target.WorkspaceRelativePath);
        var executable=ResolveTargetExecutable(workspace,command.Target.Executable);
        var root=ArtifactRoot(command.Target.ProjectId,"flaky");
        var attempts=Math.Clamp(command.Attempts,2,200);
        var runs=new List<FlakyRunAttempt>(attempts);

        for(var i=1;i<=attempts;i++)
        {
            ct.ThrowIfCancellationRequested();
            var dir=Path.Combine(root,$"attempt-{i:D3}");Directory.CreateDirectory(dir);
            var r=await executor.RunAsync(
                executable,command.Target.Arguments??Array.Empty<string>(),workspace,null,
                TimeSpan.FromSeconds(Math.Clamp(command.Target.TimeoutSeconds,1,3600)),
                Path.Combine(dir,"stdout.log"),Path.Combine(dir,"stderr.log"),ct);
            if(!r.TimedOut && r.ExitCode is null)
            {
                runs.Add(new(i,false,false,null,r.DurationMs,Fingerprint(r.StandardError)));
                return new("InfrastructureFailure",attempts,runs.Count(x=>x.Passed),runs.Count(x=>!x.Passed),0,0m,0m,runs,RelativeArtifact(root));
            }
            var passed=!r.TimedOut&&r.ExitCode==0;
            runs.Add(new(i,passed,r.TimedOut,r.ExitCode,r.DurationMs,Fingerprint(r.StandardOutput+"\n"+r.StandardError)));
        }

        var failed=runs.Count(x=>!x.Passed);var timedOut=runs.Count(x=>x.TimedOut);
        var rate=decimal.Round((decimal)failed/attempts*100m,2);
        var status=rate>=command.FlakyFailurePercent?"Failed":rate>=command.FlakyWarningPercent?"Warning":"Passed";
        var confidence=decimal.Round(Math.Min(99m,55m+(attempts-2)*2m),2);
        return new(status,attempts,attempts-failed,failed,timedOut,rate,confidence,runs,RelativeArtifact(root));
    }

    public async Task<SoakResult> RunSoakAsync(SoakCommand command,CancellationToken ct)
    {
        var workspace=workspaceResolver.Resolve(command.Target.WorkspaceRelativePath);
        var executable=ResolveTargetExecutable(workspace,command.Target.Executable);
        var root=ArtifactRoot(command.Target.ProjectId,"soak");
        var stdoutPath=Path.Combine(root,"stdout.log");var stderrPath=Path.Combine(root,"stderr.log");
        var psi=new ProcessStartInfo
        {
            FileName=executable,WorkingDirectory=workspace,RedirectStandardOutput=true,RedirectStandardError=true,
            UseShellExecute=false,CreateNoWindow=true
        };
        foreach(var a in command.Target.Arguments??Array.Empty<string>())psi.ArgumentList.Add(a);
        using var process=new Process{StartInfo=psi,EnableRaisingEvents=true};
        var samples=new List<SoakTelemetryPoint>();
        var fpsSamples=new ConcurrentQueue<(int Second,decimal Fps,decimal? Frame)>();
        var stdout=new StringBuilder();var stderr=new StringBuilder();var sw=Stopwatch.StartNew();
        bool crashed=false,timedOut=false;
        try
        {
            if(!process.Start())throw new InvalidOperationException("Failed to start soak target.");
        }
        catch(Exception ex)
        {
            await File.WriteAllTextAsync(stderrPath,ex.ToString(),ct);
            return new("InfrastructureFailure",0,0,0,null,null,null,false,false,[],RelativeArtifact(root));
        }

        var outTask=ReadLinesAsync(process.StandardOutput,stdout,fpsSamples,sw,ct);
        var errTask=ReadLinesAsync(process.StandardError,stderr,null,sw,ct);
        var duration=Math.Clamp(command.DurationSeconds,5,86400);
        var interval=Math.Clamp(command.SampleIntervalSeconds,1,60);
        var startMemory=Math.Max(0,process.WorkingSet64/1024/1024);
        var lastCpu=process.TotalProcessorTime;var lastAt=sw.Elapsed;

        while(!process.HasExited&&sw.Elapsed<TimeSpan.FromSeconds(duration))
        {
            await Task.Delay(TimeSpan.FromSeconds(interval),ct);
            if(process.HasExited)break;
            try
            {
                process.Refresh();
                var nowCpu=process.TotalProcessorTime;var nowAt=sw.Elapsed;
                var wall=Math.Max(.001,(nowAt-lastAt).TotalSeconds);
                var cpu=decimal.Round((decimal)((nowCpu-lastCpu).TotalSeconds/wall/Math.Max(1,Environment.ProcessorCount)*100d),2);
                var sec=(int)sw.Elapsed.TotalSeconds;
                var nearest=fpsSamples.Where(x=>Math.Abs(x.Second-sec)<=interval*2).LastOrDefault();
                samples.Add(new(sec,Math.Max(0,process.WorkingSet64/1024/1024),cpu,nearest.Fps==0?null:nearest.Fps,nearest.Frame));
                lastCpu=nowCpu;lastAt=nowAt;
            }catch{}
        }

        if(!process.HasExited)
        {
            // Reaching the configured soak window is success for the harness, not a timeout.
            // The target is terminated only because enough sustained evidence has been collected.
            try{process.Kill(entireProcessTree:true);}catch{}
        }
        try{await process.WaitForExitAsync(CancellationToken.None);}catch{}
        try{await Task.WhenAll(outTask,errTask);}catch{}
        sw.Stop();
        await File.WriteAllTextAsync(stdoutPath,stdout.ToString(),CancellationToken.None);
        await File.WriteAllTextAsync(stderrPath,stderr.ToString(),CancellationToken.None);
        crashed=!timedOut&&process.ExitCode!=0;

        var endMemory=samples.Count>0?samples[^1].WorkingSetMb:startMemory;
        var memoryGrowth=startMemory<=0?0m:decimal.Round((decimal)(endMemory-startMemory)/startMemory*100m,2);
        var allFps=fpsSamples.Select(x=>x.Fps).Where(x=>x>0).ToArray();
        decimal? startFps=allFps.Length>0?allFps.Take(Math.Max(1,allFps.Length/4)).Average():null;
        decimal? endFps=allFps.Length>0?allFps.Skip(Math.Max(0,allFps.Length*3/4)).DefaultIfEmpty().Average():null;
        decimal? drift=startFps is >0m&&endFps is not null?decimal.Round((startFps.Value-endFps.Value)/startFps.Value*100m,2):null;
        var status=crashed?"Failed"
            :memoryGrowth>=command.MemoryGrowthWarningPercent||drift>=command.PerformanceDriftWarningPercent?"Warning"
            :"Passed";
        return new(status,startMemory,endMemory,memoryGrowth,startFps,endFps,drift,crashed,timedOut,samples,RelativeArtifact(root));
    }

    public async Task<GitBisectResult> FindRegressionCommitAsync(GitBisectCommand command,CancellationToken ct)
    {
        var workspace=workspaceResolver.Resolve(command.Target.WorkspaceRelativePath);
        var git=ProcessExecutor.FindExecutable("git");
        var root=ArtifactRoot(command.Target.ProjectId,"regression-bisect");
        if(git is null)return new("InfrastructureFailure",null,null,0,[],RelativeArtifact(root));

        var rev=await RunGitAsync(git,workspace,root,["rev-list","--ancestry-path","--reverse",$"{command.GoodCommit}..{command.BadCommit}"],ct);
        if(rev.ExitCode!=0)return new("InfrastructureFailure",null,null,0,[],RelativeArtifact(root));
        var commits=rev.StandardOutput.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).ToList();
        if(commits.Count==0)return new("Unknown",null,command.GoodCommit,0,[],RelativeArtifact(root));
        if(!commits.Contains(command.BadCommit,StringComparer.OrdinalIgnoreCase))commits.Add(command.BadCommit);

        var history=new List<GitBisectEvaluation>();
        var low=-1;var high=commits.Count-1;var eval=0;string? lastGood=command.GoodCommit;
        while(high-low>1&&eval<Math.Clamp(command.MaxEvaluations,2,100))
        {
            var mid=(low+high)/2;
            var commit=commits[mid];
            var evaluated=await EvaluateCommitAsync(git,workspace,root,commit,command.Target,ct);
            if(evaluated.Result is null)
            {
                history.Add(new(commit,"InfrastructureFailure",false,evaluated.DurationMs));
                return new("InfrastructureFailure",null,lastGood,eval+1,history,RelativeArtifact(root));
            }
            var passed=evaluated.Result.Value;
            history.Add(new(commit,passed?"Passed":"Failed",passed,evaluated.DurationMs));
            eval++;
            if(passed){low=mid;lastGood=commit;}else high=mid;
        }
        var firstBad=commits[Math.Clamp(high,0,commits.Count-1)];
        return new("Found",firstBad,lastGood,eval,history,RelativeArtifact(root));
    }

    public async Task<MutationResult> RunMutationAsync(MutationCommand command,CancellationToken ct)
    {
        var workspace=workspaceResolver.Resolve(command.Target.WorkspaceRelativePath);
        var root=ArtifactRoot(command.Target.ProjectId,"mutation");
        var temp=Path.Combine(root,"workspace");
        await PrepareIsolatedWorkspaceAsync(workspace,temp,ct);
        var executable=ResolveTargetExecutable(temp,command.Target.Executable);
        var cases=new List<MutationCaseResult>();
        var infrastructureFailure=false;
        foreach(var mutation in command.Mutations.Take(Math.Clamp(command.MaxMutations,1,200)))
        {
            ct.ThrowIfCancellationRequested();
            var file=SafePath(temp,mutation.FileRelativePath);
            if(!File.Exists(file)){cases.Add(new(mutation.Id,false,false,null,"File not found."));continue;}
            var original=await File.ReadAllTextAsync(file,ct);
            var index=original.IndexOf(mutation.OriginalText,StringComparison.Ordinal);
            if(index<0){cases.Add(new(mutation.Id,false,false,null,"OriginalText was not found."));continue;}
            var mutated=original[..index]+mutation.ReplacementText+original[(index+mutation.OriginalText.Length)..];
            await File.WriteAllTextAsync(file,mutated,ct);
            var dir=Path.Combine(root,"cases",SafeName(mutation.Id));Directory.CreateDirectory(dir);
            var r=await executor.RunAsync(executable,command.Target.Arguments??Array.Empty<string>(),temp,null,
                TimeSpan.FromSeconds(Math.Clamp(command.Target.TimeoutSeconds,1,3600)),Path.Combine(dir,"stdout.log"),Path.Combine(dir,"stderr.log"),ct);
            if(!r.TimedOut && r.ExitCode is null)
            {
                infrastructureFailure=true;
                cases.Add(new(mutation.Id,true,false,null,"Infrastructure failure starting the configured test target."));
                await File.WriteAllTextAsync(file,original,ct);
                break;
            }
            var killed=r.TimedOut||(r.ExitCode.HasValue&&r.ExitCode.Value!=0);
            cases.Add(new(mutation.Id,true,killed,r.ExitCode,killed?"Tests detected the mutation.":"Mutation survived the configured tests."));
            await File.WriteAllTextAsync(file,original,ct);
        }
        var applied=cases.Count(x=>x.Applied);var killedCount=cases.Count(x=>x.Applied&&x.Killed);
        var score=applied==0?0m:decimal.Round((decimal)killedCount/applied*100m,2);
        var status=infrastructureFailure||applied==0?"InfrastructureFailure":score>=80m?"Passed":score>=60m?"Warning":"Failed";
        return new(status,cases.Count,applied,killedCount,Math.Max(0,applied-killedCount),score,cases,RelativeArtifact(root));
    }

    public async Task<FuzzResult> RunFuzzAsync(FuzzCommand command,CancellationToken ct)
    {
        var workspace=workspaceResolver.Resolve(command.Target.WorkspaceRelativePath);
        var executable=ResolveTargetExecutable(workspace,command.Target.Executable);
        var root=ArtifactRoot(command.Target.ProjectId,"fuzz");var inputDir=Path.Combine(root,"inputs");Directory.CreateDirectory(inputDir);
        var random=new Random(command.Seed);var results=new List<FuzzCaseResult>();var infrastructureFailure=false;
        var cases=Math.Clamp(command.Cases,1,10000);var max=Math.Clamp(command.MaxInputBytes,1,1024*1024);
        for(var i=0;i<cases;i++)
        {
            ct.ThrowIfCancellationRequested();
            var size=i<8?new[]{0,1,2,8,32,128,512,Math.Min(4096,max)}[i]:random.Next(0,max+1);
            var bytes=new byte[size];random.NextBytes(bytes);
            if(i%10==0&&size>0)Array.Fill(bytes,(byte)0xFF);
            var input=Path.Combine(inputDir,$"case-{i:D5}.bin");await File.WriteAllBytesAsync(input,bytes,ct);
            var args=(command.Target.Arguments??Array.Empty<string>())
                .Select(a=>a.Replace(command.ArgumentPlaceholder,input,StringComparison.Ordinal)).ToArray();
            var dir=Path.Combine(root,"runs",$"case-{i:D5}");Directory.CreateDirectory(dir);
            var r=await executor.RunAsync(executable,args,workspace,new Dictionary<string,string>{{"DIP_FUZZ","1"},{"DIP_FUZZ_SEED",command.Seed.ToString()}},
                TimeSpan.FromSeconds(Math.Clamp(command.Target.TimeoutSeconds,1,600)),Path.Combine(dir,"stdout.log"),Path.Combine(dir,"stderr.log"),ct);
            if(!r.TimedOut && r.ExitCode is null)
            {
                infrastructureFailure=true;
                results.Add(new(i,size,null,false,false,Fingerprint(r.StandardError)));
                break;
            }
            var crashed=!r.TimedOut&&r.ExitCode.HasValue&&r.ExitCode.Value!=0;
            results.Add(new(i,size,r.ExitCode,r.TimedOut,crashed,Fingerprint(r.StandardOutput+"\n"+r.StandardError)));
        }
        var crashes=results.Count(x=>x.Crashed);var timeouts=results.Count(x=>x.TimedOut);
        var unique=results.Where(x=>x.Crashed||x.TimedOut).Select(x=>x.Fingerprint).Distinct().Count();
        var status=infrastructureFailure?"InfrastructureFailure":crashes>0?"Failed":timeouts>0?"Warning":"Passed";
        return new(status,results.Count,crashes,timeouts,unique,results,RelativeArtifact(root));
    }

    private async Task<(bool? Result,long DurationMs)> EvaluateCommitAsync(
        string git,string workspace,string root,string commit,ExperimentTarget target,CancellationToken ct)
    {
        var worktree=Path.Combine(root,"worktrees",SafeName(commit));
        if(Directory.Exists(worktree))Directory.Delete(worktree,true);
        Directory.CreateDirectory(Path.GetDirectoryName(worktree)!);
        var add=await RunGitAsync(git,workspace,root,["worktree","add","--force","--detach",worktree,commit],ct);
        if(add.ExitCode!=0)return(null,add.DurationMs);
        try
        {
            var executable=ResolveTargetExecutable(worktree,target.Executable);
            var dir=Path.Combine(root,"evaluations",SafeName(commit));Directory.CreateDirectory(dir);
            var r=await executor.RunAsync(executable,target.Arguments??Array.Empty<string>(),worktree,null,
                TimeSpan.FromSeconds(Math.Clamp(target.TimeoutSeconds,1,3600)),Path.Combine(dir,"stdout.log"),Path.Combine(dir,"stderr.log"),ct);
            if(!r.TimedOut&&r.ExitCode is null)return(null,r.DurationMs);
            return(!r.TimedOut&&r.ExitCode==0,r.DurationMs);
        }
        finally
        {
            try{await RunGitAsync(git,workspace,root,["worktree","remove","--force",worktree],CancellationToken.None);}catch{}
        }
    }

    private async Task<ProcessExecutionResult> RunGitAsync(string git,string workspace,string root,IEnumerable<string> args,CancellationToken ct)
    {
        var id=Guid.NewGuid().ToString("N");var dir=Path.Combine(root,"git");Directory.CreateDirectory(dir);
        return await executor.RunAsync(git,args,workspace,null,TimeSpan.FromMinutes(5),Path.Combine(dir,$"{id}.out.log"),Path.Combine(dir,$"{id}.err.log"),ct);
    }

    private async Task PrepareIsolatedWorkspaceAsync(string source,string target,CancellationToken ct)
    {
        if(Directory.Exists(target))Directory.Delete(target,true);
        var git=ProcessExecutor.FindExecutable("git");
        if(git is not null&&Directory.Exists(Path.Combine(source,".git")))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var root=Path.GetDirectoryName(target)!;
            var result=await RunGitAsync(git,source,root,["worktree","add","--force","--detach",target,"HEAD"],ct);
            if(result.ExitCode==0)return;
        }
        CopyDirectory(source,target);
    }

    private static void CopyDirectory(string source,string target)
    {
        Directory.CreateDirectory(target);
        foreach(var file in Directory.EnumerateFiles(source))
            File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);
        foreach(var dir in Directory.EnumerateDirectories(source))
        {
            var name=Path.GetFileName(dir);
            if(name is ".git" or "bin" or "obj" or ".dipqa" or "artifacts" or "node_modules" or ".vs")continue;
            CopyDirectory(dir,Path.Combine(target,name));
        }
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,StringBuilder sink,ConcurrentQueue<(int Second,decimal Fps,decimal? Frame)>? metrics,Stopwatch sw,CancellationToken ct)
    {
        while(!reader.EndOfStream)
        {
            var line=await reader.ReadLineAsync(ct);if(line is null)break;sink.AppendLine(line);
            if(metrics is not null&&TryMetric(line,out var fps,out var frame))
                metrics.Enqueue(((int)sw.Elapsed.TotalSeconds,fps,frame));
        }
    }

    private static bool TryMetric(string line,out decimal fps,out decimal? frame)
    {
        fps=0m;frame=null;
        if(!line.Contains("DIP_METRIC",StringComparison.OrdinalIgnoreCase))return false;
        foreach(var token in line.Split([' ',';'],StringSplitOptions.RemoveEmptyEntries))
        {
            var parts=token.Split('=',2);if(parts.Length!=2)continue;
            if(parts[0].Equals("fps",StringComparison.OrdinalIgnoreCase)&&decimal.TryParse(parts[1],out var f))fps=f;
            if(parts[0].Equals("frame_time_ms",StringComparison.OrdinalIgnoreCase)&&decimal.TryParse(parts[1],out var ft))frame=ft;
        }
        return fps>0m||frame is not null;
    }

    private static string ResolveTargetExecutable(string workspace,string executable)
    {
        if(string.IsNullOrWhiteSpace(executable))throw new ArgumentException("Executable is required.");
        if(Path.IsPathRooted(executable))throw new InvalidOperationException("P2 target executable must be workspace-relative.");
        var resolved=SafePath(workspace,executable);
        if(!File.Exists(resolved))throw new FileNotFoundException("P2 target executable was not found inside the configured workspace.",resolved);
        return resolved;
    }

    private static string SafePath(string root,string relative)
    {
        if(Path.IsPathRooted(relative))throw new InvalidOperationException("Path must be relative.");
        var full=Path.GetFullPath(Path.Combine(root,relative));
        var prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&!full.Equals(Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path escapes the configured workspace.");
        return full;
    }

    private static string ArtifactRoot(Guid projectId,string category)
    {
        var root=Path.GetFullPath(Path.Combine("artifacts","p2",projectId.ToString("N"),category,$"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(root);return root;
    }
    private static string RelativeArtifact(string full)=>Path.GetRelativePath(Path.GetFullPath("."),full).Replace('\\','/');
    private static string Fingerprint(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(text)))).ToLowerInvariant()[..16];
    private static string Normalize(string text)=>string.Join("\n",text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).TakeLast(30));
    private static string SafeName(string value)=>string.Concat(value.Select(c=>char.IsLetterOrDigit(c)||c is '-' or '_'?c:'_')).Substring(0,Math.Min(80,value.Length));
}
