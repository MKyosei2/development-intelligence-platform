using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class ConventionalQaAdapter(ProcessExecutor executor,IDiagnosticNormalizer normalizer,LanguageId language):IQaAdapter
{
    public LanguageId Language { get; }=language;
    public int Priority => 50;

    public async Task<HardcodedQaResult> RunAsync(QaExecutionCommand command,string workspace,string artifactRoot,CancellationToken ct)
    {
        var tools=new List<QaToolExecution>();
        var findings=new List<QaFindingCandidate>();
        var timeout=TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds,5,1800));
        foreach(var step in BuildSteps(workspace))
        {
            ct.ThrowIfCancellationRequested();
            if(!step.ShouldRun(workspace)) continue;
            var executable=ResolveExecutable(workspace,step.Executables);
            if(executable is null)
            {
                tools.Add(new(step.Name,false,false,null,0,string.Empty,string.Empty,$"{step.Name} is unavailable; install one of: {string.Join(", ",step.Executables)}."));
                continue;
            }
            var result=await QaToolSupport.ExecuteAsync(executor,step.Name,executable,step.Arguments(workspace),workspace,artifactRoot,step.Environment,timeout,Language,step.FallbackSeverity,step.Category,ct);
            tools.Add(result.Tool);findings.AddRange(result.Findings);
            if(step.StructuredFormat is not null&&step.StructuredRelativePath is not null)
            {
                var output=Path.Combine(workspace,step.StructuredRelativePath);
                if(File.Exists(output))
                {
                    var document=new DiagnosticDocument(step.Name,step.StructuredFormat,await File.ReadAllTextAsync(output,ct),output,step.FallbackSeverity,step.Category);
                    findings.AddRange(normalizer.Normalize(Language,document));
                }
            }
        }

        if(!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var runtime=Path.IsPathRooted(command.RuntimeExecutable!)?command.RuntimeExecutable!:Path.GetFullPath(Path.Combine(workspace,command.RuntimeExecutable!));
            if(!runtime.StartsWith(workspace.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Runtime executable escapes QA workspace.");
            if(File.Exists(runtime))
            {
                var result=await QaToolSupport.ExecuteAsync(executor,$"{Language.Value}-runtime",runtime,command.RuntimeArguments??Array.Empty<string>(),workspace,artifactRoot,null,timeout,Language,Severity.Critical,"runtime",ct);
                tools.Add(result.Tool);findings.AddRange(result.Findings);
            }
        }

        var dedup=findings.GroupBy(x=>string.Join('|',x.Category,x.Tool,x.RuleId,x.FilePath,x.Line,x.Message),StringComparer.Ordinal).Select(x=>x.First()).Take(1000).ToArray();
        return new(Language,tools,dedup,QaToolSupport.JsonSummary(Language,tools,dedup));
    }

    private IReadOnlyList<Step> BuildSteps(string workspace)
    {
        if(Language==LanguageId.JavaScript)
            return [
                new("eslint",[LocalTool("eslint"),"eslint"],_=>[".","--format","json","--output-file",Path.Combine(".dipqa","eslint.json")],Severity.Warning,"static-analysis",HasPackageJson,"json",Path.Combine(".dipqa","eslint.json")),
                new("npm-test",["npm"],_=>["test","--","--runInBand"],Severity.Warning,"test",HasRealNpmTest)
            ];
        if(Language==LanguageId.TypeScript)
            return [
                new("tsc",[LocalTool("tsc"),"tsc"],_=>["--noEmit","--pretty","false"],Severity.Critical,"compile",w=>File.Exists(Path.Combine(w,"tsconfig.json"))),
                new("eslint",[LocalTool("eslint"),"eslint"],_=>[".","--format","json","--output-file",Path.Combine(".dipqa","eslint.json")],Severity.Warning,"static-analysis",HasPackageJson,"json",Path.Combine(".dipqa","eslint.json")),
                new("npm-test",["npm"],_=>["test","--","--runInBand"],Severity.Warning,"test",HasRealNpmTest)
            ];
        if(Language==LanguageId.Go)
            return [
                new("go-vet",["go"],_=>["vet","./..."],Severity.Warning,"static-analysis",w=>File.Exists(Path.Combine(w,"go.mod"))),
                new("go-test",["go"],_=>["test","./..."],Severity.Warning,"test",w=>File.Exists(Path.Combine(w,"go.mod"))),
                new("go-race",["go"],_=>["test","-race","./..."],Severity.Critical,"data-race",w=>File.Exists(Path.Combine(w,"go.mod")))
            ];
        if(Language==LanguageId.Rust)
            return [
                new("cargo-check",["cargo"],_=>["check","--all-targets","--message-format","short"],Severity.Critical,"compile",w=>File.Exists(Path.Combine(w,"Cargo.toml"))),
                new("cargo-test",["cargo"],_=>["test","--all-targets"],Severity.Warning,"test",w=>File.Exists(Path.Combine(w,"Cargo.toml"))),
                new("cargo-clippy",["cargo"],_=>["clippy","--all-targets"],Severity.Warning,"static-analysis",w=>File.Exists(Path.Combine(w,"Cargo.toml")))
            ];
        if(Language==LanguageId.Java||Language==LanguageId.Kotlin)
        {
            if(File.Exists(Path.Combine(workspace,OperatingSystem.IsWindows()?"gradlew.bat":"gradlew"))||File.Exists(Path.Combine(workspace,"build.gradle"))||File.Exists(Path.Combine(workspace,"build.gradle.kts")))
                return [new("gradle-test",[OperatingSystem.IsWindows()?"gradlew.bat":"./gradlew","gradle"],_=>["test","--console=plain"],Severity.Warning,"test",_=>true)];
            if(File.Exists(Path.Combine(workspace,OperatingSystem.IsWindows()?"mvnw.cmd":"mvnw"))||File.Exists(Path.Combine(workspace,"pom.xml")))
                return [new("maven-test",[OperatingSystem.IsWindows()?"mvnw.cmd":"./mvnw","mvn"],_=>["-B","test"],Severity.Warning,"test",_=>true)];
        }
        if(Language==LanguageId.Lua)
            return [new("luac",["luac"],w=>EnumerateSourceFiles(w,".lua").SelectMany(f=>new[]{"-p",f}).ToArray(),Severity.Critical,"syntax",w=>EnumerateSourceFiles(w,".lua").Any())];
        if(Language==LanguageId.Hlsl)
            return [new("dxc-help",["dxc"],_=>["-help"],Severity.Info,"tooling",w=>EnumerateSourceFiles(w,".hlsl",".hlsli").Any())];
        if(Language==LanguageId.Glsl)
            return [new("glslang-version",["glslangValidator"],_=>["--version"],Severity.Info,"tooling",w=>EnumerateSourceFiles(w,".glsl",".vert",".frag",".comp").Any())];
        return Array.Empty<Step>();
    }

    private static string LocalTool(string name)=>Path.Combine("node_modules",".bin",OperatingSystem.IsWindows()?name+".cmd":name);
    private static bool HasPackageJson(string workspace)=>File.Exists(Path.Combine(workspace,"package.json"));
    private static bool HasRealNpmTest(string workspace)
    {
        var path=Path.Combine(workspace,"package.json");if(!File.Exists(path))return false;
        try
        {
            var text=File.ReadAllText(path);
            return text.Contains("\"test\"",StringComparison.OrdinalIgnoreCase)&&!text.Contains("Error: no test specified",StringComparison.OrdinalIgnoreCase);
        }
        catch{return false;}
    }

    private static string? ResolveExecutable(string workspace,IReadOnlyList<string> candidates)
    {
        foreach(var candidate in candidates)
        {
            var relative=candidate.StartsWith("./",StringComparison.Ordinal)?candidate[2..]:candidate;
            if(relative.Contains(Path.DirectorySeparatorChar)||relative.Contains(Path.AltDirectorySeparatorChar)||candidate.StartsWith(".",StringComparison.Ordinal))
            {
                var full=Path.GetFullPath(Path.Combine(workspace,relative));if(File.Exists(full))return full;
            }
            var path=ProcessExecutor.FindExecutable(candidate);if(path is not null)return path;
        }
        return null;
    }

    private static IEnumerable<string> EnumerateSourceFiles(string workspace,params string[] extensions)
    {
        var ignored=new[]{Path.DirectorySeparatorChar+"node_modules"+Path.DirectorySeparatorChar,Path.DirectorySeparatorChar+".git"+Path.DirectorySeparatorChar,Path.DirectorySeparatorChar+"target"+Path.DirectorySeparatorChar};
        return Directory.EnumerateFiles(workspace,"*.*",SearchOption.AllDirectories)
            .Where(f=>extensions.Any(ext=>f.EndsWith(ext,StringComparison.OrdinalIgnoreCase)))
            .Where(f=>!ignored.Any(i=>f.Contains(i,StringComparison.OrdinalIgnoreCase))).Take(500);
    }

    private sealed record Step(
        string Name,
        IReadOnlyList<string> Executables,
        Func<string,IReadOnlyList<string>> Arguments,
        Severity FallbackSeverity,
        string Category,
        Func<string,bool> ShouldRun,
        string? StructuredFormat=null,
        string? StructuredRelativePath=null,
        IReadOnlyDictionary<string,string>? Environment=null);
}
