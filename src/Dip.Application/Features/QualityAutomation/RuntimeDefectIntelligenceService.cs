// Purpose: Orchestrates the DIP workflow for R un ti me De fe ct In te ll ig en ce Se rv ic e.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Features.Incidents;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation;

public sealed class RuntimeDefectIntelligenceService(IncidentAggregationService incidents):IRuntimeDefectIntelligence
{
    public async Task<RuntimeDefectResult> ObserveAsync(RuntimeDefectObservation o,CancellationToken ct)
    {
        var text=string.Join(Environment.NewLine,new string?[]{o.Title,o.Message,o.ExceptionType,o.StackTrace}.Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x!));
        var signals=new List<string>();var category="runtime";var severity=Severity.Critical;
        void Signal(bool condition,string value){if(condition)signals.Add(value);}
        Signal(Has(text,"deadlock","lock timeout","monitor contention"),"deadlock/hang signature");
        Signal(Has(text,"data race","race detected","threadsanitizer"),"data-race signature");
        Signal(Has(text,"outofmemory","out of memory","memory leak","heap-use-after-free","addresssanitizer"),"memory-safety/pressure signature");
        Signal(Has(text,"timeout","timed out","watchdog"),"timeout/watchdog signature");
        Signal(Has(text,"nullreference","null pointer","nil pointer","segmentation fault","access violation"),"invalid-reference/crash signature");
        if(signals.Any(x=>x.Contains("deadlock")||x.Contains("data-race")))category="concurrency";
        else if(signals.Any(x=>x.Contains("memory")))category="memory";
        var normalized=NormalizeForFingerprint(o.ExceptionType??o.Title)+"|"+FirstStackFrame(o.StackTrace)+"|"+(o.FilePath??"")+"|"+(o.Line?.ToString()??"");
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant()[..32];
        var detail=JsonSerializer.Serialize(new{o.Source,o.Language,o.Message,o.ExceptionType,o.StackTrace,o.CommitSha,o.BuildId,o.SceneOrRoute,o.FilePath,o.Line,o.Tags,signals});
        var incident=await incidents.ObserveSyntheticAsync(o.ProjectId,o.SubjectKey,fingerprint,o.Title,severity,detail,null,ct);
        var summary=signals.Count==0?"Runtime failure captured and clustered by stable fingerprint.":$"Runtime failure captured; signals: {string.Join(", ",signals)}.";
        return new(incident.Id,fingerprint,severity.ToString(),category,summary,signals);
    }
    private static bool Has(string text,params string[] values)=>values.Any(v=>text.Contains(v,StringComparison.OrdinalIgnoreCase));
    private static string NormalizeForFingerprint(string text)=>string.Concat(text.ToLowerInvariant().Where(c=>char.IsLetterOrDigit(c)||char.IsWhiteSpace(c))).Trim();
    private static string FirstStackFrame(string? stack)=>string.IsNullOrWhiteSpace(stack)?"":stack.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(x=>x.Contains(" at ",StringComparison.OrdinalIgnoreCase)||x.Contains(':'))?.Trim()??"";
}
