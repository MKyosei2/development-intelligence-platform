using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed record AdaptiveProjectRiskResult(
    ProjectRiskLevel RiskLevel,
    decimal HealthPenalty,
    decimal RecentFailureRatePercent,
    decimal PreviousFailureRatePercent,
    int ReproducedFailures,
    int RegressionSignals,
    string Trend,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Second-layer risk calibration. It never lowers the deterministic Project Truth
/// result. It only raises risk when recent objective evidence, reproduced failures,
/// regression signals, or sustained historical risk justify that decision.
/// </summary>
public sealed class AdaptiveProjectRiskEvaluator
{
    public AdaptiveProjectRiskResult Evaluate(
        ProjectRiskLevel baseline,
        IReadOnlyCollection<EvidenceRecord> evidence,
        IReadOnlyCollection<ProjectTruthSnapshot> history,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(history);

        var objective = evidence.Where(IsObjective).ToArray();
        var recent = objective.Where(x => x.ObservedAtUtc >= now.AddDays(-14)).ToArray();
        var previous = objective.Where(x => x.ObservedAtUtc >= now.AddDays(-28) && x.ObservedAtUtc < now.AddDays(-14)).ToArray();
        var recentRate = FailureRate(recent);
        var previousRate = FailureRate(previous);
        var reproduced = recent.Count(IsReproducedFailure);
        var regression = recent.Count(x => IsRegression(x) && x.Outcome is EvidenceOutcome.Failed or EvidenceOutcome.Warning);
        var reasons = new List<string>();
        var observed = ProjectRiskLevel.Low;

        if (reproduced >= 2)
        {
            observed = ProjectRiskLevel.Critical;
            reasons.Add($"{reproduced} mechanically reproduced failures were observed in the last 14 days.");
        }
        else if (reproduced == 1)
        {
            observed = Max(observed, ProjectRiskLevel.High);
            reasons.Add("A current failure has been mechanically reproduced.");
        }

        if (recent.Length >= 5 && recentRate >= 35m)
        {
            observed = Max(observed, ProjectRiskLevel.Critical);
            reasons.Add($"Recent objective failure rate is {recentRate:0.#}% across {recent.Length} observations.");
        }
        else if (recent.Length >= 5 && recentRate >= 20m)
        {
            observed = Max(observed, ProjectRiskLevel.High);
            reasons.Add($"Recent objective failure rate is {recentRate:0.#}% across {recent.Length} observations.");
        }
        else if (recent.Length >= 5 && recentRate >= 10m)
        {
            observed = Max(observed, ProjectRiskLevel.Medium);
            reasons.Add($"Recent objective failure rate is {recentRate:0.#}%.");
        }

        var worsening = recent.Length >= 5 && previous.Length >= 5 && recentRate - previousRate >= 15m;
        if (worsening)
        {
            observed = Max(observed, ProjectRiskLevel.High);
            reasons.Add($"Failure rate worsened by {(recentRate - previousRate):0.#} percentage points versus the prior 14-day window.");
        }

        if (regression >= 2)
        {
            observed = Max(observed, ProjectRiskLevel.High);
            reasons.Add($"{regression} current regression/change-risk signals are unresolved.");
        }
        else if (regression == 1)
        {
            observed = Max(observed, ProjectRiskLevel.Medium);
            reasons.Add("A current regression/change-risk signal is unresolved.");
        }

        var sustainedHigh = history.OrderByDescending(x => x.EvaluatedAtUtc).Take(3).Count(x => x.RiskLevel is ProjectRiskLevel.High or ProjectRiskLevel.Critical) >= 2;
        if (sustainedHigh)
        {
            observed = Max(observed, ProjectRiskLevel.High);
            reasons.Add("Recent Project Truth history shows sustained high/critical risk rather than a one-off spike.");
        }

        var effective = Max(baseline, observed);
        var raised = (int)effective > (int)baseline;
        var penalty = raised ? effective switch
        {
            ProjectRiskLevel.Critical => 15m,
            ProjectRiskLevel.High => 10m,
            ProjectRiskLevel.Medium => 5m,
            _ => 0m
        } : 0m;
        var trend = recent.Length < 5 || previous.Length < 5 ? "insufficient-history"
            : recentRate > previousRate + 5m ? "worsening"
            : recentRate + 5m < previousRate ? "improving"
            : "stable";

        if (reasons.Count == 0)
            reasons.Add("No adaptive signal justified raising the deterministic Project Truth risk.");

        return new AdaptiveProjectRiskResult(effective, penalty, recentRate, previousRate, reproduced, regression, trend, reasons);
    }

    private static bool IsObjective(EvidenceRecord e) => e.SourceType is EvidenceSourceType.Build
        or EvidenceSourceType.Test or EvidenceSourceType.Quality or EvidenceSourceType.Runtime
        or EvidenceSourceType.Performance or EvidenceSourceType.Unity or EvidenceSourceType.Maya or EvidenceSourceType.Unreal
        or EvidenceSourceType.Collector or EvidenceSourceType.Analysis;

    private static bool IsReproducedFailure(EvidenceRecord e)
    {
        if (e.Outcome != EvidenceOutcome.Failed) return false;
        if (e.Summary.Contains("automatically reproduced", StringComparison.OrdinalIgnoreCase)) return true;
        var payload = e.PayloadJson ?? string.Empty;
        return payload.Contains("\"ReproductionStatus\":\"Reproduced\"", StringComparison.OrdinalIgnoreCase)
               || payload.Contains("\"reproductionStatus\":\"Reproduced\"", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRegression(EvidenceRecord e) => e.EvidenceKind.Contains("regression", StringComparison.OrdinalIgnoreCase)
        || e.EvidenceKind.Contains("change-risk", StringComparison.OrdinalIgnoreCase)
        || e.EvidenceKind.Contains("benchmark", StringComparison.OrdinalIgnoreCase);

    private static decimal FailureRate(IReadOnlyCollection<EvidenceRecord> rows)
    {
        if (rows.Count == 0) return 0m;
        var failed = rows.Count(x => x.Outcome == EvidenceOutcome.Failed);
        var warning = rows.Count(x => x.Outcome == EvidenceOutcome.Warning);
        return decimal.Round((failed + warning * .5m) * 100m / rows.Count, 2);
    }

    private static ProjectRiskLevel Max(ProjectRiskLevel a, ProjectRiskLevel b) => (ProjectRiskLevel)Math.Max((int)a, (int)b);
}
