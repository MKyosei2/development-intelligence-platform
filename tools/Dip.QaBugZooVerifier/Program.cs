using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.QualityAutomation;

var options=Options.Parse(args);
var root=Path.GetFullPath(options.Root??FindRepositoryRoot(Environment.CurrentDirectory));
var zooRoot=Path.Combine(root,"samples","qa-bug-zoo");
var outputRoot=Path.GetFullPath(options.Output??Path.Combine(root,"artifacts","qa-bug-zoo"));
Directory.CreateDirectory(outputRoot);
if(!Directory.Exists(zooRoot))throw new DirectoryNotFoundException($"Bug Zoo was not found: {zooRoot}");

var allCases=Directory.EnumerateFiles(zooRoot,"expected-result.json",SearchOption.AllDirectories).Select(path=>BugZooCase.Load(zooRoot,path)).OrderBy(x=>x.Language.Value).ThenBy(x=>x.Name,StringComparer.Ordinal).ToArray();
var skippedCases=allCases.Where(x=>!x.IsSupportedOnCurrentPlatform()).ToArray();
var cases=allCases.Where(x=>x.IsSupportedOnCurrentPlatform()).ToArray();

var executor=new ProcessExecutor();
var normalizer=new DiagnosticNormalizer();
var registry=new QaAdapterRegistry(new IQaAdapter[]
{
    new PythonQaEngine(executor),new CSharpQaEngine(executor),new CppQaEngine(executor),
    new ConventionalQaAdapter(executor,normalizer,LanguageId.JavaScript),new ConventionalQaAdapter(executor,normalizer,LanguageId.TypeScript),
    new ConventionalQaAdapter(executor,normalizer,LanguageId.Go),new ConventionalQaAdapter(executor,normalizer,LanguageId.Rust),
    new ConventionalQaAdapter(executor,normalizer,LanguageId.Java),new ConventionalQaAdapter(executor,normalizer,LanguageId.Kotlin),
    new ConventionalQaAdapter(executor,normalizer,LanguageId.Lua),new ConventionalQaAdapter(executor,normalizer,LanguageId.Hlsl),
    new ConventionalQaAdapter(executor,normalizer,LanguageId.Glsl),new UniversalFallbackQaAdapter(executor,normalizer)
});

var results=new List<CaseResult>();
foreach(var testCase in cases)
{
    var artifactRoot=Path.Combine(outputRoot,"cases",testCase.Language.Value,testCase.Name);RecreateDirectory(artifactRoot);var stopwatch=Stopwatch.StartNew();
    try
    {
        var adapter=registry.Resolve(testCase.Language);var command=BuildCommand(testCase);
        var first=await adapter.RunAsync(command,testCase.Workspace,Path.Combine(artifactRoot,"first"),CancellationToken.None);
        var unavailable=first.Tools.Count==0||first.Tools.All(t=>!t.WasAvailable);
        var relevant=RelevantFindings(first.Findings,testCase).ToArray();var noisy=first.Findings.Where(f=>f.Severity is Severity.Critical or Severity.Warning).ToArray();
        var detected=testCase.ExpectNoFindings?noisy.Length==0:relevant.Length>0;var reproduced=false;var reproducibleAttempts=0;
        if(!unavailable&&testCase.MustReproduce&&relevant.Length>0)
        {
            var firstFingerprints=relevant.Select(f=>QaFingerprint.Create(testCase.Language,f)).ToHashSet(StringComparer.Ordinal);
            for(var attempt=1;attempt<=2;attempt++)
            {
                var repeat=await adapter.RunAsync(command,testCase.Workspace,Path.Combine(artifactRoot,$"repeat-{attempt}"),CancellationToken.None);
                var repeatFingerprints=RelevantFindings(repeat.Findings,testCase).Select(f=>QaFingerprint.Create(testCase.Language,f));if(repeatFingerprints.Any(firstFingerprints.Contains))reproducibleAttempts++;
            }
            reproduced=reproducibleAttempts>0;
        }
        stopwatch.Stop();
        results.Add(new(testCase.Language.Value,testCase.Name,testCase.ExpectNoFindings,testCase.MustReproduce,detected,reproduced,reproducibleAttempts,2,unavailable,stopwatch.ElapsedMilliseconds,noisy.Length,relevant.FirstOrDefault()?.Category,relevant.FirstOrDefault()?.Severity.ToString(),relevant.FirstOrDefault()?.ExceptionType,relevant.FirstOrDefault()?.RuleId,first.Tools.Select(t=>new ToolResult(t.Tool,t.WasAvailable,t.Succeeded,t.ExitCode,t.DurationMs)).ToArray(),unavailable?"No runnable tool was available for this adapter.":null));
    }
    catch(Exception ex)when(ex is not OperationCanceledException)
    {
        stopwatch.Stop();results.Add(new(testCase.Language.Value,testCase.Name,testCase.ExpectNoFindings,testCase.MustReproduce,false,false,0,0,true,stopwatch.ElapsedMilliseconds,0,null,null,null,null,Array.Empty<ToolResult>(),ex.ToString()));
    }
}

