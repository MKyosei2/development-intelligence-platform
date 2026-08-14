using System.Text.Json;
using System.Xml.Linq;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class DiagnosticNormalizer : IDiagnosticNormalizer
{
    public IReadOnlyList<QaFindingCandidate> Normalize(LanguageId language,DiagnosticDocument document)
    {
        if(string.IsNullOrWhiteSpace(document.Content)) return Array.Empty<QaFindingCandidate>();
        try
        {
            return document.Format.Trim().ToLowerInvariant() switch
            {
                "sarif" or "sarif-json" => ParseSarif(document),
                "junit" or "junit-xml" or "xml" => ParseJUnit(document),
                "json" => ParseGenericJson(document),
                _ => Array.Empty<QaFindingCandidate>()
            };
        }
        catch(Exception ex) when(ex is JsonException or System.Xml.XmlException or InvalidOperationException)
        {
            return [new QaFindingCandidate("diagnostic-parser",Severity.Warning,document.Tool,"Diagnostic document could not be normalized",ex.Message,document.SourcePath,RuleId:"dip-normalizer")];
        }
    }

    private static IReadOnlyList<QaFindingCandidate> ParseSarif(DiagnosticDocument document)
    {
        using var json=JsonDocument.Parse(document.Content);
        var output=new List<QaFindingCandidate>();
        if(!json.RootElement.TryGetProperty("runs",out var runs)||runs.ValueKind!=JsonValueKind.Array) return output;
        foreach(var run in runs.EnumerateArray())
        {
            var tool=document.Tool;
            if(run.TryGetProperty("tool",out var toolNode)&&toolNode.TryGetProperty("driver",out var driver)&&driver.TryGetProperty("name",out var name)&&name.ValueKind==JsonValueKind.String)
                tool=name.GetString()??tool;
            if(!run.TryGetProperty("results",out var results)||results.ValueKind!=JsonValueKind.Array) continue;
            foreach(var result in results.EnumerateArray())
            {
                var rule=String(result,"ruleId");
                var level=String(result,"level");
                var message=NestedString(result,"message","text")??NestedString(result,"message","markdown")??"Static analysis finding";
                string? file=null; int? line=null; int? column=null;
                if(result.TryGetProperty("locations",out var locations)&&locations.ValueKind==JsonValueKind.Array)
                {
                    var first=locations.EnumerateArray().FirstOrDefault();
                    if(first.ValueKind!=JsonValueKind.Undefined&&first.TryGetProperty("physicalLocation",out var physical))
                    {
                        file=NestedString(physical,"artifactLocation","uri");
                        if(physical.TryGetProperty("region",out var region))
                        {
                            line=Int(region,"startLine"); column=Int(region,"startColumn");
                        }
                    }
                }
                output.Add(new QaFindingCandidate("static-analysis",SarifSeverity(level,document.FallbackSeverity),tool,rule is null?"Static analysis finding":$"{tool} {rule}",message,file,line,column,RuleId:rule));
            }
        }
        return Deduplicate(output);
    }

    private static IReadOnlyList<QaFindingCandidate> ParseJUnit(DiagnosticDocument document)
    {
        var xml=XDocument.Parse(document.Content,LoadOptions.PreserveWhitespace|LoadOptions.SetLineInfo);
        var output=new List<QaFindingCandidate>();
        foreach(var test in xml.Descendants().Where(x=>x.Name.LocalName=="testcase"))
        {
            var name=test.Attribute("name")?.Value??"test";
            var cls=test.Attribute("classname")?.Value;
            var file=test.Attribute("file")?.Value;
            int? line=int.TryParse(test.Attribute("line")?.Value,out var n)?n:null;
            foreach(var failure in test.Elements().Where(x=>x.Name.LocalName is "failure" or "error"))
            {
                var isError=failure.Name.LocalName=="error";
                var type=failure.Attribute("type")?.Value;
                var message=failure.Attribute("message")?.Value??failure.Value.Trim();
                output.Add(new QaFindingCandidate("test",isError?Severity.Critical:Severity.Warning,document.Tool,$"{name} {(isError?"errored":"failed")}",message,file,line,Function:cls,ExceptionType:type,StackTrace:failure.Value,RuleId:"junit"));
            }
        }
        return Deduplicate(output);
    }

    private static IReadOnlyList<QaFindingCandidate> ParseGenericJson(DiagnosticDocument document)
    {
        using var json=JsonDocument.Parse(document.Content);
        var output=new List<QaFindingCandidate>();
        IEnumerable<JsonElement> Items()
        {
            if(json.RootElement.ValueKind==JsonValueKind.Array) return json.RootElement.EnumerateArray().ToArray();
            foreach(var key in new[]{"results","diagnostics","findings","issues","errors"})
                if(json.RootElement.TryGetProperty(key,out var items)&&items.ValueKind==JsonValueKind.Array) return items.EnumerateArray().ToArray();
            return [json.RootElement];
        }
        foreach(var item in Items())
        {
            if(item.ValueKind!=JsonValueKind.Object) continue;
            var message=FirstString(item,"message","description","text","reason")??"Diagnostic finding";
            var rule=FirstString(item,"ruleId","rule","code","id");
            var category=FirstString(item,"category","type")??document.FallbackCategory;
            var severity=ParseSeverity(FirstString(item,"severity","level"),document.FallbackSeverity);
            var file=FirstString(item,"filePath","file","path","filename");
            var line=FirstInt(item,"line","lineNumber","startLine");
            var column=FirstInt(item,"column","columnNumber","startColumn");
            output.Add(new QaFindingCandidate(category,severity,document.Tool,rule is null?document.Tool:$"{document.Tool} {rule}",message,file,line,column,RuleId:rule));
        }
        return Deduplicate(output);
    }

    private static Severity SarifSeverity(string? level,Severity fallback)=>level?.ToLowerInvariant() switch
    {
        "error"=>Severity.Critical,"warning"=>Severity.Warning,"note" or "none"=>Severity.Info,_=>fallback
    };
    private static Severity ParseSeverity(string? value,Severity fallback)=>value?.ToLowerInvariant() switch
    {
        "critical" or "fatal" or "error" or "high"=>Severity.Critical,
        "warning" or "warn" or "medium"=>Severity.Warning,
        "info" or "information" or "note" or "low"=>Severity.Info,
        _=>fallback
    };

    private static IReadOnlyList<QaFindingCandidate> Deduplicate(IEnumerable<QaFindingCandidate> values)=>values
        .GroupBy(x=>string.Join('|',x.Tool,x.RuleId,x.FilePath,x.Line,x.Message),StringComparer.Ordinal)
        .Select(x=>x.First()).Take(1000).ToArray();
    private static string? String(JsonElement node,string property)=>node.TryGetProperty(property,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
    private static int? Int(JsonElement node,string property)=>node.TryGetProperty(property,out var value)&&value.TryGetInt32(out var n)?n:null;
    private static string? NestedString(JsonElement node,string parent,string property)=>node.TryGetProperty(parent,out var p)?String(p,property):null;
    private static string? FirstString(JsonElement node,params string[] names)=>names.Select(name=>String(node,name)).FirstOrDefault(x=>!string.IsNullOrWhiteSpace(x));
    private static int? FirstInt(JsonElement node,params string[] names)=>names.Select(name=>Int(node,name)).FirstOrDefault(x=>x.HasValue);
}
