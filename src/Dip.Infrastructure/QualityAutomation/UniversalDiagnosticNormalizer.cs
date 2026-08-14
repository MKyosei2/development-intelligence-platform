using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed partial class UniversalDiagnosticNormalizer
{
    public IReadOnlyList<QaFindingCandidate> NormalizeText(LanguageId language,string tool,string stdout,string stderr,bool timedOut=false)
    {
        if(language==LanguageId.Python)return QaParsing.ParsePython(tool,stdout+"\n"+stderr,timedOut);
        if(language==LanguageId.CSharp)return QaParsing.ParseCSharp(tool,stdout+"\n"+stderr,timedOut);
        if(language==LanguageId.Cpp||language==LanguageId.C)return QaParsing.ParseCpp(tool,stdout+"\n"+stderr,timedOut);
        var text=stdout+"\n"+stderr;var list=new List<QaFindingCandidate>();
        if(timedOut)list.Add(new("hang",Severity.Critical,tool,$"{language} process timed out","The command exceeded its QA timeout.",ExceptionType:"Timeout"));
        foreach(Match m in CommonCompilerRegex().Matches(text))
        {
            var warning=m.Groups[4].Value.Equals("warning",StringComparison.OrdinalIgnoreCase);list.Add(new(warning?"static-analysis":"compile",warning?Severity.Warning:Severity.Critical,tool,$"{language} {m.Groups[4].Value}",m.Groups[5].Value.Trim(),m.Groups[1].Value,Int(m.Groups[2].Value),Int(m.Groups[3].Value),RuleId:tool));
        }
        foreach(Match m in OnLineRegex().Matches(text))list.Add(new("syntax",Severity.Critical,tool,$"{language} syntax error",m.Groups[1].Value.Trim(),m.Groups[2].Value,Int(m.Groups[3].Value),RuleId:tool));
        if(list.Count==0&&text.Contains("SyntaxError",StringComparison.OrdinalIgnoreCase))list.Add(new("syntax",Severity.Critical,tool,$"{language} syntax error",Last(text),ExceptionType:"SyntaxError",StackTrace:Trim(text),RuleId:tool));
        if(list.Count==0&&text.Contains("syntax error",StringComparison.OrdinalIgnoreCase))list.Add(new("syntax",Severity.Critical,tool,$"{language} syntax error",LastContaining(text,"syntax error"),StackTrace:Trim(text),RuleId:tool));
        return Dedup(list);
    }

    public IReadOnlyList<QaFindingCandidate> NormalizeReportFile(string path,LanguageId language,string tool)
    {
        var ext=Path.GetExtension(path).ToLowerInvariant();var name=Path.GetFileName(path).ToLowerInvariant();
        try
        {
            if(ext==".sarif")return ParseSarif(path,language,tool);
            if(ext==".xml"||name.EndsWith(".trx",StringComparison.Ordinal))return ParseJUnitLike(path,language,tool);
            if(ext==".json")return ParseJson(path,language,tool);
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or System.Xml.XmlException){return new[]{new QaFindingCandidate("diagnostic-parser",Severity.Warning,tool,"Diagnostic report could not be normalized",ex.Message,path,RuleId:"dip-normalizer")};}
        return Array.Empty<QaFindingCandidate>();
    }
    private static IReadOnlyList<QaFindingCandidate> ParseSarif(string path,LanguageId language,string tool)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(path));var list=new List<QaFindingCandidate>();
        if(!doc.RootElement.TryGetProperty("runs",out var runs))return list;
        foreach(var run in runs.EnumerateArray())if(run.TryGetProperty("results",out var results))foreach(var r in results.EnumerateArray())
        {
            var level=Str(r,"level")??"warning";var severity=level.Equals("error",StringComparison.OrdinalIgnoreCase)?Severity.Critical:Severity.Warning;var rule=Str(r,"ruleId");var message=r.TryGetProperty("message",out var msg)?Str(msg,"text")??msg.ToString():"SARIF finding";string? file=null;int? line=null;int? col=null;
            if(r.TryGetProperty("locations",out var locs)&&locs.ValueKind==JsonValueKind.Array&&locs.GetArrayLength()>0){var l=locs.EnumerateArray().First();if(l.TryGetProperty("physicalLocation",out var p)){if(p.TryGetProperty("artifactLocation",out var a))file=Str(a,"uri");if(p.TryGetProperty("region",out var reg)){line=Num(reg,"startLine");col=Num(reg,"startColumn");}}}
            list.Add(new("static-analysis",severity,tool,rule is null?"SARIF finding":$"{rule} finding",message,file,line,col,RuleId:rule));
        }
        return Dedup(list);
    }
    private static IReadOnlyList<QaFindingCandidate> ParseJUnitLike(string path,LanguageId language,string tool)
    {
        var doc=XDocument.Load(path);var list=new List<QaFindingCandidate>();
        foreach(var tc in doc.Descendants().Where(x=>x.Name.LocalName is "testcase" or "test-case"))
        {
            var failed=tc.Elements().FirstOrDefault(x=>x.Name.LocalName is "failure" or "error");
            var result=(string?)tc.Attribute("result");var explicitFailure=!string.IsNullOrWhiteSpace(result)&&!result.Equals("Passed",StringComparison.OrdinalIgnoreCase)&&!result.Equals("Success",StringComparison.OrdinalIgnoreCase)&&!result.Equals("Skipped",StringComparison.OrdinalIgnoreCase);
            if(failed is null&&!explicitFailure)continue;
            var name=(string?)tc.Attribute("name")??"test";var message=(string?)failed?.Attribute("message")??failed?.Descendants().FirstOrDefault(x=>x.Name.LocalName=="message")?.Value??failed?.Value.Trim()??$"Test result: {result}";
            list.Add(new("test",Severity.Warning,tool,$"Test failed: {name}",string.IsNullOrWhiteSpace(message)?"Test failure":message,RuleId:tc.Name.LocalName=="test-case"?"nunit":"junit"));
        }
        foreach(var r in doc.Descendants().Where(x=>x.Name.LocalName=="UnitTestResult"&&string.Equals((string?)x.Attribute("outcome"),"Failed",StringComparison.OrdinalIgnoreCase)))
        {
            var name=(string?)r.Attribute("testName")??"test";var output=r.Descendants().FirstOrDefault(x=>x.Name.LocalName=="Message")?.Value??"TRX test failed";list.Add(new("test",Severity.Warning,tool,$"Test failed: {name}",output,RuleId:"trx"));
        }
        return Dedup(list);
    }
    private static IReadOnlyList<QaFindingCandidate> ParseJson(string path,LanguageId language,string tool)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(path));var list=new List<QaFindingCandidate>();Walk(doc.RootElement,list,tool);return Dedup(list);
    }
    private static void Walk(JsonElement e,List<QaFindingCandidate> list,string tool)
    {
        if(e.ValueKind==JsonValueKind.Array){foreach(var child in e.EnumerateArray())Walk(child,list,tool);return;}if(e.ValueKind!=JsonValueKind.Object)return;
        var message=Str(e,"message")??Str(e,"text")??Str(e,"description");var severityText=Str(e,"severity")??Str(e,"level");var file=Str(e,"file")??Str(e,"filePath")??Str(e,"path");var line=Num(e,"line")??Num(e,"lineNumber");var col=Num(e,"column")??Num(e,"columnNumber");var rule=Str(e,"ruleId")??Str(e,"rule")??Str(e,"code");
        if(!string.IsNullOrWhiteSpace(message)&&(severityText is not null||file is not null||rule is not null)){var critical=severityText?.Contains("error",StringComparison.OrdinalIgnoreCase)==true||severityText?.Contains("critical",StringComparison.OrdinalIgnoreCase)==true;list.Add(new("diagnostic",critical?Severity.Critical:Severity.Warning,tool,rule is null?"JSON diagnostic":$"{rule} diagnostic",message!,file,line,col,RuleId:rule));}
        foreach(var p in e.EnumerateObject())if(p.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)Walk(p.Value,list,tool);
    }
    private static string? Str(JsonElement e,string n)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(n,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
    private static int? Num(JsonElement e,string n)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(n,out var v)&&v.TryGetInt32(out var x)?x:null;
    private static int? Int(string s)=>int.TryParse(s,out var n)?n:null;
    private static string Trim(string t)=>t.Length<=30000?t:t[..30000];private static string Last(string t)=>t.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim()??"Command failed.";private static string LastContaining(string t,string q)=>t.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).LastOrDefault(x=>x.Contains(q,StringComparison.OrdinalIgnoreCase))?.Trim()??Last(t);
    private static IReadOnlyList<QaFindingCandidate> Dedup(IEnumerable<QaFindingCandidate> x)=>x.GroupBy(v=>string.Join('|',v.Category,v.FilePath,v.Line,v.RuleId,v.Message),StringComparer.Ordinal).Select(g=>g.First()).Take(500).ToArray();
    [GeneratedRegex(@"^(.+?):(\d+)(?::(\d+))?:\s*(?:\[[^]]+\]\s*)?(error|warning)(?:\s+[^:]+)?:\s*(.+)$",RegexOptions.Multiline|RegexOptions.IgnoreCase)]private static partial Regex CommonCompilerRegex();
    [GeneratedRegex(@"(?i)(?:parse\s+error|syntax\s+error)\s*:\s*(.+?)\s+in\s+(.+?)\s+on\s+line\s+(\d+)")]private static partial Regex OnLineRegex();
}
