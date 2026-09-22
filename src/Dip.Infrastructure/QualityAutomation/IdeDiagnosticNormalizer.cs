// Purpose: Provides infrastructure behavior for this application boundary.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class IdeDiagnosticNormalizer : IIdeDiagnosticNormalizer
{
    public IReadOnlyList<QaFindingCandidate> Normalize(IdeDiagnosticBatch batch) => batch.Diagnostics.Take(5000).Select(d => new QaFindingCandidate(
        "ide-lsp", ParseSeverity(d.Severity), $"ide:{d.Source}", $"{d.Source} {d.Code}".Trim(), d.Message, d.FilePath, d.Line, d.Column, RuleId: string.IsNullOrWhiteSpace(d.Code) ? null : d.Code, SourceFormat: "lsp", SourceFormatVersion: "3.17")).ToArray();
    private static Severity ParseSeverity(string value) => value.ToLowerInvariant() switch { "error" or "critical" => Severity.Critical, "warning" or "warn" => Severity.Warning, _ => Severity.Info };
}
