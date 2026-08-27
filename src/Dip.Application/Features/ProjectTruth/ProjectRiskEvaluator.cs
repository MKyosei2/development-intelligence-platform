using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed class ProjectRiskEvaluator
{
    private readonly ProjectTruthPolicy _policy;

    public ProjectRiskEvaluator()
        : this(ProjectTruthPolicy.Default)
    {
    }

    public ProjectRiskEvaluator(ProjectTruthPolicy policy)
    {
        _policy = (policy ?? throw new ArgumentNullException(nameof(policy))).Validate();
    }

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

        if (contradictionCount >= _policy.HighRiskContradictionThreshold)
            return ProjectRiskLevel.High;

        var progressGap = progress.ReportedProgress is { } reported
            ? Math.Max(0m, reported - progress.VerifiedProgress)
            : 0m;

        if (contradictionCount > 0
            || progressGap >= _policy.MediumRiskProgressGapThreshold
            || (unverifiedClaimCount > 0
                && progress.ReportedProgress >= _policy.NearCompletionReportedProgressThreshold)
            || confidence <= _policy.MediumRiskMaximumConfidence)
        {
            return ProjectRiskLevel.Medium;
        }

        return ProjectRiskLevel.Low;
    }
}
