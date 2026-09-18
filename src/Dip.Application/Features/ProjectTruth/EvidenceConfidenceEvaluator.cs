// Purpose: Evaluates E vi de nc eC on fi de nc eE va lu at or using DIP project evidence and policy.
using Dip.Application.Features.P2Intelligence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

/// <summary>
/// Converts the source and declared confidence of recent evidence into a
/// project-level confidence. Multiple independent providers are needed before
/// a project can be described as fully verified.
/// </summary>
public sealed class EvidenceConfidenceEvaluator
{
    public EvidenceConfidence Evaluate(
        IReadOnlyCollection<EvidenceRecord> evidence,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var recent = evidence
            .Where(item => item.ObservedAtUtc >= now.AddDays(-30))
            .Where(item => item.SourceType != EvidenceSourceType.HumanReport)
            .ToArray();

        if (recent.Length == 0)
            return EvidenceConfidence.Unverified;

        var effective = recent
            .Select(item => new { Item = item, Confidence = EvidenceFreshnessPolicy.EffectiveConfidence(item, now) })
            .ToArray();

        // SourceType alone is too coarse for an extensible platform. Prefer an
        // explicit provider/source id in evidence payload when present.
        var distinctSources = effective
            .Where(item => item.Confidence >= EvidenceConfidence.High)
            .Select(item => SourceIdentity(item.Item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var strongest = effective.Max(item => item.Confidence);

        if (strongest == EvidenceConfidence.Verified && distinctSources >= 3)
            return EvidenceConfidence.Verified;
        if (strongest >= EvidenceConfidence.High)
            return EvidenceConfidence.High;
        if (strongest == EvidenceConfidence.Medium)
            return EvidenceConfidence.Medium;
        return EvidenceConfidence.Low;
    }

    private static string SourceIdentity(EvidenceRecord item)
    {
        var provider=item.PayloadValue("provider")
            ??item.PayloadValue("sourceProvider")
            ??item.PayloadValue("source");
        return string.IsNullOrWhiteSpace(provider)
            ?item.SourceType.ToString()
            :$"{item.SourceType}:{provider.Trim()}";
    }
}
