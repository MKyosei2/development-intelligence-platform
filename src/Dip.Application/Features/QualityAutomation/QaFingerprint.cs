// Purpose: Coordinates Q aF in ge rp ri nt application behavior for QualityAutomation.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.QualityAutomation;

public static class QaFingerprint
{
    public static string Create(LanguageId language,QaFindingCandidate finding)
    {
        var category=Normalize(finding.Category);
        var stable=language==LanguageId.Python&&category=="runtime"?PythonRuntimeStable(finding):string.Join('|',language.Value,category,Normalize(finding.ExceptionType),Normalize(finding.RuleId),NormalizePath(finding.FilePath),finding.Line?.ToString(CultureInfo.InvariantCulture)??string.Empty,Normalize(finding.Function),Normalize(FirstMeaningfulLine(finding.StackTrace??finding.Message)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stable)));
    }
    private static string PythonRuntimeStable(QaFindingCandidate finding)
    {
        var exceptionType=Normalize(finding.ExceptionType);var message=Normalize(finding.Message);
        if(string.IsNullOrEmpty(exceptionType)){var colon=message.IndexOf(':');if(colon>0){var candidate=message[..colon].Trim();if(candidate.EndsWith("error",StringComparison.Ordinal)||candidate.EndsWith("exception",StringComparison.Ordinal)){exceptionType=candidate;message=message[(colon+1)..].Trim();}}}
        return string.Join('|',LanguageId.Python.Value,"runtime",exceptionType,message);
    }
    private static string Normalize(string? value)=>(value??string.Empty).Trim().ToLowerInvariant();
    private static string NormalizePath(string? value){if(string.IsNullOrWhiteSpace(value))return string.Empty;var normalized=value.Replace('\\','/').Trim();var parts=normalized.Split('/',StringSplitOptions.RemoveEmptyEntries);return string.Join('/',parts.TakeLast(Math.Min(3,parts.Length))).ToLowerInvariant();}
    private static string FirstMeaningfulLine(string value)=>value.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim()??string.Empty;
}
