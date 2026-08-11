using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Intelligence;

public sealed record PortabilityResult(decimal Score, string Risk, IReadOnlyList<string> Blockers, string EvidenceJson);
public sealed record EvidenceGovernanceResult(int Fresh, int Stale, int Conflicting, IReadOnlyList<string> Conflicts, IReadOnlyList<string> StagnantSubjects, IReadOnlyList<string> DependencyRisks);
public sealed record ProjectDependency(string SubjectKey, string DependsOnSubjectKey);

public sealed class P2AnalysisService
{
    private readonly IEvidenceRepository _evidence;
    private readonly IProjectClaimRepository _claims;
    private readonly ISystemClock _clock;

    public P2AnalysisService(IEvidenceRepository evidence, IProjectClaimRepository claims, ISystemClock clock)
    {
        _evidence = evidence;
        _claims = claims;
        _clock = clock;
    }

    public Task<PortabilityResult> PortabilityAsync(
        Guid projectId,
        string targetPlatform,
        decimal cpuRatio,
        decimal gpuRatio,
        decimal memoryRatio,
        decimal storageRatio,
        decimal thermalRatio,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dimensions = new Dictionary<string, decimal>
        {
            ["CPU"] = cpuRatio,
            ["GPU"] = gpuRatio,
            ["Memory"] = memoryRatio,
            ["Storage"] = storageRatio,
            ["Thermal"] = thermalRatio
        };
        var blockers = dimensions
            .Where(x => x.Value < 70)
            .OrderBy(x => x.Value)
            .Select(x => $"{x.Key} capability is {x.Value:0.#}% of the calibrated baseline.")
            .ToArray();
        var harmonic = dimensions.Values.Any(x => x <= 0)
            ? 0
            : 5m / dimensions.Values.Sum(x => 1m / Math.Max(1, x));
        var score = Math.Clamp(decimal.Round(harmonic, 2), 0, 100);
        var risk = score < 55 ? "High" : score < 75 ? "Medium" : "Low";
        var payload = JsonSerializer.Serialize(new
        {
            targetPlatform,
            score,
            risk,
            dimensions,
            blockers,
            simulated = true,
            realDevice = false
        });
        return Task.FromResult(new PortabilityResult(score, risk, blockers, payload));
    }

    public async Task<EvidenceGovernanceResult> GovernanceAsync(
        Guid projectId,
        IReadOnlyList<ProjectDependency>? dependencies,
        CancellationToken cancellationToken)
    {
        var evidence = await _evidence.ListForProjectAsync(projectId, _clock.UtcNow.AddDays(-180), cancellationToken);
        var claims = await _claims.ListByProjectAsync(projectId, cancellationToken);
        var staleCut = _clock.UtcNow.AddDays(-14);
        var stale = evidence.Count(x => x.ObservedAtUtc < staleCut);
        var fresh = evidence.Count - stale;
        var conflicts = new List<string>();

        foreach (var group in evidence.Where(x => x.ObservedAtUtc > _clock.UtcNow.AddDays(-30)).GroupBy(x => x.SubjectKey))
        {
            var high = group.Where(x => (int)x.Confidence >= (int)EvidenceConfidence.High).ToArray();
            if (high.Any(x => x.Outcome == EvidenceOutcome.Succeeded) && high.Any(x => x.Outcome == EvidenceOutcome.Failed))
                conflicts.Add($"{group.Key}: recent high-confidence evidence contains both success and failure; real-device/recent execution should take precedence over simulation/report-only evidence.");
        }

        var latestBySubject = evidence
            .GroupBy(x => x.SubjectKey)
            .ToDictionary(g => g.Key, g => g.Max(x => x.ObservedAtUtc), StringComparer.OrdinalIgnoreCase);
        var stagnant = claims
            .GroupBy(x => x.SubjectKey)
            .Select(g => g.OrderByDescending(x => x.ClaimedAtUtc).First())
            .Where(c => c.ClaimedStatus.Contains("progress", StringComparison.OrdinalIgnoreCase)
                        || c.ClaimedStatus.Contains("進行", StringComparison.OrdinalIgnoreCase)
                        || c.ClaimedProgress is > 0 and < 100)
            .Where(c => !latestBySubject.TryGetValue(c.SubjectKey, out var t) || t < _clock.UtcNow.AddDays(-5))
            .Select(c => $"{c.SubjectKey}: reported as in progress but no fresh objective evidence was observed for at least 5 days.")
            .ToArray();

        var dependencyRisks = new List<string>();
        if (dependencies is not null)
        {
            var verified = claims
                .GroupBy(x => x.SubjectKey)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ClaimedAtUtc).First().VerificationStatus, StringComparer.OrdinalIgnoreCase);
            foreach (var dependency in dependencies)
            {
                if (!verified.TryGetValue(dependency.DependsOnSubjectKey, out var status) || status != ClaimVerificationStatus.Confirmed)
                    dependencyRisks.Add($"{dependency.SubjectKey}: dependency {dependency.DependsOnSubjectKey} is not confirmed by current evidence.");
            }
        }

        return new(fresh, stale, conflicts.Count, conflicts, stagnant, dependencyRisks);
    }
}
