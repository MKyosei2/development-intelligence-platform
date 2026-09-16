// Purpose: Coordinates C la im Ve ri fi ca ti on Ev al ua to r application behavior for ProjectTruth.
using Dip.Application.Features.P2Intelligence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

/// <summary>
/// Compares a human status report with objective evidence for the same subject.
/// It deliberately evaluates the statement, not the person who made it. The
/// completion vocabulary intentionally spans software, media and construction;
/// verification semantics remain EvidenceKind/Outcome based and domain-neutral.
/// </summary>
public sealed class ClaimVerificationEvaluator
{
    private static readonly string[] CompletionTokens =
    [
        "complete", "completed", "done", "finished", "ready", "approved", "published", "delivered", "inspected",
        "完了", "完成", "対応済", "終了", "承認済", "公開済", "納品済", "検査済", "レビュー済", "レンダー完了"
    ];

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
            // Evidence immediately preceding a report is often exactly what the
            // reporter used to make the report, so keep a 24-hour look-back.
            .Where(item => item.ObservedAtUtc >= claim.ClaimedAtUtc.AddHours(-24))
            .OrderByDescending(item => item.ObservedAtUtc)
            .Take(50)
            .ToArray();

        var resolved = EvidenceConflictPolicy.Resolve(relevant, now);
        var authoritative = resolved.Select(item => item.Authoritative).ToArray();

        var links = relevant.Select(item =>
        {
            var isAuthoritative = authoritative.Any(x => x.Id == item.Id);
            var relation = !isAuthoritative
                ? EvidenceRelation.Neutral
                : item.Outcome switch
                {
                    EvidenceOutcome.Failed => EvidenceRelation.Contradicts,
                    EvidenceOutcome.Succeeded => EvidenceRelation.Supports,
                    _ => EvidenceRelation.Neutral
                };

            var freshness = EvidenceFreshnessPolicy.Freshness(item, now);
            var reason = relation switch
            {
                EvidenceRelation.Supports => $"{item.EvidenceKind} succeeded ({freshness}).",
                EvidenceRelation.Contradicts => $"{item.EvidenceKind} failed ({freshness}).",
                _ => $"{item.EvidenceKind} is contextual, superseded, or stale evidence ({freshness})."
            };

            return new ClaimEvidenceAssessment(item.Id, relation, reason);
        }).ToArray();

        var strongFailures = authoritative
            .Where(item => item.Outcome == EvidenceOutcome.Failed)
            .Where(item => EvidenceFreshnessPolicy.IsFreshEnoughForVerification(item, now))
            .ToArray();

        if (strongFailures.Length > 0)
        {
            return new(
                claim.Id,
                ClaimVerificationStatus.Contradicted,
                $"{strongFailures.Length} high-confidence evidence item(s) conflict with the report.",
                links);
        }

        var strongSupports = authoritative
            .Where(item => item.Outcome == EvidenceOutcome.Succeeded)
            .Where(item => EvidenceFreshnessPolicy.IsFreshEnoughForVerification(item, now))
            .GroupBy(item => item.EvidenceKind, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        var warningCount = authoritative.Count(item =>
            item.Outcome == EvidenceOutcome.Warning
            && EvidenceFreshnessPolicy.EffectiveConfidence(item, now) >= EvidenceConfidence.High
            && EvidenceFreshnessPolicy.Freshness(item, now) != "Stale");

        var completionClaim = CompletionTokens.Any(token =>
            claim.ClaimedStatus.Contains(token, StringComparison.OrdinalIgnoreCase));

        if (completionClaim && strongSupports.Length >= 2 && warningCount == 0)
        {
            return new(
                claim.Id,
                ClaimVerificationStatus.Confirmed,
                "The completion report is supported by multiple independent high-confidence checks.",
                links);
        }

        if (!completionClaim && strongSupports.Length >= 1 && warningCount == 0)
        {
            return new(
                claim.Id,
                ClaimVerificationStatus.Confirmed,
                "The reported state is supported by high-confidence evidence.",
                links);
        }

        if (strongSupports.Length >= 1 || warningCount > 0)
        {
            return new(
                claim.Id,
                ClaimVerificationStatus.PartiallyConfirmed,
                "Some evidence supports the report, but the evidence set is incomplete or contains warnings.",
                links);
        }

        var evidenceAfterClaim = relevant.Any(item => item.ObservedAtUtc >= claim.ClaimedAtUtc);
        if (!evidenceAfterClaim && claim.ClaimedAtUtc < now.AddDays(-7))
        {
            return new(
                claim.Id,
                ClaimVerificationStatus.Stale,
                "The report is old and has no newer objective evidence.",
                links);
        }

        return new(
            claim.Id,
            ClaimVerificationStatus.Unverified,
            "There is not enough objective evidence to verify this report yet.",
            links);
    }
}
