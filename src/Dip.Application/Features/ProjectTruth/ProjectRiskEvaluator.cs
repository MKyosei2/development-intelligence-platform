// Purpose: Evaluates P ro je ct Ri sk Ev al ua to r using DIP project evidence and policy.
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed class ProjectRiskEvaluator
{
    private readonly ProjectTruthPolicy _policy;

    public ProjectRiskEvaluator():this(ProjectTruthPolicy.Default){}
    public ProjectRiskEvaluator(ProjectTruthPolicy policy){_policy=(policy??throw new ArgumentNullException(nameof(policy))).Validate();}

    public ProjectRiskLevel Evaluate(
        ProgressAssessment progress,
        EvidenceConfidence confidence,
        IReadOnlyCollection<ProjectBlocker> blockers,
        int contradictionCount,
        int unverifiedClaimCount)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(blockers);

        if (blockers.Any(item => SeverityIs(item.Severity,"Critical","SafetyCritical","ReleaseCritical")))
            return ProjectRiskLevel.Critical;

        if (blockers.Any(item => SeverityIs(item.Severity,"Blocker","High","Rejected","Failed")))
            return ProjectRiskLevel.High;

        if (contradictionCount >= _policy.HighRiskContradictionThreshold)
            return ProjectRiskLevel.High;

        var progressGap = progress.ReportedProgress is { } reported
            ? Math.Max(0m, reported - progress.VerifiedProgress)
            : 0m;

        if (contradictionCount > 0
            || progressGap >= _policy.MediumRiskProgressGapThreshold
            || (unverifiedClaimCount > 0 && progress.ReportedProgress >= _policy.NearCompletionReportedProgressThreshold)
            || confidence <= _policy.MediumRiskMaximumConfidence)
            return ProjectRiskLevel.Medium;

        return ProjectRiskLevel.Low;
    }

    private static bool SeverityIs(string value,params string[] expected)
        =>expected.Any(x=>value.Equals(x,StringComparison.OrdinalIgnoreCase));
}