var bugCases=results.Where(x=>!x.ExpectNoFindings).ToArray();var cleanCases=results.Where(x=>x.ExpectNoFindings).ToArray();var reproducibleCases=results.Where(x=>x.MustReproduce).ToArray();
var detectedCount=bugCases.Count(x=>x.Detected);var falsePositives=cleanCases.Count(x=>!x.Detected);var reproducedCount=reproducibleCases.Count(x=>x.Reproduced);var infrastructureFailures=results.Count(x=>x.InfrastructureFailure);
var detectionRecall=Percent(detectedCount,bugCases.Length);var falsePositiveRate=Percent(falsePositives,cleanCases.Length);var reproductionRate=Percent(reproducedCount,reproducibleCases.Length);var medianDuration=Median(results.Where(r=>!r.InfrastructureFailure).Select(r=>r.DurationMs).ToArray());
var summary=new{schemaVersion="2.0",generatedAt=DateTimeOffset.UtcNow,repositoryRoot=root,languages=results.Select(x=>x.Language).Distinct().OrderBy(x=>x).ToArray(),cases=results.Count,injectedBugCases=bugCases.Length,detectedBugCases=detectedCount,detectionRecallPercent=detectionRecall,cleanControlCases=cleanCases.Length,falsePositiveCases=falsePositives,falsePositiveRatePercent=falsePositiveRate,reproductionEligibleCases=reproducibleCases.Length,reproducedCases=reproducedCount,reproductionRatePercent=reproductionRate,infrastructureFailures,platformSkippedCases=skippedCases.Select(x=>$"{x.Language.Value}/{x.Name}").ToArray(),medianCaseDurationMs=medianDuration,results};
var jsonOptions=new JsonSerializerOptions{WriteIndented=true};await File.WriteAllTextAsync(Path.Combine(outputRoot,"qa-bug-zoo-results.json"),JsonSerializer.Serialize(summary,jsonOptions));await File.WriteAllTextAsync(Path.Combine(outputRoot,"qa-bug-zoo-results.md"),Markdown(results,bugCases.Length,detectedCount,detectionRecall,cleanCases.Length,falsePositives,falsePositiveRate,reproducibleCases.Length,reproducedCount,reproductionRate,infrastructureFailures,skippedCases,medianDuration));
Console.WriteLine($"Bug Zoo: {summary.languages.Length} languages, detected {detectedCount}/{bugCases.Length} ({detectionRecall:0.##}%), false positives {falsePositives}/{cleanCases.Length} ({falsePositiveRate:0.##}%), reproduced {reproducedCount}/{reproducibleCases.Length} ({reproductionRate:0.##}%).");
Console.WriteLine($"Evidence: {Path.Combine(outputRoot,"qa-bug-zoo-results.json")}");
if(infrastructureFailures>0&&!options.AllowMissingTools)return 2;if(bugCases.Any(x=>!x.Detected&&!x.InfrastructureFailure))return 3;if(cleanCases.Any(x=>!x.Detected&&!x.InfrastructureFailure))return 4;if(reproducibleCases.Any(x=>x.Detected&&!x.Reproduced&&!x.InfrastructureFailure))return 5;return 0;

