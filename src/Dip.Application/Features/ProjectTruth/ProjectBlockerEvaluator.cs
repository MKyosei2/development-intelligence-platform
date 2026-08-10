using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed class ProjectBlockerEvaluator
{
    private static readonly string[] CriticalKinds =
    [
        "build", "runtime", "integration", "release", "pipeline"
    ];

    public IReadOnlyList<ProjectBlocker> Evaluate(
        IReadOnlyCollection<ProjectClaim> claims,
        IReadOnlyDictionary<Guid, ClaimVerificationAssessment> assessments,
        IReadOnlyCollection<EvidenceRecord> evidence)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(assessments);
        ArgumentNullException.ThrowIfNull(evidence);

        var blockers = new List<ProjectBlocker>();

        // Only the latest observation for a subject/kind pair is authoritative.
        var latestEvidence = evidence
            .GroupBy(
                item => $"{item.SubjectKey}\u001f{item.EvidenceKind}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.ObservedAtUtc).First())
            .ToArray();

        foreach (var item in latestEvidence)
        {
            if (item.Outcome != EvidenceOutcome.Failed
                || item.Confidence < EvidenceConfidence.High)
            {
                continue;
            }

            var critical = CriticalKinds.Any(token =>
                item.EvidenceKind.Contains(token, StringComparison.OrdinalIgnoreCase));

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
                    "claim:evidence-mismatch",
                    "Attention",
                    "Reported status conflicts with observed evidence",
                    assessment.Summary,
                    claim.SubjectKey,
                    assessment.EvidenceLinks
                        .FirstOrDefault(item => item.Relation == EvidenceRelation.Contradicts)
                        ?.EvidenceId));
            }
            else if (assessment.Status == ClaimVerificationStatus.Stale)
            {
                blockers.Add(new(
                    "claim:stale",
                    "Attention",
                    "Reported status needs fresh evidence",
                    assessment.Summary,
                    claim.SubjectKey,
                    null));
            }
        }

        return blockers
            .OrderByDescending(item => SeverityRank(item.Severity))
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .ToArray();
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 3,
        "Blocker" => 2,
        "Attention" => 1,
        _ => 0
    };
}
