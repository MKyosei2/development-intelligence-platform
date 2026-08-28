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
        var observationWindowCompleted=false;
        var terminatedByHarness=false;
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
            // Reaching the configured soak window is success for the harness, not a target crash.
            // Record the ownership of termination before Kill so a non-zero OS exit code caused by
            // the harness cannot be misclassified as an application crash.
            observationWindowCompleted=sw.Elapsed>=TimeSpan.FromSeconds(duration);
            if(observationWindowCompleted)
            {
                terminatedByHarness=true;
                try{process.Kill(entireProcessTree:true);}catch{}
            }
        }
        try{await process.WaitForExitAsync(CancellationToken.None);}catch{}
        try{await Task.WhenAll(outTask,errTask);}catch{}
        sw.Stop();
        await File.WriteAllTextAsync(stdoutPath,stdout.ToString(),CancellationToken.None);
        await File.WriteAllTextAsync(stderrPath,stderr.ToString(),CancellationToken.None);
        var exitCode=process.HasExited?process.ExitCode:(int?)null;
        crashed=!terminatedByHarness&&!timedOut&&exitCode is not null&&exitCode.Value!=0;

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

        // A mutation score is meaningless if the unmodified target already fails.
        var baselineDir=Path.Combine(root,"baseline");Directory.CreateDirectory(baselineDir);
        var baseline=await executor.RunAsync(
            executable,command.Target.Arguments??Array.Empty<string>(),temp,null,
            TimeSpan.FromSeconds(Math.Clamp(command.Target.TimeoutSeconds,1,3600)),
            Path.Combine(baselineDir,"stdout.log"),Path.Combine(baselineDir,"stderr.log"),ct);
        var baselinePassed=!baseline.TimedOut&&baseline.ExitCode==0;
        if(!baselinePassed)
        {
            var detail=baseline.TimedOut
                ?"Baseline timed out; mutation score was not calculated."
                :$"Baseline exited with code {(baseline.ExitCode?.ToString()??"n/a")}; mutation score was not calculated.";
            return new("InfrastructureFailure",0,0,0,0,0m,Array.Empty<MutationCaseResult>(),
                RelativeArtifact(root),false,detail);
        }

        var requested=command.Mutations?.ToList()??new List<MutationCaseSpec>();
        if(command.GenerateCommonMutations)
            requested.AddRange(BuildAutomaticMutations(temp,command.AutoSourceFiles,Math.Clamp(command.MaxMutations,1,200)-requested.Count));

        var unique=requested
            .Where(x=>!string.IsNullOrWhiteSpace(x.Id)
                      &&!string.IsNullOrWhiteSpace(x.FileRelativePath)
                      &&x.OriginalText!=x.ReplacementText)
            .GroupBy(x=>x.Id,StringComparer.OrdinalIgnoreCase)
            .Select(x=>x.First())
            .Take(Math.Clamp(command.MaxMutations,1,200))
            .ToArray();

        var cases=new List<MutationCaseResult>();
        var infrastructureFailure=false;

        foreach(var mutation in unique)
        {
            ct.ThrowIfCancellationRequested();
            var file=SafePath(temp,mutation.FileRelativePath);
            if(!File.Exists(file))
            {
                cases.Add(new(mutation.Id,false,false,null,"File not found.",mutation.FileRelativePath,null,null,mutation.Operator));
                continue;
            }

            var original=await File.ReadAllTextAsync(file,ct);
            var offsets=FindAll(original,mutation.OriginalText,3);
            if(offsets.Count==0)
            {
                cases.Add(new(mutation.Id,false,false,null,"OriginalText was not found.",mutation.FileRelativePath,null,null,mutation.Operator));
                continue;
            }
            if(offsets.Count>1)
            {
                cases.Add(new(mutation.Id,false,false,null,
                    "OriginalText is ambiguous because it occurs more than once; specify a more precise mutation.",
                    mutation.FileRelativePath,null,null,mutation.Operator));
                continue;
            }

            var index=offsets[0];
            var (line,column)=LineColumn(original,index);
            var mutated=original[..index]+mutation.ReplacementText+original[(index+mutation.OriginalText.Length)..];
            var dir=Path.Combine(root,"cases",SafeName(mutation.Id));Directory.CreateDirectory(dir);

            try
            {
                await File.WriteAllTextAsync(file,mutated,ct);
                await File.WriteAllTextAsync(Path.Combine(dir,"mutation.patch.txt"),
                    $"File: {mutation.FileRelativePath}{Environment.NewLine}Line: {line}, Column: {column}{Environment.NewLine}"
                    +$"Operator: {mutation.Operator??"explicit"}{Environment.NewLine}"
                    +$"- {mutation.OriginalText}{Environment.NewLine}+ {mutation.ReplacementText}{Environment.NewLine}",ct);

                var r=await executor.RunAsync(
                    executable,command.Target.Arguments??Array.Empty<string>(),temp,null,
                    TimeSpan.FromSeconds(Math.Clamp(command.Target.TimeoutSeconds,1,3600)),
                    Path.Combine(dir,"stdout.log"),Path.Combine(dir,"stderr.log"),ct);

                if(!r.TimedOut&&r.ExitCode is null)
                {
                    infrastructureFailure=true;
                    cases.Add(new(mutation.Id,true,false,null,
                        "Infrastructure failure starting the configured test target.",
                        mutation.FileRelativePath,line,column,mutation.Operator));
                    break;
                }

                var killed=r.TimedOut||(r.ExitCode.HasValue&&r.ExitCode.Value!=0);
                cases.Add(new(mutation.Id,true,killed,r.ExitCode,
                    killed?"Tests detected the mutation.":"Mutation survived the configured tests.",
                    mutation.FileRelativePath,line,column,mutation.Operator));
            }
            finally
            {
                await File.WriteAllTextAsync(file,original,CancellationToken.None);
            }
        }

        var applied=cases.Count(x=>x.Applied);var killedCount=cases.Count(x=>x.Applied&&x.Killed);
        var score=applied==0?0m:decimal.Round((decimal)killedCount/applied*100m,2);
        var status=infrastructureFailure||applied==0?"InfrastructureFailure":score>=80m?"Passed":score>=60m?"Warning":"Failed";
        var summary=$"Baseline passed. {killedCount}/{applied} applied mutation(s) were killed; {Math.Max(0,applied-killedCount)} survived.";
        return new(status,cases.Count,applied,killedCount,Math.Max(0,applied-killedCount),score,cases,
            RelativeArtifact(root),true,summary);
    }

    public async Task<FuzzResult> RunFuzzAsync(FuzzCommand command,CancellationToken ct)
    {
        var workspace=workspaceResolver.Resolve(command.Target.WorkspaceRelativePath);
        var executable=ResolveTargetExecutable(workspace,command.Target.Executable);
        var root=ArtifactRoot(command.Target.ProjectId,"fuzz");
        var corpusDir=Path.Combine(root,"corpus");
        var seedDir=Path.Combine(corpusDir,"seed");
        var interestingDir=Path.Combine(corpusDir,"interesting");
        var crashDir=Path.Combine(corpusDir,"crash");
        var timeoutDir=Path.Combine(corpusDir,"timeout");
        Directory.CreateDirectory(seedDir);Directory.CreateDirectory(interestingDir);
        Directory.CreateDirectory(crashDir);Directory.CreateDirectory(timeoutDir);

        var configuredArguments=command.Target.Arguments??Array.Empty<string>();
        if(string.IsNullOrWhiteSpace(command.ArgumentPlaceholder)
           || !configuredArguments.Any(a=>a.Contains(command.ArgumentPlaceholder,StringComparison.Ordinal)))
        {
            var errorPath=Path.Combine(root,"fuzz-manifest.json");
            var errorManifest=System.Text.Json.JsonSerializer.Serialize(new
            {
                status="InfrastructureFailure",
                reason="At least one configured target argument must contain the fuzz input placeholder.",
                argumentPlaceholder=command.ArgumentPlaceholder
            },new System.Text.Json.JsonSerializerOptions{WriteIndented=true});
            await File.WriteAllTextAsync(errorPath,errorManifest,ct);
            return new("InfrastructureFailure",0,0,0,0,Array.Empty<FuzzCaseResult>(),RelativeArtifact(root),Array.Empty<FuzzFailureArtifact>());
        }

        var random=new Random(command.Seed);
        var results=new List<FuzzCaseResult>();
        var failures=new List<FuzzFailureArtifact>();
        var seenFailureFingerprints=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenOutputFingerprints=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var infrastructureFailure=false;
        var cases=Math.Clamp(command.Cases,1,10000);
        var max=Math.Clamp(command.MaxInputBytes,1,1024*1024);

        for(var i=0;i<cases;i++)
        {
            ct.ThrowIfCancellationRequested();
            var bytes=BuildFuzzInput(i,max,random);
            var transient=Path.Combine(root,"current-input.bin");
            await File.WriteAllBytesAsync(transient,bytes,ct);
            if(i<8) await File.WriteAllBytesAsync(Path.Combine(seedDir,$"seed-{i:D2}.bin"),bytes,ct);

            var args=configuredArguments
                .Select(a=>a.Replace(command.ArgumentPlaceholder,transient,StringComparison.Ordinal)).ToArray();
            var dir=Path.Combine(root,"runs",$"case-{i:D5}");Directory.CreateDirectory(dir);
            var r=await executor.RunAsync(
                executable,args,workspace,
                new Dictionary<string,string>{{"DIP_FUZZ","1"},{"DIP_FUZZ_SEED",command.Seed.ToString()},{"DIP_FUZZ_CASE",i.ToString()}},
                TimeSpan.FromSeconds(Math.Clamp(command.Target.TimeoutSeconds,1,600)),
                Path.Combine(dir,"stdout.log"),Path.Combine(dir,"stderr.log"),ct);

            if(!r.TimedOut&&r.ExitCode is null)
            {
                infrastructureFailure=true;
                results.Add(new(i,bytes.Length,null,false,false,Fingerprint(r.StandardError),null));
                break;
            }

            var crashed=!r.TimedOut&&r.ExitCode.HasValue&&r.ExitCode.Value!=0;
            var fingerprint=Fingerprint(r.StandardOutput+"\n"+r.StandardError);
            var failure=crashed||r.TimedOut;
            string? inputReference=null;

            if(failure)
            {
                var kind=crashed?"crash":"timeout";
                var destinationDir=crashed?crashDir:timeoutDir;
                var destination=Path.Combine(destinationDir,$"{fingerprint}-{i:D5}.bin");
                await File.WriteAllBytesAsync(destination,bytes,ct);
                inputReference=RelativeArtifact(destination);

                if(seenFailureFingerprints.Add(fingerprint))
                {
                    string? minimizedReference=null;int? minimizedBytes=null;
                    if(command.MinimizeFailures&&bytes.Length>1)
                    {
                        var minimized=await MinimizeFailureAsync(
                            executable,workspace,command,bytes,fingerprint,crashed,ct);
                        if(minimized.Length<bytes.Length)
                        {
                            var minPath=Path.Combine(destinationDir,$"{fingerprint}-{i:D5}.min.bin");
                            await File.WriteAllBytesAsync(minPath,minimized,ct);
                            minimizedReference=RelativeArtifact(minPath);minimizedBytes=minimized.Length;
                        }
                    }
                    failures.Add(new(i,kind,fingerprint,inputReference,minimizedReference,bytes.Length,minimizedBytes));
                }
            }
            else if(seenOutputFingerprints.Add(fingerprint)&&seenOutputFingerprints.Count<=128)
            {
                var interesting=Path.Combine(interestingDir,$"{fingerprint}-{i:D5}.bin");
                await File.WriteAllBytesAsync(interesting,bytes,ct);
                inputReference=RelativeArtifact(interesting);
            }

            results.Add(new(i,bytes.Length,r.ExitCode,r.TimedOut,crashed,fingerprint,inputReference));
        }

        try{File.Delete(Path.Combine(root,"current-input.bin"));}catch{}
        var crashes=results.Count(x=>x.Crashed);var timeouts=results.Count(x=>x.TimedOut);
        var unique=results.Where(x=>x.Crashed||x.TimedOut).Select(x=>x.Fingerprint).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var status=infrastructureFailure?"InfrastructureFailure":crashes>0?"Failed":timeouts>0?"Warning":"Passed";

        var manifest=System.Text.Json.JsonSerializer.Serialize(new
        {
            command.Seed,casesRequested=cases,casesExecuted=results.Count,maxInputBytes=max,
            crashes,timeouts,uniqueFailureFingerprints=unique,
            seedCorpus=Directory.EnumerateFiles(seedDir).Select(RelativeArtifact).ToArray(),
            failures
        },new System.Text.Json.JsonSerializerOptions{WriteIndented=true});
        await File.WriteAllTextAsync(Path.Combine(root,"fuzz-manifest.json"),manifest,ct);

        return new(status,results.Count,crashes,timeouts,unique,results,RelativeArtifact(root),failures);
    }

    private async Task<byte[]> MinimizeFailureAsync(
        string executable,string workspace,FuzzCommand command,byte[] original,string expectedFingerprint,bool expectCrash,CancellationToken ct)
    {
        var current=original.ToArray();
        var maxRuns=Math.Clamp(command.MaxMinimizationRunsPerFailure,1,100);
        var runs=0;
        for(var chunk=Math.Max(1,current.Length/2);chunk>=1&&runs<maxRuns;chunk=Math.Max(0,chunk/2))
        {
            var changed=true;
            while(changed&&runs<maxRuns)
            {
                changed=false;
                for(var offset=0;offset<current.Length&&runs<maxRuns;offset+=Math.Max(1,chunk))
                {
                    var length=Math.Min(chunk,current.Length-offset);
                    if(length<=0||current.Length-length<=0)continue;
                    var candidate=current[..offset].Concat(current[(offset+length)..]).ToArray();
                    var input=Path.Combine(Path.GetTempPath(),$"dip-fuzz-min-{Guid.NewGuid():N}.bin");
                    try
                    {
                        await File.WriteAllBytesAsync(input,candidate,ct);
                        var args=(command.Target.Arguments??Array.Empty<string>())
                            .Select(a=>a.Replace(command.ArgumentPlaceholder,input,StringComparison.Ordinal)).ToArray();
                        var minStdout=Path.Combine(Path.GetTempPath(),$"dip-fuzz-min-{Guid.NewGuid():N}.stdout.log");
                        var minStderr=Path.Combine(Path.GetTempPath(),$"dip-fuzz-min-{Guid.NewGuid():N}.stderr.log");
                        try
                        {
                            var r=await executor.RunAsync(executable,args,workspace,
                                new Dictionary<string,string>{{"DIP_FUZZ","1"},{"DIP_FUZZ_MINIMIZE","1"}},
                                TimeSpan.FromSeconds(Math.Clamp(command.Target.TimeoutSeconds,1,600)),
                                minStdout,minStderr,ct);
                            runs++;
                            var sameKind=expectCrash?(!r.TimedOut&&r.ExitCode is not null&&r.ExitCode!=0):r.TimedOut;
                            var sameFingerprint=Fingerprint(r.StandardOutput+"\n"+r.StandardError)==expectedFingerprint;
                            if(sameKind&&sameFingerprint){current=candidate;changed=true;break;}
                        }
                        finally
                        {
                            try{File.Delete(minStdout);}catch{}
                            try{File.Delete(minStderr);}catch{}
                        }
                    }
                    finally{try{File.Delete(input);}catch{}}
                }
            }
            if(chunk==1)break;
        }
        return current;
    }

    private static byte[] BuildFuzzInput(int index,int max,Random random)
    {
        var boundary=new[]{0,1,2,7,8,15,16,31,32,63,64,127,128,255,256,511,512,1023,1024,4095,4096};
        var size=index<boundary.Length?Math.Min(boundary[index],max):random.Next(0,max+1);
        var bytes=new byte[size];
        switch(index%8)
        {
            case 0: break;
            case 1: Array.Fill(bytes,(byte)0x00);break;
            case 2: Array.Fill(bytes,(byte)0xFF);break;
            case 3: Array.Fill(bytes,(byte)'A');break;
            case 4:
                for(var i=0;i<bytes.Length;i++)bytes[i]=(byte)(i&0xFF);
                break;
            default: random.NextBytes(bytes);break;
        }
        return bytes;
    }

    private static List<MutationCaseSpec> BuildAutomaticMutations(
        string workspace,IReadOnlyList<string>? sourceFiles,int remaining)
    {
        var result=new List<MutationCaseSpec>();
        if(remaining<=0||sourceFiles is null)return result;
        var ops=new(string Old,string New,string Name)[]
        {
            (">=",">","boundary-gte-to-gt"),("<=", "<","boundary-lte-to-lt"),
            ("==","!=","equality-invert"),("!=","==","inequality-invert"),
            ("true","false","bool-true-to-false"),("false","true","bool-false-to-true"),
            (" + "," - ","arithmetic-plus-to-minus"),(" - "," + ","arithmetic-minus-to-plus")
        };
        foreach(var relative in sourceFiles.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if(result.Count>=remaining)break;
            var file=SafePath(workspace,relative);
            if(!File.Exists(file))continue;
            var text=File.ReadAllText(file);
            foreach(var op in ops)
            {
                var search=0;
                while(result.Count<remaining)
                {
                    var index=text.IndexOf(op.Old,search,StringComparison.Ordinal);
                    if(index<0)break;
                    var contextStart=Math.Max(0,index-24);
                    var contextLength=Math.Min(text.Length-contextStart,op.Old.Length+48);
                    var context=text.Substring(contextStart,contextLength);
                    // Use the local context as OriginalText when possible so the mutation is unambiguous.
                    if(FindAll(text,context,2).Count==1)
                    {
                        var local=context.IndexOf(op.Old,StringComparison.Ordinal);
                        var replacement=context[..local]+op.New+context[(local+op.Old.Length)..];
                        result.Add(new($"auto-{op.Name}-{result.Count+1:D3}",relative,context,replacement,op.Name));
                    }
                    search=index+op.Old.Length;
                }
            }
        }
        return result;
    }

    private static List<int> FindAll(string text,string value,int limit)
    {
        var result=new List<int>();
        if(string.IsNullOrEmpty(value))return result;
        var start=0;
        while(start<=text.Length-value.Length&&result.Count<limit)
        {
            var index=text.IndexOf(value,start,StringComparison.Ordinal);
            if(index<0)break;
            result.Add(index);start=index+Math.Max(1,value.Length);
        }
        return result;
    }

    private static (int Line,int Column) LineColumn(string text,int offset)
    {
        var line=1;var column=1;
        for(var i=0;i<Math.Min(offset,text.Length);i++)
        {
            if(text[i]=='\n'){line++;column=1;}else column++;
        }
        return(line,column);
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
