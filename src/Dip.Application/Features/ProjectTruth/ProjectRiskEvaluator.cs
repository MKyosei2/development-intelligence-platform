using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed class ProjectRiskEvaluator
{
    public ProjectRiskLevel Evaluate(
        ProgressAssessment progress,
        EvidenceConfidence confidence,
        IReadOnlyCollection<ProjectBlocker> blockers,
        int contradictionCount,
        int unverifiedClaimCount)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(blockers);

        if (blockers.Any(item => item.Severity == "Critical"))
            return ProjectRiskLevel.Critical;

        if (blockers.Any(item => item.Severity == "Blocker"))
            return ProjectRiskLevel.High;

        if (contradictionCount >= 2)
            return ProjectRiskLevel.High;

        var progressGap = progress.ReportedProgress is { } reported
            ? Math.Max(0m, reported - progress.VerifiedProgress)
            : 0m;

        if (contradictionCount == 1
            || progressGap >= 20m
            || (unverifiedClaimCount > 0
                && progress.ReportedProgress >= 80m)
            || confidence <= EvidenceConfidence.Low)
        {
            return ProjectRiskLevel.Medium;
        }

        return ProjectRiskLevel.Low;
    }
}
