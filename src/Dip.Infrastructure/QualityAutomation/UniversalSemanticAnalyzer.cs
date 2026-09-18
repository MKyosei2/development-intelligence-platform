// Purpose: Provides the infrastructure capability represented by U ni ve rs al Se ma nt ic An al yz er.
using System.Text.RegularExpressions;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>
/// Language-neutral structural/data-flow pass. It is intentionally conservative:
/// it reports only evidence-backed local source->sink flows and high-signal constructs.
/// Native analyzers (compiler/Semgrep/CodeQL/etc.) remain authoritative when available.
/// </summary>
public sealed class UniversalSemanticAnalyzer:IUniversalSemanticAnalyzer
{
    private static readonly HashSet<string> Ignored=new(StringComparer.OrdinalIgnoreCase){".git","bin","obj","node_modules","artifacts",".dipqa","Library","Temp","Logs","Build","build","vendor","target"};
    private static readonly IReadOnlyDictionary<string,LanguageId> LanguageByExtension=new Dictionary<string,LanguageId>(StringComparer.OrdinalIgnoreCase)
    {
        [".py"]=LanguageId.Python,[".cs"]=LanguageId.CSharp,[".c"]=LanguageId.C,[".h"]=LanguageId.Cpp,[".cc"]=LanguageId.Cpp,[".cpp"]=LanguageId.Cpp,[".cxx"]=LanguageId.Cpp,[".hpp"]=LanguageId.Cpp,
        [".js"]=LanguageId.JavaScript,[".jsx"]=LanguageId.JavaScript,[".mjs"]=LanguageId.JavaScript,[".cjs"]=LanguageId.JavaScript,[".ts"]=LanguageId.TypeScript,[".tsx"]=LanguageId.TypeScript,
        [".java"]=LanguageId.Java,[".kt"]=LanguageId.Kotlin,[".kts"]=LanguageId.Kotlin,[".go"]=LanguageId.Go,[".rs"]=LanguageId.Rust,[".rb"]=LanguageId.Ruby,[".php"]=LanguageId.Php,
        [".swift"]=LanguageId.Swift,[".m"]=LanguageId.ObjectiveC,[".mm"]=LanguageId.ObjectiveC,[".lua"]=LanguageId.Lua,[".sh"]=LanguageId.Bash,[".ps1"]=LanguageId.PowerShell,[".gd"]=LanguageId.Gdscript,
        [".hlsl"]=LanguageId.Hlsl,[".compute"]=LanguageId.Hlsl,[".glsl"]=LanguageId.Glsl,[".vert"]=LanguageId.Glsl,[".frag"]=LanguageId.Glsl,[".geom"]=LanguageId.Glsl
    };
    private static readonly Regex Assignment=new(@"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:=|:=)\s*(?<rhs>.+)",RegexOptions.Compiled|RegexOptions.CultureInvariant);
    private static readonly Regex DangerousSink=new(@"(?<sink>eval|exec|system|popen|Process\.Start|Runtime\.getRuntime\(\)\.exec|child_process\.(?:exec|execSync)|os\.system|subprocess\.(?:run|Popen|call)|shell_exec|mysqli_query|executeQuery|ExecuteSqlRaw|FromSqlRaw)\s*\((?<arg>.+)",RegexOptions.Compiled|RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    private static readonly Regex Source=new(@"(?<source>Request\.|HttpContext\.|Console\.ReadLine|Environment\.GetEnvironmentVariable|process\.argv|req\.(?:body|query|params)|input\(|sys\.argv|os\.environ|params\[|ARGV|\$_(?:GET|POST|REQUEST)|readLine\(|getenv\(|std::cin|scanf\s*\()",RegexOptions.Compiled|RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    private static readonly Regex SecretLiteral=new("""(?<key>api[_-]?key|secret|password|token)\s*[:=]\s*["'](?<value>[^"']{8,})["']""",RegexOptions.Compiled|RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    private static readonly Regex EmptyCatch=new(@"catch\s*(?:\([^)]*\))?\s*\{\s*\}",RegexOptions.Compiled|RegexOptions.Singleline|RegexOptions.CultureInvariant);
    private static readonly Regex SqlConcat=new("""(?:SELECT|INSERT|UPDATE|DELETE).{0,160}(?:\+|\$\{|format\(|f["'])""",RegexOptions.Compiled|RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);

    public async Task<QaSemanticAnalysisResult> AnalyzeAsync(QaSemanticAnalysisRequest request,CancellationToken ct)
    {
        var findings=new List<QaFindingCandidate>();var evidence=new List<string>();var files=EnumerateFiles(request.Workspace,request.Language).Take(Math.Clamp(request.MaximumFiles,1,5000)).ToArray();
        foreach(var file in files)
        {
            ct.ThrowIfCancellationRequested();string text;try{text=await File.ReadAllTextAsync(file,ct);}catch{continue;}if(text.Length>2_000_000)continue;
            var relative=Path.GetRelativePath(request.Workspace,file).Replace('\\','/');var lines=text.Split('\n');var tree=UniversalSyntaxTree.Parse(text);var syntaxNodes=UniversalSyntaxTree.CountNodes(tree);var tainted=new Dictionary<string,(int Line,string Source)>(StringComparer.Ordinal);
            for(var i=0;i<lines.Length;i++)
            {
                var line=lines[i];var lineNo=i+1;var assignment=Assignment.Match(line);
                if(assignment.Success)
                {
                    var rhs=assignment.Groups["rhs"].Value;var src=Source.Match(rhs);
                    if(src.Success)tainted[assignment.Groups["name"].Value]=(lineNo,src.Groups["source"].Value);
                    else foreach(var pair in tainted.Where(p=>Regex.IsMatch(rhs,$@"\b{Regex.Escape(p.Key)}\b")))tainted[assignment.Groups["name"].Value]=(lineNo,pair.Value.Source);
                }
                var sink=DangerousSink.Match(line);
                if(sink.Success)
                {
                    var arg=sink.Groups["arg"].Value;var direct=Source.Match(arg);var flow=tainted.FirstOrDefault(p=>Regex.IsMatch(arg,$@"\b{Regex.Escape(p.Key)}\b"));
                    if(direct.Success||!string.IsNullOrEmpty(flow.Key))
                    {
                        var origin=direct.Success?direct.Groups["source"].Value:$"{flow.Key} (tainted at line {flow.Value.Line} from {flow.Value.Source})";
                        findings.Add(new("data-flow",Severity.Critical,"dip-semantic","Untrusted data reaches an execution/query sink",$"{origin} reaches {sink.Groups["sink"].Value} without an observed sanitizer in this bounded flow.",relative,lineNo,RuleId:"DIP-DF001",SourceFormat:"dip-structural",SourceFormatVersion:"1.0"));
                    }
                }
                var secret=SecretLiteral.Match(line);
                if(secret.Success&&!LooksPlaceholder(secret.Groups["value"].Value))findings.Add(new("security",Severity.Critical,"dip-semantic","Possible hard-coded secret",$"A literal value is assigned to '{secret.Groups["key"].Value}'. Move credentials to a secret store/environment binding.",relative,lineNo,RuleId:"DIP-SEC001",SourceFormat:"dip-structural",SourceFormatVersion:"1.0"));
                if(SqlConcat.IsMatch(line))findings.Add(new("data-flow",Severity.Warning,"dip-semantic","Dynamic SQL construction detected","SQL text appears to be constructed by concatenation/interpolation. Verify that all external values are parameterized.",relative,lineNo,RuleId:"DIP-DF002",SourceFormat:"dip-structural",SourceFormatVersion:"1.0"));
                if(line.Contains("Thread.Sleep(",StringComparison.Ordinal)&&line.Contains("async",StringComparison.OrdinalIgnoreCase))findings.Add(new("concurrency",Severity.Warning,"dip-semantic","Blocking sleep near async code","Thread.Sleep can block a worker/event thread; verify that a cancellation-aware async delay is intended.",relative,lineNo,RuleId:"DIP-CON001",SourceFormat:"dip-structural",SourceFormatVersion:"1.0"));
                if((line.Contains(".Result",StringComparison.Ordinal)||line.Contains(".Wait()",StringComparison.Ordinal))&&text.Contains("async ",StringComparison.Ordinal))findings.Add(new("concurrency",Severity.Warning,"dip-semantic","Sync-over-async deadlock risk","Blocking on Task.Result/Wait in code that also uses async can deadlock synchronization contexts or exhaust worker threads.",relative,lineNo,RuleId:"DIP-CON002",SourceFormat:"dip-structural",SourceFormatVersion:"1.0"));
                if(Regex.Matches(line,@"\b(lock|synchronized)\b",RegexOptions.IgnoreCase).Count>=2)findings.Add(new("concurrency",Severity.Warning,"dip-semantic","Nested lock acquisition requires deadlock review","Multiple lock/synchronized tokens occur on one statement; verify consistent lock ordering across call paths.",relative,lineNo,RuleId:"DIP-CON003",SourceFormat:"dip-structural",SourceFormatVersion:"1.0"));
            }
            foreach(Match m in EmptyCatch.Matches(text).Cast<Match>().Take(20))
            {
                var lineNo=1+text[..m.Index].Count(c=>c=='\n');findings.Add(new("reliability",Severity.Warning,"dip-semantic","Empty catch block suppresses failure evidence","An exception is swallowed without logging, rethrowing, or an explicit recovery action.",relative,lineNo,RuleId:"DIP-REL001",SourceFormat:"dip-structural",SourceFormatVersion:"1.0"));
            }
            evidence.Add($"{relative}: ast-nodes={syntaxNodes}, lines={lines.Length}, taint-symbols={tainted.Count}");
        }
        var distinct=findings.GroupBy(x=>string.Join('|',x.RuleId,x.FilePath,x.Line,x.Message),StringComparer.Ordinal).Select(x=>x.First()).Take(2000).ToArray();
        return new(distinct,evidence.Take(200).ToArray(),files.Length);
    }

    private static bool LooksPlaceholder(string value)=>value.Contains("change-me",StringComparison.OrdinalIgnoreCase)||value.Contains("replace",StringComparison.OrdinalIgnoreCase)||value.Contains("example",StringComparison.OrdinalIgnoreCase)||value.All(c=>c is '*' or 'x' or 'X');
    private static IEnumerable<string> EnumerateFiles(string root,LanguageId requestedLanguage)
    {
        var allLanguages=requestedLanguage.IsAuto||requestedLanguage.IsMulti||requestedLanguage.IsUnknown;
        var stack=new Stack<string>();stack.Push(Path.GetFullPath(root));
        while(stack.Count>0)
        {
            var dir=stack.Pop();string[] dirs,files;try{dirs=Directory.GetDirectories(dir);files=Directory.GetFiles(dir);}catch{continue;}
            foreach(var file in files)
            {
                if(!LanguageByExtension.TryGetValue(Path.GetExtension(file),out var fileLanguage))continue;
                if(allLanguages||fileLanguage==requestedLanguage)yield return file;
            }
            foreach(var child in dirs)if(!Ignored.Contains(Path.GetFileName(child)))stack.Push(child);
        }
    }
}