static QaExecutionCommand BuildCommand(BugZooCase testCase)
{
    string? runtime=null;
    if(testCase.Language==LanguageId.Python&&File.Exists(Path.Combine(testCase.Workspace,"app.py")))runtime="app.py";
    else if(testCase.Language==LanguageId.CSharp&&(testCase.MustReproduce||string.Equals(testCase.Category,"runtime",StringComparison.OrdinalIgnoreCase)||string.Equals(testCase.Category,"hang",StringComparison.OrdinalIgnoreCase)))runtime=CSharpRuntime(testCase.Workspace);
    else if(testCase.Language==LanguageId.JavaScript&&File.Exists(Path.Combine(testCase.Workspace,"app.js"))&&testCase.MustReproduce)runtime="app.js";
    var timeout=string.Equals(testCase.Category,"hang",StringComparison.OrdinalIgnoreCase)?5:120;
    return new(Guid.Empty,$"bug-zoo:{testCase.Language.Value}:{testCase.Name}",testCase.Language,".",null,runtime,Array.Empty<string>(),timeout,false,false,false,false);
}
static string? CSharpRuntime(string workspace){var project=Directory.EnumerateFiles(workspace,"*.csproj",SearchOption.TopDirectoryOnly).FirstOrDefault();if(project is null)return null;var assembly=Path.GetFileNameWithoutExtension(project);return Path.Combine("bin","Release","net8.0",assembly+".dll");}
static IEnumerable<QaFindingCandidate> RelevantFindings(IEnumerable<QaFindingCandidate> findings,BugZooCase testCase){if(testCase.ExpectNoFindings)return Array.Empty<QaFindingCandidate>();return findings.Where(f=>string.Equals(f.Category,testCase.Category,StringComparison.OrdinalIgnoreCase)&&string.Equals(f.Severity.ToString(),testCase.Severity,StringComparison.OrdinalIgnoreCase)&&(string.IsNullOrWhiteSpace(testCase.ExceptionType)||string.Equals(f.ExceptionType,testCase.ExceptionType,StringComparison.OrdinalIgnoreCase))&&(string.IsNullOrWhiteSpace(testCase.RuleId)||string.Equals(f.RuleId,testCase.RuleId,StringComparison.OrdinalIgnoreCase)));}
static decimal Percent(int numerator,int denominator)=>denominator==0?0:decimal.Round(100m*numerator/denominator,2);
static long Median(long[] values){if(values.Length==0)return 0;Array.Sort(values);return values.Length%2==1?values[values.Length/2]:(values[values.Length/2-1]+values[values.Length/2])/2;}
static void RecreateDirectory(string path){if(Directory.Exists(path))Directory.Delete(path,true);Directory.CreateDirectory(path);}
static string FindRepositoryRoot(string start){var current=new DirectoryInfo(Path.GetFullPath(start));while(current!=null){if(Directory.Exists(Path.Combine(current.FullName,"samples","qa-bug-zoo")))return current.FullName;current=current.Parent;}throw new DirectoryNotFoundException("Unable to locate repository root containing samples/qa-bug-zoo.");}
static string Markdown(IReadOnlyList<CaseResult> results,int bugCount,int detected,decimal recall,int cleanCount,int falsePositives,decimal fpRate,int reproCount,int reproduced,decimal reproRate,int infraFailures,IReadOnlyList<BugZooCase> skippedCases,long medianMs)
{
    var sb=new StringBuilder();sb.AppendLine("# DIP QA Bug Zoo Results").AppendLine();sb.AppendLine($"- Languages exercised: **{results.Select(x=>x.Language).Distinct().Count()}**");sb.AppendLine($"- Injected bugs: **{bugCount}**");sb.AppendLine($"- Detected: **{detected}/{bugCount} ({recall:0.##}%)**");sb.AppendLine($"- Clean controls: **{cleanCount}**");sb.AppendLine($"- False positives: **{falsePositives}/{cleanCount} ({fpRate:0.##}%)**");sb.AppendLine($"- Reproduced: **{reproduced}/{reproCount} ({reproRate:0.##}%)**");sb.AppendLine($"- Infrastructure failures: **{infraFailures}**");sb.AppendLine($"- Platform-skipped cases: **{skippedCases.Count}**");if(skippedCases.Count>0)sb.AppendLine($"  - {string.Join(", ",skippedCases.Select(x=>$"{x.Language.Value}/{x.Name}"))}");sb.AppendLine($"- Median case duration: **{medianMs} ms**").AppendLine();sb.AppendLine("| Language | Case | Expected | Detected | Reproduced | Infrastructure | Duration ms |");sb.AppendLine("|---|---|---|---:|---:|---:|---:|");foreach(var result in results)sb.AppendLine($"| {result.Language} | {result.Name} | {(result.ExpectNoFindings?"clean":result.MatchedCategory??"bug")} | {(result.Detected?"yes":"no")} | {(result.MustReproduce?(result.Reproduced?"yes":"no"):"n/a")} | {(result.InfrastructureFailure?"fail":"ok")} | {result.DurationMs} |");return sb.ToString();
}
sealed record ToolResult(string Tool,bool WasAvailable,bool Succeeded,int? ExitCode,long DurationMs);
sealed record CaseResult(string Language,string Name,bool ExpectNoFindings,bool MustReproduce,bool Detected,bool Reproduced,int ReproducedAttempts,int ReproductionAttempts,bool InfrastructureFailure,long DurationMs,int FindingCount,string? MatchedCategory,string? MatchedSeverity,string? MatchedExceptionType,string? MatchedRuleId,IReadOnlyList<ToolResult> Tools,string? Error);
sealed record BugZooCase(LanguageId Language,string Name,string Workspace,bool ExpectNoFindings,string? Category,string? Severity,string? ExceptionType,string? RuleId,bool MustReproduce,bool OptionalOnWindows,bool OptionalOnNonWindows)
{
    public static BugZooCase Load(string zooRoot,string expectedPath)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(expectedPath));var json=document.RootElement;var workspace=Path.GetDirectoryName(expectedPath)!;var relative=Path.GetRelativePath(zooRoot,workspace).Replace('\\','/').Split('/');if(relative.Length<2)throw new InvalidDataException($"Unexpected Bug Zoo path: {workspace}");
        var language=LanguageId.Parse(relative[0]);return new(language,relative[1],workspace,GetBool(json,"expectNoFindings"),GetString(json,"category"),GetString(json,"severity"),GetString(json,"exceptionType"),GetString(json,"ruleId"),GetBool(json,"mustReproduce"),GetBool(json,"optionalOnWindows"),GetBool(json,"optionalOnNonWindows"));
    }
    public bool IsSupportedOnCurrentPlatform()=>!(OperatingSystem.IsWindows()&&OptionalOnWindows)&&!(!OperatingSystem.IsWindows()&&OptionalOnNonWindows);
    static string? GetString(JsonElement json,string name)=>json.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
    static bool GetBool(JsonElement json,string name)=>json.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.True;
}
sealed record Options(string? Root,string? Output,bool AllowMissingTools)
{
    public static Options Parse(string[] args){string? root=null;string? output=null;var allowMissing=false;for(var i=0;i<args.Length;i++){switch(args[i]){case "--root" when i+1<args.Length:root=args[++i];break;case "--output" when i+1<args.Length:output=args[++i];break;case "--allow-missing-tools":allowMissing=true;break;}}return new(root,output,allowMissing);}
}
