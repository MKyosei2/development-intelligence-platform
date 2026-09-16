// Purpose: Coordinates P ro je ct Bl oc ke rE va lu at or application behavior for ProjectTruth.
using Dip.Application.Features.P2Intelligence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed class ProjectBlockerEvaluator
{
    private readonly ProjectTruthPolicy _policy;
    public ProjectBlockerEvaluator():this(ProjectTruthPolicy.Default){}
    public ProjectBlockerEvaluator(ProjectTruthPolicy policy){_policy=(policy??throw new ArgumentNullException(nameof(policy))).Validate();}

    public IReadOnlyList<ProjectBlocker> Evaluate(
        IReadOnlyCollection<ProjectClaim> claims,
        IReadOnlyDictionary<Guid, ClaimVerificationAssessment> assessments,
        IReadOnlyCollection<EvidenceRecord> evidence,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(assessments);
        ArgumentNullException.ThrowIfNull(evidence);

        var blockers = new List<ProjectBlocker>();
        var latestEvidence = EvidenceConflictPolicy.Resolve(evidence, now).Select(group => group.Authoritative).ToArray();

        foreach (var item in latestEvidence)
        {
            if (item.Outcome != EvidenceOutcome.Failed
                || EvidenceFreshnessPolicy.EffectiveConfidence(item, now) < EvidenceConfidence.High
                || EvidenceFreshnessPolicy.Freshness(item, now) == "Stale")
                continue;

            var critical = _policy.CriticalEvidenceKindTokens.Any(token =>
                item.EvidenceKind.Contains(token, StringComparison.OrdinalIgnoreCase)
                || item.Title.Contains(token,StringComparison.OrdinalIgnoreCase));

            blockers.Add(new(
                $"evidence:{item.EvidenceKind}",
                critical ? "Critical" : "Blocker",
                item.Title,
                item.Summary,
                item.SubjectKey,
                item.Id));
        }

        var latestClaims = claims
            .GroupBy(item => item.SubjectKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.ClaimedAtUtc).First())
            .ToArray();

        foreach (var claim in latestClaims)
        {
            if (!assessments.TryGetValue(claim.Id, out var assessment))
                continue;

            if (assessment.Status == ClaimVerificationStatus.Contradicted)
            {
                blockers.Add(new(
                    "claim:evidence-mismatch","Attention",
                    "Reported status conflicts with observed evidence",
                    assessment.Summary,claim.SubjectKey,
                    assessment.EvidenceLinks.FirstOrDefault(item => item.Relation == EvidenceRelation.Contradicts)?.EvidenceId));
            }
            else if (assessment.Status == ClaimVerificationStatus.Stale)
            {
                blockers.Add(new(
                    "claim:stale","Attention",
                    "Reported status needs fresh evidence",
                    assessment.Summary,claim.SubjectKey,null));
            }
        }

        return blockers.OrderByDescending(item => SeverityRank(item.Severity)).ThenBy(item => item.Title,StringComparer.OrdinalIgnoreCase).Take(25).ToArray();
    }

    private static int SeverityRank(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" or "safetycritical" or "releasecritical" => 3,
        "blocker" or "high" or "failed" or "rejected" => 2,
        "attention" or "medium" => 1,
        _ => 0
    };
}
