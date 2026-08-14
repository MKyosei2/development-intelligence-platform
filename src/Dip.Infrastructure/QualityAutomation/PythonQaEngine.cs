using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class PythonQaEngine(ProcessExecutor executor):IQaAdapter,IPythonHardcodedQaRunner
{
    public LanguageId Language=>LanguageId.Python;
    public int Priority=>100;
    public async Task<HardcodedQaResult> RunAsync(QaExecutionCommand command,string workspace,string artifactRoot,CancellationToken ct)
    {
        var python=ProcessExecutor.FindExecutable("python")??ProcessExecutor.FindExecutable("python3")??throw new FileNotFoundException("Python executable was not found in PATH.");
        var tools=new List<QaToolExecution>();var findings=new List<QaFindingCandidate>();
        var env=new Dictionary<string,string>{{"PYTHONFAULTHANDLER","1"},{"PYTHONASYNCIODEBUG","1"},{"PYTHONHASHSEED","0"}};
        var timeout=TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds,5,1800));
        await Run("python-compileall",python,["-m","compileall","-q","."],Severity.Critical,"syntax");
        await Run("ruff",python,["-m","ruff","check","."],Severity.Warning,"static-analysis");
        await Run("mypy",python,["-m","mypy","."],Severity.Warning,"type-check");
        await RunPytest();
        if(!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var runtime=command.RuntimeExecutable!;IEnumerable<string> args;string executable;
            if(runtime.EndsWith(".py",StringComparison.OrdinalIgnoreCase)){executable=python;args=new[]{runtime}.Concat(command.RuntimeArguments??Array.Empty<string>());}
            else{executable=Path.IsPathRooted(runtime)?runtime:Path.GetFullPath(Path.Combine(workspace,runtime));args=command.RuntimeArguments??Array.Empty<string>();}
            var runtimeResult=await Run("python-runtime",executable,args,Severity.Critical,"runtime");
            if(runtime.EndsWith(".py",StringComparison.OrdinalIgnoreCase)&&runtimeResult.Tool.Succeeded)await RunMemoryProbe(runtime,command.RuntimeArguments??Array.Empty<string>());
        }
        return new(Language,tools,findings,QaToolSupport.JsonSummary(Language,tools,findings));

        async Task<(QaToolExecution Tool,IReadOnlyList<QaFindingCandidate> Findings,ProcessExecutionResult? Process)> Run(string name,string executable,IEnumerable<string> args,Severity fallback,string category)
        {
            var result=await QaToolSupport.ExecuteAsync(executor,name,executable,args,workspace,artifactRoot,env,timeout,Language,fallback,category,ct);tools.Add(result.Tool);findings.AddRange(result.Findings);return result;
        }

        async Task RunPytest()
        {
            var result=await QaToolSupport.ExecuteAsync(executor,"pytest",python,["-m","pytest","-q","--disable-warnings","--maxfail=25"],workspace,artifactRoot,env,timeout,Language,Severity.Warning,"test",ct);
            var noTests=result.Process?.ExitCode==5&&((result.Process.StandardOutput?.Contains("no tests ran",StringComparison.OrdinalIgnoreCase)??false)||(result.Process.StandardError?.Contains("no tests ran",StringComparison.OrdinalIgnoreCase)??false));
            if(noTests)
            {
                tools.Add(result.Tool with{Succeeded=true,Summary="pytest completed with no tests collected; this is neutral evidence, not a test failure."});
                return;
            }
            tools.Add(result.Tool);findings.AddRange(result.Findings);
        }

        async Task RunMemoryProbe(string target,IReadOnlyList<string> runtimeArgs)
        {
            const string probe="import runpy,sys,tracemalloc; target=sys.argv[1]; sys.argv=[target,*sys.argv[2:]]; tracemalloc.start(5); before=tracemalloc.take_snapshot(); state=runpy.run_path(target,run_name='__main__'); after=tracemalloc.take_snapshot(); net=sum(max(0,s.size_diff) for s in after.compare_to(before,'lineno')); current,peak=tracemalloc.get_traced_memory(); print(f'DIP_TRACEMALLOC_NET_BYTES={net}'); print(f'DIP_TRACEMALLOC_PEAK_BYTES={peak}')";
            var args=new List<string>{"-X","tracemalloc=5","-c",probe,target};args.AddRange(runtimeArgs);
            var result=await QaToolSupport.ExecuteAsync(executor,"python-tracemalloc",python,args,workspace,artifactRoot,env,timeout,Language,Severity.Warning,"memory-growth",ct);tools.Add(result.Tool);findings.AddRange(result.Findings);
            if(result.Process is null||!result.Tool.Succeeded)return;var net=Marker(result.Process.StandardOutput,"DIP_TRACEMALLOC_NET_BYTES");
            if(net>=64L*1024*1024)findings.Add(new("memory-growth",net>=256L*1024*1024?Severity.Critical:Severity.Warning,"python-tracemalloc","Python retained a large amount of newly allocated memory",$"Net live allocation growth during the QA scenario was {net/1024/1024} MB.",RuleId:"python-tracemalloc-growth"));
        }
        static long Marker(string text,string name){foreach(var line in text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries))if(line.StartsWith(name+"=",StringComparison.Ordinal)&&long.TryParse(line[(name.Length+1)..],out var n))return n;return 0;}
    }
}
