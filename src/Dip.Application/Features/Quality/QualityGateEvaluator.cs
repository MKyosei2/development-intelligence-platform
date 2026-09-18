// Purpose: Aggregates deterministic quality-rule evaluations into a fail-closed gate status, score, and reasons.
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
    private readonly QualityRuleEvaluator _ruleEvaluator;

    public QualityGateEvaluator(QualityRuleEvaluator ruleEvaluator)
    {
        _ruleEvaluator = ruleEvaluator;
    }

    public QualityGateResultDto Evaluate(
        IReadOnlyCollection<Metric> metrics,
        IReadOnlyCollection<QualityIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(issues);

        // "No issue found" and "nothing meaningful was evaluated" are different
        // states. A summary-only/empty report is never promoted to Passed.
        if (!HasMeaningfulCoverage(metrics, issues))
        {
            return new(
                "Warning",
                0,
                1,
                0m,
                ["Quality evidence is incomplete: no meaningful metric or issue observation was evaluated. The gate is not PASS."]);
        }

        var evaluations = _ruleEvaluator.Evaluate(metrics, issues);
        var criticalCount = evaluations.Count(x => x.Severity == Severity.Critical);
        var warningCount = evaluations.Count(x => x.Severity == Severity.Warning);
        var score = Math.Clamp(100m - evaluations.Sum(x => x.ScorePenalty), 0m, 100m);
        var status = criticalCount > 0 || score < 60m
            ? "Failed"
            : warningCount > 0 || score < 85m
                ? "Warning"
                : "Passed";

        IReadOnlyList<string> reasons = evaluations.Count == 0
            ? ["Quality checks produced meaningful observations and no blocking issue or material regression was found."]
            : evaluations
                .OrderByDescending(x => x.Severity)
                .ThenBy(x => x.RuleId, StringComparer.Ordinal)
                .Select(x => x.Reason)
                .Take(20)
                .ToArray();

        return new(status, criticalCount, warningCount, score, reasons);
    }

    private static bool HasMeaningfulCoverage(
        IReadOnlyCollection<Metric> metrics,
        IReadOnlyCollection<QualityIssue> issues)
    {
        if (issues.Count > 0)
            return true;

        return metrics.Any(metric =>
            !metric.Name.Equals("collector_issue_critical_count", StringComparison.OrdinalIgnoreCase)
            && !metric.Name.Equals("collector_issue_warning_count", StringComparison.OrdinalIgnoreCase));
    }
}
