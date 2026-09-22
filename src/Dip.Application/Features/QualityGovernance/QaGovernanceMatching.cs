// Purpose: Centralizes QA governance path, waiver, and lifecycle matching rules.
using System.Text.RegularExpressions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityGovernance;

internal static class QaGovernanceMatching
{
    public static bool IsProductFailure(QaFinding finding) =>
        finding.Severity == Severity.Critical
        || finding.Category.Contains("test", StringComparison.OrdinalIgnoreCase)
        || finding.Category.Contains("compile", StringComparison.OrdinalIgnoreCase)
        || finding.Category.Contains("runtime", StringComparison.OrdinalIgnoreCase)
        || finding.Category.Contains("crash", StringComparison.OrdinalIgnoreCase);

    public static bool IsClosed(FindingLifecycleStatus status) =>
        status is FindingLifecycleStatus.Verified
            or FindingLifecycleStatus.Closed
            or FindingLifecycleStatus.Duplicate
            or FindingLifecycleStatus.WontFix;

    public static bool MatchesWaiver(QaWaiver waiver, QaFinding finding) =>
        (!string.IsNullOrWhiteSpace(waiver.Fingerprint)
            && waiver.Fingerprint.Equals(finding.Fingerprint, StringComparison.OrdinalIgnoreCase))
        || (!string.IsNullOrWhiteSpace(waiver.RuleId)
            && string.Equals(waiver.RuleId, finding.RuleId, StringComparison.OrdinalIgnoreCase))
        || (!string.IsNullOrWhiteSpace(waiver.PathPattern)
            && Glob(waiver.PathPattern!, finding.FilePath ?? string.Empty));

    public static bool TryOwner(IReadOnlyList<QaOwnershipRule> rules, string? path, out QaOwnershipRule? owner)
    {
        owner = rules.FirstOrDefault(x => Glob(x.PathPattern, path ?? string.Empty));
        return owner is not null;
    }

    public static bool Glob(string pattern, string value)
    {
        var normalizedPattern = (pattern ?? string.Empty).Replace('\\', '/');
        var normalizedValue = (value ?? string.Empty).Replace('\\', '/');
        var expression = "^"
            + Regex.Escape(normalizedPattern)
                .Replace("\\*\\*", ".*")
                .Replace("\\*", "[^/]*")
                .Replace("\\?", ".")
            + "$";
        return Regex.IsMatch(
            normalizedValue,
            expression,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
