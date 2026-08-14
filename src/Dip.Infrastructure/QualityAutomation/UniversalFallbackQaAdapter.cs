using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>
/// Last-resort adapter for an arbitrary LanguageId. It intentionally avoids pretending to
/// understand a language grammar. Instead it executes the repository's own build/test entry
/// point when one can be identified and normalizes any SARIF/JSON/JUnit diagnostics emitted
/// under .dipqa/diagnostics. Language-specific adapters always take precedence.
/// </summary>
public sealed class UniversalFallbackQaAdapter(ProcessExecutor executor,IDiagnosticNormalizer normalizer):IQaAdapter
{
    public LanguageId Language=>new("universal");
    public int Priority=>-1000;
    public bool CanHandle(WorkspaceTechnologyProfile profile)=>true;

    public async Task<HardcodedQaResult> RunAsync(QaExecutionCommand command,string workspace,string artifactRoot,CancellationToken ct)
    {
        var actual=command.Language??Language;
        var tools=new List<QaToolExecution>();var findings=new List<QaFindingCandidate>();
        var timeout=TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds,5,1800));
        var step=ResolveRepositoryStep(workspace);
        if(step is not null)
        {
            var executable=ResolveExecutable(workspace,step.Value.Executable);
            if(executable is null) tools.Add(new(step.Value.Name,false,false,null,0,string.Empty,string.Empty,$"{step.Value.Executable} is unavailable."));
            else
            {
                var result=await QaToolSupport.ExecuteAsync(executor,step.Value.Name,executable,step.Value.Args,workspace,artifactRoot,null,timeout,actual,Severity.Critical,"build",ct);
                tools.Add(result.Tool);findings.AddRange(result.Findings);
            }
        }

        var diagnosticRoot=Path.Combine(workspace,".dipqa","diagnostics");
        if(Directory.Exists(diagnosticRoot))
        {
            foreach(var path in Directory.EnumerateFiles(diagnosticRoot,"*.*",SearchOption.AllDirectories).Take(500))
            {
                ct.ThrowIfCancellationRequested();
                var ext=Path.GetExtension(path).ToLowerInvariant();
                var format=ext switch{".sarif"=>"sarif",".xml"=>"junit",".json"=>"json",_=>null};
                if(format is null)continue;
                var doc=new DiagnosticDocument(Path.GetFileNameWithoutExtension(path),format,await File.ReadAllTextAsync(path,ct),path);
                findings.AddRange(normalizer.Normalize(actual,doc));
            }
        }

        if(!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var runtime=Path.GetFullPath(Path.Combine(workspace,command.RuntimeExecutable!));
            var prefix=workspace.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!runtime.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Runtime executable escapes the QA workspace.");
            if(File.Exists(runtime))
            {
                var result=await QaToolSupport.ExecuteAsync(executor,$"{actual.Value}-runtime",runtime,command.RuntimeArguments??Array.Empty<string>(),workspace,artifactRoot,null,timeout,actual,Severity.Critical,"runtime",ct);
                tools.Add(result.Tool);findings.AddRange(result.Findings);
            }
        }

        if(tools.Count==0&&findings.Count==0)
            findings.Add(new("qa-configuration",Severity.Warning,"dip-universal","No executable QA entry point was detected","Add a language adapter, a recognized repository build file, RuntimeExecutable, or structured diagnostics under .dipqa/diagnostics.",RuleId:"dip-universal-no-entrypoint"));
        var dedup=findings.GroupBy(x=>string.Join('|',x.Category,x.Tool,x.RuleId,x.FilePath,x.Line,x.Message),StringComparer.Ordinal).Select(x=>x.First()).Take(1000).ToArray();
        return new(actual,tools,dedup,QaToolSupport.JsonSummary(actual,tools,dedup));
    }

    private static (string Name,string Executable,IReadOnlyList<string> Args)? ResolveRepositoryStep(string workspace)
    {
        if(File.Exists(Path.Combine(workspace,"Makefile")))return("make","make",["-k"]);
        if(File.Exists(Path.Combine(workspace,"package.json")))return("npm-test","npm",["test"]);
        if(File.Exists(Path.Combine(workspace,"pom.xml")))return("maven-test","mvn",["-B","test"]);
        if(File.Exists(Path.Combine(workspace,"build.gradle"))||File.Exists(Path.Combine(workspace,"build.gradle.kts")))return("gradle-test",OperatingSystem.IsWindows()?"gradlew.bat":"./gradlew",["test","--console=plain"]);
        if(File.Exists(Path.Combine(workspace,"Cargo.toml")))return("cargo-test","cargo",["test","--all-targets"]);
        if(File.Exists(Path.Combine(workspace,"go.mod")))return("go-test","go",["test","./..."]);
        return null;
    }

    private static string? ResolveExecutable(string workspace,string value)
    {
        var relative=value.StartsWith("./",StringComparison.Ordinal)?value[2..]:value;
        if(value.StartsWith(".",StringComparison.Ordinal)||relative.Contains(Path.DirectorySeparatorChar)||relative.Contains(Path.AltDirectorySeparatorChar))
        {
            var full=Path.GetFullPath(Path.Combine(workspace,relative));if(File.Exists(full))return full;
        }
        return ProcessExecutor.FindExecutable(value);
    }
}
