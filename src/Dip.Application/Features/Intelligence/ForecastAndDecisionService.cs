// Purpose: Coordinates F or ec as tA nd De ci si on Se rv ic e application behavior for Intelligence.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Intelligence;

public sealed class ForecastAndDecisionService
{
    private readonly IEvidenceRepository _evidence;
    private readonly IProjectClaimRepository _claims;
    private readonly IProjectTruthRepository _truth;
    private readonly IProjectForecastStore _forecasts;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public ForecastAndDecisionService(
        IEvidenceRepository evidence,
        IProjectClaimRepository claims,
        IProjectTruthRepository truth,
        IProjectForecastStore forecasts,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _evidence = evidence;
        _claims = claims;
        _truth = truth;
        _forecasts = forecasts;
        _uow = uow;
        _clock = clock;
    }

    public async Task<ForecastResult> ForecastAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var evidence = await _evidence.ListForProjectAsync(
            projectId,
            now.AddDays(-60),
            cancellationToken);
        var claims = await _claims.ListByProjectAsync(projectId, cancellationToken);
        var snapshot = await _truth.GetLatestAsync(projectId, cancellationToken);
        var history = (await _truth.ListRecentAsync(projectId, 200, cancellationToken))
            .OrderBy(item => item.EvaluatedAtUtc)
            .ToArray();

        var critical = evidence.Count(item =>
            item.Outcome == EvidenceOutcome.Failed
            && (int)item.Confidence >= (int)EvidenceConfidence.High);
        var stale = claims.Count(item =>
            item.VerificationStatus == ClaimVerificationStatus.Stale);
        var contradicted = claims.Count(item =>
            item.VerificationStatus == ClaimVerificationStatus.Contradicted);

        var progress = snapshot?.VerifiedProgress
            ?? history.LastOrDefault()?.VerifiedProgress
            ?? 0m;
        var gap = (snapshot?.ReportedProgress ?? progress) - progress;
        var remaining = Math.Max(0m, 100m - progress);

        var velocity = CalculateVerifiedProgressVelocity(history, snapshot, now);
        var stagnant = remaining > 0m && (!velocity.HasValue || velocity.Value <= 0.05m);

        var risk = Math.Clamp(
            critical * 10m
            + contradicted * 12m
            + stale * 4m
            + Math.Max(0m, gap) * 0.7m
            + remaining * 0.15m
            + (stagnant ? 15m : 0m),
            0m,
            98m);

        var historySpanDays = history.Length < 2
            ? 0d
            : (history[^1].EvaluatedAtUtc - history[0].EvaluatedAtUtc).TotalDays;
        var confidence = evidence.Count >= 30 && history.Length >= 10 && historySpanDays >= 14d
            ? ForecastConfidence.High
            : evidence.Count >= 10 && history.Length >= 3 && historySpanDays >= 3d
                ? ForecastConfidence.Medium
                : ForecastConfidence.Low;

        DateTimeOffset? earliest = null;
        DateTimeOffset? latest = null;
        if (progress >= 100m)
        {
            earliest = now;
            latest = now;
        }
        else if (velocity is > 0.05m)
        {
            var days = Math.Clamp(
                remaining / velocity.Value,
                1m,
                180m);
            earliest = now.AddDays((double)(days * 0.85m));
            latest = now.AddDays((double)(days * 1.35m + critical * 2m));
        }

        var reasons = new List<string>
        {
            $"Evidence-backed progress is {progress:0.#}%.",
            $"{critical} high-confidence failed evidence item(s) exist in the last 60 days.",
            $"{contradicted} contradicted and {stale} stale claim(s) are currently recorded."
        };

        if (gap >= 20m)
            reasons.Add($"Reported progress exceeds evidence-backed progress by {gap:0.#} points.");

        if (velocity.HasValue)
        {
            reasons.Add(
                $"Verified-progress velocity is {velocity.Value:0.###} percentage points/day from Project Truth history.");
        }
        else
        {
            reasons.Add(
                "There is not enough Project Truth history to estimate a completion window; no date is inferred from evidence volume alone.");
        }

        var result = new ForecastResult(
            decimal.Round(risk, 2),
            confidence.ToString(),
            earliest,
            latest,
            reasons);

