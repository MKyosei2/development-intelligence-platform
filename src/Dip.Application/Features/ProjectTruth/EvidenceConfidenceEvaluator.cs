using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

/// <summary>
/// Converts the source and declared confidence of recent evidence into a
/// project-level confidence. Multiple independent machine sources are needed
/// before a project can be described as fully verified.
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

        var distinctSources = recent
            .Where(item => item.Confidence >= EvidenceConfidence.High)
            .Select(item => item.SourceType)
            .Distinct()
            .Count();

        var strongest = recent.Max(item => item.Confidence);

        if (strongest == EvidenceConfidence.Verified && distinctSources >= 3)
            return EvidenceConfidence.Verified;

        if (strongest >= EvidenceConfidence.High)
            return EvidenceConfidence.High;

        if (strongest == EvidenceConfidence.Medium)
            return EvidenceConfidence.Medium;

        return EvidenceConfidence.Low;
    }
}
