using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

/// <summary>
/// Produces two deliberately separate progress numbers: what the team reported
/// and how much of that reported progress is currently backed by evidence.
/// </summary>
public sealed class ProgressEvaluator
{
    public ProgressAssessment Evaluate(
        IReadOnlyCollection<ProjectClaim> claims,
        IReadOnlyDictionary<Guid, ClaimVerificationAssessment> assessments)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(assessments);

        var latestClaims = claims
            .GroupBy(item => item.SubjectKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.ClaimedAtUtc).First())
            .ToArray();

        if (latestClaims.Length == 0)
            return new(0m, null);

        var reportedValues = latestClaims
            .Select(GetReportedProgress)
            .ToArray();

        var reported = decimal.Round(reportedValues.Average(), 2);

        var verifiedValues = latestClaims.Select(claim =>
        {
            var reportedValue = GetReportedProgress(claim);
            if (!assessments.TryGetValue(claim.Id, out var assessment))
                return 0m;

            var factor = assessment.Status switch
            {
                ClaimVerificationStatus.Confirmed => 1m,
                ClaimVerificationStatus.PartiallyConfirmed => 0.5m,
                ClaimVerificationStatus.Stale => 0.25m,
                ClaimVerificationStatus.Contradicted => 0m,
                _ => 0m
            };

            return reportedValue * factor;
        }).ToArray();

        return new(
            decimal.Round(verifiedValues.Average(), 2),
            reported);
    }

    private static decimal GetReportedProgress(ProjectClaim claim)
    {
        if (claim.ClaimedProgress is { } explicitProgress)
            return explicitProgress;

        var status = claim.ClaimedStatus;
        if (ContainsAny(status, "complete", "completed", "done", "finished", "ready", "完了", "完成", "対応済", "終了"))
            return 100m;
        if (ContainsAny(status, "not started", "todo", "未着手"))
            return 0m;
        if (ContainsAny(status, "blocked", "停止", "保留"))
            return 50m;
        if (ContainsAny(status, "progress", "working", "started", "作業中", "進行中", "着手"))
            return 50m;
        return 50m;
    }

    private static bool ContainsAny(string source, params string[] values) =>
        values.Any(value => source.Contains(value, StringComparison.OrdinalIgnoreCase));
}
