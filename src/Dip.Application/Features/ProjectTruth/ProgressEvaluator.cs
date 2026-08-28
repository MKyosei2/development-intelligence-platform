using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

/// <summary>
/// Produces two deliberately separate progress numbers: what the team reported
/// and how much of that reported progress is currently backed by evidence.
/// Status vocabulary is intentionally cross-domain (software/media/construction).
/// </summary>
public sealed class ProgressEvaluator
{
    private readonly ProjectTruthPolicy _policy;

    public ProgressEvaluator():this(ProjectTruthPolicy.Default){}
    public ProgressEvaluator(ProjectTruthPolicy policy){_policy=(policy??throw new ArgumentNullException(nameof(policy))).Validate();}

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

        var reportedValues = latestClaims.Select(GetReportedProgress).ToArray();
        var reported = decimal.Round(reportedValues.Average(), 2);

        var verifiedValues = latestClaims.Select(claim =>
        {
            var reportedValue = GetReportedProgress(claim);
            if (!assessments.TryGetValue(claim.Id, out var assessment))
                return 0m;

            var factor = assessment.Status switch
            {
                ClaimVerificationStatus.Confirmed => 1m,
                ClaimVerificationStatus.PartiallyConfirmed => _policy.PartiallyConfirmedProgressFactor,
                ClaimVerificationStatus.Stale => _policy.StaleProgressFactor,
                ClaimVerificationStatus.Contradicted => 0m,
                _ => 0m
            };

            return reportedValue * factor;
        }).ToArray();

        return new(decimal.Round(verifiedValues.Average(), 2), reported);
    }

    private static decimal GetReportedProgress(ProjectClaim claim)
    {
        if (claim.ClaimedProgress is { } explicitProgress)
            return explicitProgress;

        var status = claim.ClaimedStatus;
        if (ContainsAny(status,
            "complete","completed","done","finished","ready","approved","accepted","passed","delivered","published","rendered","reviewed","inspected",
            "完了","完成","対応済","終了","承認済","合格","納品済","公開済","レンダー済","レビュー済","検査済"))
            return 100m;
        if (ContainsAny(status, "not started","todo","planned","未着手","未開始","計画"))
            return 0m;
        if (ContainsAny(status, "blocked","rejected","failed","停止","保留","否認","差戻","不合格","失敗"))
            return 50m;
        if (ContainsAny(status, "progress","working","started","reviewing","inspecting","rendering","作業中","進行中","着手","レビュー中","検査中","レンダー中"))
            return 50m;
        return 50m;
    }

    private static bool ContainsAny(string source, params string[] values) =>
        values.Any(value => source.Contains(value, StringComparison.OrdinalIgnoreCase));
}
