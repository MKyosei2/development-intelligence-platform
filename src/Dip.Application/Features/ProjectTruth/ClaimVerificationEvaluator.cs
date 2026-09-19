// Purpose: Evaluates a reported project claim against revision-bound objective evidence.
using Dip.Application.Features.P2Intelligence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

/// <summary>
/// Compares a human status report with objective evidence for the same subject.
/// Evidence from a historical revision is retained for context but cannot verify
/// the current revision.
/// </summary>
public sealed class ClaimVerificationEvaluator
{
    public ClaimVerificationAssessment Evaluate(
        ProjectClaim claim,
        IReadOnlyCollection<EvidenceRecord> evidence,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(evidence);

        var relevant = evidence
            .Where(item => string.Equals(
                item.SubjectKey,
                claim.SubjectKey,
                StringComparison.OrdinalIgnoreCase))
            .Where(item => item.ObservedAtUtc >= claim.ClaimedAtUtc.AddHours(-24))
            .OrderByDescending(item => item.ObservedAtUtc)
            .Take(50)
            .ToArray();

        var pipelineRevisions = EvidenceRevisionPolicy.BuildPipelineRevisionMap(relevant);
        var currentRevision =
            EvidenceRevisionPolicy.ResolveCurrentRevision(relevant, pipelineRevisions);

        var applicable = relevant
            .Where(item => EvidenceRevisionPolicy.AppliesToCurrentRevision(
                item,
                currentRevision,
                pipelineRevisions))
            .ToArray();

        var resolved = EvidenceConflictPolicy.Resolve(applicable, now);
        var authoritative = resolved.Select(item => item.Authoritative).ToArray();
        var authoritativeIds = authoritative.Select(item => item.Id).ToHashSet();
        var semanticState = ClaimStatusNormalizer.Classify(claim.ClaimedStatus);

        var links = relevant.Select(item =>
        {
            var applies = EvidenceRevisionPolicy.AppliesToCurrentRevision(
                item,
                currentRevision,
                pipelineRevisions);

            var revision = EvidenceRevisionPolicy.EffectiveRevision(item, pipelineRevisions);
            var isAuthoritative = applies && authoritativeIds.Contains(item.Id);
            var relation = isAuthoritative
                ? RelationFor(semanticState, item.Outcome)
                : EvidenceRelation.Neutral;

            var freshness = EvidenceFreshnessPolicy.Freshness(item, now);
            var revisionText = currentRevision is null
                ? "revision not declared"
                : applies
                    ? $"current revision {currentRevision}"
                    : $"historical revision {revision ?? "unknown"}; current is {currentRevision}";

            var reason = relation switch
            {
                EvidenceRelation.Supports =>
                    $"{item.EvidenceKind} supports the reported state ({freshness}; {revisionText}).",
                EvidenceRelation.Contradicts =>
                    $"{item.EvidenceKind} conflicts with the reported state ({freshness}; {revisionText}).",
                _ =>
                    $"{item.EvidenceKind} is contextual, superseded, stale, or revision-inapplicable evidence ({freshness}; {revisionText})."
            };

            return new ClaimEvidenceAssessment(item.Id, relation, reason);
        }).ToArray();

        var strongContradictions = authoritative
            .Where(item =>
                RelationFor(semanticState, item.Outcome) == EvidenceRelation.Contradicts)
            .Where(item =>
                EvidenceFreshnessPolicy.IsFreshEnoughForVerification(item, now))
            .ToArray();

        if (strongContradictions.Length > 0)
        {
            return new(
                claim.Id,
                ClaimVerificationStatus.Contradicted,
                $"{strongContradictions.Length} high-confidence current-revision evidence item(s) conflict with the report.",
                links);
        }

        var strongSupports = authoritative
            .Where(item =>
                RelationFor(semanticState, item.Outcome) == EvidenceRelation.Supports)
            .Where(item =>
                EvidenceFreshnessPolicy.IsFreshEnoughForVerification(item, now))
            .GroupBy(item => item.EvidenceKind, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        var warningCount = authoritative.Count(item =>
            item.Outcome == EvidenceOutcome.Warning
            && EvidenceFreshnessPolicy.EffectiveConfidence(item, now) >= EvidenceConfidence.High
            && EvidenceFreshnessPolicy.Freshness(item, now) != "Stale");

        var completionLike =
            semanticState is ClaimSemanticState.Complete or ClaimSemanticState.Ready;

        if (completionLike && strongSupports.Length >= 2 && warningCount == 0)
        {
            return new(
                claim.Id,
                ClaimVerificationStatus.Confirmed,
                currentRevision is null
                    ? "The completion report is supported by multiple high-confidence checks."
                    : $"The completion report is supported by multiple high-confidence checks bound to revision {currentRevision}.",
                links);
        }

        if (!completionLike && strongSupports.Length >= 1 && warningCount == 0)
        {
            return new(
                claim.Id,
                ClaimVerificationStatus.Confirmed,
                currentRevision is null
                    ? "The reported state is supported by high-confidence evidence."
                    : $"The reported state is supported by high-confidence evidence bound to revision {currentRevision}.",
                links);
        }

        if (strongSupports.Length >= 1 || warningCount > 0)
        {
            return new(
                claim.Id,
                ClaimVerificationStatus.PartiallyConfirmed,
                "Some current-revision evidence supports the report, but the evidence set is incomplete or contains warnings.",
                links);
        }

        var evidenceAfterClaim =
            applicable.Any(item => item.ObservedAtUtc >= claim.ClaimedAtUtc);

        if (!evidenceAfterClaim && claim.ClaimedAtUtc < now.AddDays(-7))
        {
            return new(
                claim.Id,
                ClaimVerificationStatus.Stale,
                currentRevision is null
                    ? "The report is old and has no newer objective evidence."
                    : $"The report is old and has no newer objective evidence for revision {currentRevision}.",
                links);
        }

        return new(
            claim.Id,
            ClaimVerificationStatus.Unverified,
            currentRevision is null
                ? "There is not enough objective evidence to verify this report yet."
                : $"There is not enough objective evidence bound to current revision {currentRevision} to verify this report yet.",
            links);
    }

    private static EvidenceRelation RelationFor(
        ClaimSemanticState state,
        EvidenceOutcome outcome)
    {
        return state switch
        {
            ClaimSemanticState.Complete or ClaimSemanticState.Ready => outcome switch
            {
                EvidenceOutcome.Succeeded => EvidenceRelation.Supports,
                EvidenceOutcome.Failed => EvidenceRelation.Contradicts,
                _ => EvidenceRelation.Neutral
            },
            ClaimSemanticState.Blocked or ClaimSemanticState.Failed => outcome switch
            {
                EvidenceOutcome.Failed => EvidenceRelation.Supports,
                _ => EvidenceRelation.Neutral
            },
            ClaimSemanticState.InProgress => outcome switch
            {
                EvidenceOutcome.Succeeded => EvidenceRelation.Supports,
                _ => EvidenceRelation.Neutral
            },
            ClaimSemanticState.NotStarted => EvidenceRelation.Neutral,
            _ => outcome switch
            {
                EvidenceOutcome.Succeeded => EvidenceRelation.Supports,
                EvidenceOutcome.Failed => EvidenceRelation.Contradicts,
                _ => EvidenceRelation.Neutral
            }
        };
    }
}
