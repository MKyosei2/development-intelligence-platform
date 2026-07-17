using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Quality;

public sealed record QualityGateResultDto(
    string Status,
    int CriticalIssueCount,
    int WarningIssueCount,
    decimal Score,
    IReadOnlyList<string> Reasons);

public sealed class QualityGateEvaluator
{
    public QualityGateResultDto Evaluate(
        IReadOnlyCollection<Metric> metrics,
        IReadOnlyCollection<QualityIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(issues);

        var reasons = new List<string>();

        var criticalCount = issues.Count(x =>
            x.Status == IssueStatus.Open
            && x.Severity == Severity.Critical);

        var warningCount = issues.Count(x =>
            x.Status == IssueStatus.Open
            && x.Severity == Severity.Warning);

        var regressions = metrics
            .Where(x => x.DifferencePercent is not null)
            .Where(x => x.DifferencePercent <= -10m)
            .ToArray();

        decimal score = 100m;
        score -= criticalCount * 25m;
        score -= warningCount * 8m;
        score -= regressions.Length * 5m;
        score = Math.Clamp(score, 0m, 100m);

        if (criticalCount > 0)
            reasons.Add($"{criticalCount} critical issue(s) are open.");

        if (warningCount > 0)
            reasons.Add($"{warningCount} warning issue(s) are open.");

        foreach (var metric in regressions.Take(5))
        {
            reasons.Add(
                $"{metric.Name} regressed by {Math.Abs(metric.DifferencePercent!.Value):0.##}%.");
        }

        if (reasons.Count == 0)
            reasons.Add("No blocking quality issues or major regressions were found.");

        var status = criticalCount > 0 || score < 60m
            ? "Failed"
            : warningCount > 0 || regressions.Length > 0 || score < 85m
                ? "Warning"
                : "Passed";

        return new(
            status,
            criticalCount,
            warningCount,
            score,
            reasons);
    }
}
