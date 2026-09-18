// Purpose: Evaluates project evidence confidence without mixing historical revisions into current verification.
using Dip.Application.Features.P2Intelligence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

/// <summary>
/// Converts recent objective evidence into a project-level confidence.
/// When revision-bound evidence exists, only evidence applicable to the latest
/// observed revision contributes to current confidence.
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

        var pipelineRevisions = EvidenceRevisionPolicy.BuildPipelineRevisionMap(recent);
        var currentRevision = EvidenceRevisionPolicy.ResolveCurrentRevision(recent, pipelineRevisions);
        var applicable = recent
            .Where(item => EvidenceRevisionPolicy.AppliesToCurrentRevision(
                item,
                currentRevision,
                pipelineRevisions))
            .ToArray();

        if (applicable.Length == 0)
            return EvidenceConfidence.Unverified;

        var effective = applicable
            .Select(item => new
            {
                Item = item,
                Confidence = EvidenceFreshnessPolicy.EffectiveConfidence(item, now)
            })
            .ToArray();

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
        var provider = item.PayloadValue("provider")
            ?? item.PayloadValue("sourceProvider")
            ?? item.PayloadValue("source")
            ?? item.SourceInstanceId;

        return string.IsNullOrWhiteSpace(provider)
            ? item.SourceType.ToString()
            : $"{item.SourceType}:{provider.Trim()}";
    }
}
