using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>
/// Last-resort adapter for an arbitrary LanguageId. Unknown languages can define direct
/// compiler/test/linter/fuzzer invocations in .dipqa/qa.json without recompiling DIP.
/// Structured SARIF/JSON/JUnit output is normalized into the same Finding model used by
/// built-in language adapters. Shell evaluation is intentionally not used.
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
        var defaultTimeout=TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds,5,1800));
        var manifest=QaManifest.TryLoad(workspace);
        var manifestApplies=manifest is not null&&(string.IsNullOrWhiteSpace(manifest.Language)||LanguageId.Parse(manifest.Language)==actual);

        if(manifestApplies)
        {
            foreach(var step in manifest!.Steps)
            {
                ct.ThrowIfCancellationRequested();
                if(string.IsNullOrWhiteSpace(step.Name)||string.IsNullOrWhiteSpace(step.Executable))
                    throw new InvalidDataException("Every .dipqa/qa.json step requires name and executable.");
                var executable=ResolveExecutable(workspace,step.Executable);
                if(executable is null)
                {
                    tools.Add(new(step.Name,false,false,null,0,string.Empty,string.Empty,$"{step.Executable} is unavailable for manifest step '{step.Name}'."));
                    continue;
                }
                var timeout=TimeSpan.FromSeconds(Math.Clamp(step.TimeoutSeconds??command.TimeoutSeconds,5,1800));
                var result=await QaToolSupport.ExecuteAsync(executor,step.Name,executable,step.Arguments??Array.Empty<string>(),workspace,artifactRoot,step.Environment,timeout,actual,step.ParsedSeverity,step.Category,ct);
                tools.Add(result.Tool);findings.AddRange(result.Findings);
                if(!string.IsNullOrWhiteSpace(step.DiagnosticFormat)&&!string.IsNullOrWhiteSpace(step.DiagnosticPath))
                {
                    var path=ResolveWorkspacePath(workspace,step.DiagnosticPath!);
                    if(File.Exists(path))
                    {
                        var doc=new DiagnosticDocument(step.Name,step.DiagnosticFormat!,await File.ReadAllTextAsync(path,ct),path,step.ParsedSeverity,step.Category);
                        findings.AddRange(normalizer.Normalize(actual,doc));
                    }
                }
            }
        }
        else
        {
            var step=ResolveRepositoryStep(workspace);
            if(step is not null)
            {
                var executable=ResolveExecutable(workspace,step.Value.Executable);
                if(executable is null) tools.Add(new(step.Value.Name,false,false,null,0,string.Empty,string.Empty,$"{step.Value.Executable} is unavailable."));
                else
                {
                    var result=await QaToolSupport.ExecuteAsync(executor,step.Value.Name,executable,step.Value.Args,workspace,artifactRoot,null,defaultTimeout,actual,Severity.Critical,"build",ct);
                    tools.Add(result.Tool);findings.AddRange(result.Findings);
                }
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
            var runtime=ResolveWorkspacePath(workspace,command.RuntimeExecutable!);
            if(File.Exists(runtime))
            {
                var result=await QaToolSupport.ExecuteAsync(executor,$"{actual.Value}-runtime",runtime,command.RuntimeArguments??Array.Empty<string>(),workspace,artifactRoot,null,defaultTimeout,actual,Severity.Critical,"runtime",ct);
                tools.Add(result.Tool);findings.AddRange(result.Findings);
            }
        }

        if(tools.Count==0&&findings.Count==0)
            findings.Add(new("qa-configuration",Severity.Warning,"dip-universal","No executable QA entry point was detected","Add .dipqa/qa.json, a language adapter, a recognized repository build file, RuntimeExecutable, or structured diagnostics under .dipqa/diagnostics.",RuleId:"dip-universal-no-entrypoint"));
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
        if(Path.IsPathRooted(value))throw new InvalidOperationException("QA manifest executable must be workspace-relative or discoverable from PATH.");
        var relative=value.StartsWith("./",StringComparison.Ordinal)?value[2..]:value;
        if(value.StartsWith(".",StringComparison.Ordinal)||relative.Contains(Path.DirectorySeparatorChar)||relative.Contains(Path.AltDirectorySeparatorChar))
        {
            var full=ResolveWorkspacePath(workspace,relative);if(File.Exists(full))return full;
        }
        return ProcessExecutor.FindExecutable(value);
    }

    private static string ResolveWorkspacePath(string workspace,string relative)
    {
        if(Path.IsPathRooted(relative))throw new InvalidOperationException("QA path must be workspace-relative.");
        var root=Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        var candidate=Path.GetFullPath(Path.Combine(root,relative));
        var prefix=root+Path.DirectorySeparatorChar;
        if(!candidate.Equals(root,StringComparison.OrdinalIgnoreCase)&&!candidate.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("QA path escapes the configured workspace.");
        return candidate;
    }
}
