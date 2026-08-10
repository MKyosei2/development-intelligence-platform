using System.Security.Cryptography;
using System.Text;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation;

public static class QaFingerprint
{
    public static string Create(QaLanguage language,QaFindingCandidate finding)
    {
        var stable = string.Join('|',
            language,
            Normalize(finding.Category),
            Normalize(finding.ExceptionType),
            Normalize(finding.RuleId),
            NormalizePath(finding.FilePath),
            finding.Line?.ToString() ?? string.Empty,
            Normalize(finding.Function),
            Normalize(FirstMeaningfulLine(finding.StackTrace ?? finding.Message)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stable)));
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
    private static string NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized=value.Replace('\\','/').Trim();
        var parts=normalized.Split('/',StringSplitOptions.RemoveEmptyEntries);
        return string.Join('/',parts.TakeLast(Math.Min(3,parts.Length))).ToLowerInvariant();
    }
    private static string FirstMeaningfulLine(string value) => value.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
}