        await _forecasts.AddAsync(
            new ProjectForecast(
                Guid.NewGuid(),
                projectId,
                "release-risk",
                risk,
                earliest,
                latest,
                confidence,
                JsonSerializer.Serialize(reasons),
                now),
            cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<PmDecisionBrief> BriefAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var evidence = await _evidence.ListForProjectAsync(
            projectId,
            now.AddDays(-30),
            cancellationToken);
        var claims = await _claims.ListByProjectAsync(projectId, cancellationToken);
        var snapshot = await _truth.GetLatestAsync(projectId, cancellationToken);
        var history = await _truth.ListRecentAsync(projectId, 200, cancellationToken);

        var priorities = new List<PmDecisionItem>();
        foreach (var item in evidence
                     .Where(entry => entry.Outcome == EvidenceOutcome.Failed)
                     .OrderByDescending(entry => entry.Confidence)
                     .ThenByDescending(entry => entry.ObservedAtUtc)
                     .Take(5))
        {
            priorities.Add(new PmDecisionItem(
                priorities.Count + 1,
                item.SubjectKey,
                item.Title,
                item.Summary,
                "Confirm the owner and next verifiable action, then rerun the evidence source after the fix.",
                item.Confidence.ToString()));
        }

        foreach (var claim in claims
                     .Where(item => item.VerificationStatus is
                         ClaimVerificationStatus.Contradicted or ClaimVerificationStatus.Stale)
                     .Take(Math.Max(0, 5 - priorities.Count)))
        {
            priorities.Add(new PmDecisionItem(
                priorities.Count + 1,
                claim.SubjectKey,
                $"Reported status is {claim.VerificationStatus}",
                $"Claimed status: {claim.ClaimedStatus}",
                "Collect fresh build/test/runtime/asset evidence for this claim; do not treat this as a person-level trust judgment.",
                "Medium"));
        }

        var changes = evidence
            .Where(item => item.ObservedAtUtc > now.AddDays(-1))
            .GroupBy(item => item.Outcome)
            .Select(group => $"{group.Count()} new {group.Key} evidence item(s) since yesterday.")
            .ToArray();

        var health = new List<string>();
        if (snapshot is not null)
        {
            var previous = history
                .Where(item => item.EvaluatedAtUtc <= now.AddDays(-1))
                .OrderByDescending(item => item.EvaluatedAtUtc)
                .FirstOrDefault();

            if (previous is not null)
            {
                var healthDelta = snapshot.HealthScore - previous.HealthScore;
                if (Math.Abs(healthDelta) >= 0.5m)
                    health.Add($"Health score changed {healthDelta:+0.#;-0.#;0} since the latest snapshot at least one day old.");

                var progressDelta = snapshot.VerifiedProgress - previous.VerifiedProgress;
                if (Math.Abs(progressDelta) >= 0.5m)
                    health.Add($"Evidence-backed progress changed {progressDelta:+0.#;-0.#;0} points.");

                if (snapshot.RiskLevel != previous.RiskLevel)
                    health.Add($"Risk changed {previous.RiskLevel} → {snapshot.RiskLevel}.");
            }

            if (snapshot.BlockerCount > 0)
                health.Add($"{snapshot.BlockerCount} blocker(s) are reflected in Project Truth.");
            if (snapshot.ContradictionCount > 0)
                health.Add($"{snapshot.ContradictionCount} claim/evidence contradiction(s) require attention.");
            if ((snapshot.ReportedProgress ?? snapshot.VerifiedProgress) - snapshot.VerifiedProgress >= 20m)
                health.Add("Reported progress is materially ahead of evidence-backed progress.");
        }

        return new PmDecisionBrief(
            priorities,
            changes,
            health,
            priorities.Count == 0
                ? "No high-priority evidence-backed action was found."
                : $"{priorities.Count} evidence-backed priority item(s) need attention.");
    }

    private static decimal? CalculateVerifiedProgressVelocity(
        IReadOnlyList<ProjectTruthSnapshot> history,
        ProjectTruthSnapshot? latest,
        DateTimeOffset now)
    {
        if (latest is null || history.Count < 2)
            return null;

        var candidates = history
            .Where(item =>
                item.EvaluatedAtUtc < latest.EvaluatedAtUtc
                && item.EvaluatedAtUtc >= now.AddDays(-45))
            .OrderBy(item => item.EvaluatedAtUtc)
            .ToArray();
        if (candidates.Length == 0)
            return null;

        var oldest = candidates[0];
        var days = (decimal)(latest.EvaluatedAtUtc - oldest.EvaluatedAtUtc).TotalDays;
        if (days < 2m)
            return null;

        var progressDelta = latest.VerifiedProgress - oldest.VerifiedProgress;
        if (progressDelta <= 0m)
            return 0m;

        return decimal.Round(progressDelta / days, 4);
    }
}
