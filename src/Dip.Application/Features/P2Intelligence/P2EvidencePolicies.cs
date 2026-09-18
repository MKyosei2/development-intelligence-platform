// Purpose: Defines freshness, revision applicability, and conflict-resolution policy for project evidence.
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.P2Intelligence;

/// <summary>
/// An old observation is never silently treated as a current measurement.
/// Simulation/prediction evidence decays faster than measured evidence.
/// </summary>
public static class EvidenceFreshnessPolicy
{
    public static TimeSpan FreshWindow(EvidenceRecord item)
    {
        if (item.SourceType == EvidenceSourceType.HumanReport)
            return TimeSpan.FromDays(7);

        if (item.EvidenceKind.Contains("simulation", StringComparison.OrdinalIgnoreCase)
            || item.EvidenceKind.Contains("prediction", StringComparison.OrdinalIgnoreCase)
            || item.EvidenceKind.Contains("forecast", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromDays(3);
        }

        return item.SourceType switch
        {
            EvidenceSourceType.Build or EvidenceSourceType.Test or EvidenceSourceType.Runtime or EvidenceSourceType.Quality
                => TimeSpan.FromDays(7),
            EvidenceSourceType.Unity or EvidenceSourceType.Maya or EvidenceSourceType.Unreal
                => TimeSpan.FromDays(14),
            EvidenceSourceType.GitHub => TimeSpan.FromDays(7),
            EvidenceSourceType.Performance => TimeSpan.FromDays(7),
            _ => TimeSpan.FromDays(10)
        };
    }

    public static string Freshness(EvidenceRecord item, DateTimeOffset now)
    {
        var age = now - item.ObservedAtUtc;
        var fresh = FreshWindow(item);
        if (age <= fresh)
            return "Fresh";
        if (age <= TimeSpan.FromTicks(fresh.Ticks * 2))
            return "Aging";
        return "Stale";
    }

    public static EvidenceConfidence EffectiveConfidence(EvidenceRecord item, DateTimeOffset now)
    {
        var freshness = Freshness(item, now);
        var value = (int)item.Confidence;
        value -= freshness switch
        {
            "Aging" => 1,
            "Stale" => 2,
            _ => 0
        };
        return (EvidenceConfidence)Math.Clamp(
            value,
            (int)EvidenceConfidence.Unverified,
            (int)EvidenceConfidence.Verified);
    }

    public static bool IsFreshEnoughForVerification(EvidenceRecord item, DateTimeOffset now) =>
        Freshness(item, now) != "Stale"
        && EffectiveConfidence(item, now) >= EvidenceConfidence.High;
}

/// <summary>
/// Resolves the target/source revision represented by an EvidenceRecord.
///
/// Collector-derived records do not all repeat CommitSha in every payload.
/// Records produced from the same PipelineRun inherit the revision carried by
/// the revision-bound record from that run. This keeps the existing database
/// model while preventing evidence from different revisions being combined as
/// if it proved the same current state.
/// </summary>
public static class EvidenceRevisionPolicy
{
    private static readonly string[] RevisionKeys =
    [
        "commitSha",
        "targetCommit",
        "revision",
        "headSha",
        "sourceCommit",
        "dipSourceCommit"
    ];

    public static string? DeclaredRevision(EvidenceRecord item)
    {
        ArgumentNullException.ThrowIfNull(item);

        foreach (var key in RevisionKeys)
        {
            var value = Normalize(item.PayloadValue(key)) ?? Normalize(item.MetadataValue(key));
            if (value is not null)
                return value;
        }

        return null;
    }

    public static IReadOnlyDictionary<Guid, string> BuildPipelineRevisionMap(
        IEnumerable<EvidenceRecord> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        return evidence
            .Where(item => item.PipelineRunId.HasValue)
            .Select(item => new
            {
                RunId = item.PipelineRunId!.Value,
                Revision = DeclaredRevision(item),
                item.ObservedAtUtc,
                item.CreatedAtUtc
            })
            .Where(item => item.Revision is not null)
            .GroupBy(item => item.RunId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.ObservedAtUtc)
                    .ThenByDescending(item => item.CreatedAtUtc)
                    .Select(item => item.Revision!)
                    .First());
    }

    public static string? EffectiveRevision(
        EvidenceRecord item,
        IReadOnlyDictionary<Guid, string> pipelineRevisionMap)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(pipelineRevisionMap);

        var declared = DeclaredRevision(item);
        if (declared is not null)
            return declared;

        if (item.PipelineRunId is Guid runId
            && pipelineRevisionMap.TryGetValue(runId, out var inherited))
        {
            return inherited;
        }

        return null;
    }

    public static string? ResolveCurrentRevision(
        IEnumerable<EvidenceRecord> evidence,
        IReadOnlyDictionary<Guid, string> pipelineRevisionMap)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(pipelineRevisionMap);

        return evidence
            .Select(item => new
            {
                Item = item,
                Revision = EffectiveRevision(item, pipelineRevisionMap)
            })
            .Where(item => item.Revision is not null)
            .OrderByDescending(item => item.Item.ObservedAtUtc)
            .ThenByDescending(item => item.Item.CreatedAtUtc)
            .Select(item => item.Revision)
            .FirstOrDefault();
    }

    public static bool AppliesToCurrentRevision(
        EvidenceRecord item,
        string? currentRevision,
        IReadOnlyDictionary<Guid, string> pipelineRevisionMap)
    {
        if (string.IsNullOrWhiteSpace(currentRevision))
            return true;

        var revision = EffectiveRevision(item, pipelineRevisionMap);
        return revision is not null
               && string.Equals(revision, currentRevision, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().Trim('"');
        return normalized.Length == 0 ? null : normalized;
    }
}

public sealed record ResolvedEvidenceGroup(
    string SubjectKey,
    string EvidenceKind,
    EvidenceRecord Authoritative,
    IReadOnlyList<EvidenceRecord> Conflicts);

public static class EvidenceConflictPolicy
{
    public static IReadOnlyList<ResolvedEvidenceGroup> Resolve(
        IEnumerable<EvidenceRecord> evidence,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        return evidence
            .GroupBy(
                item => $"{item.SubjectKey}\u001f{item.EvidenceKind}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group
                    .OrderByDescending(item => AuthorityScore(item, now))
                    .ThenByDescending(item => item.ObservedAtUtc)
                    .ToArray();
                var authoritative = ordered[0];
                var conflicts = ordered
                    .Skip(1)
                    .Where(item => IsOpposite(authoritative.Outcome, item.Outcome))
                    .ToArray();
                return new ResolvedEvidenceGroup(
                    authoritative.SubjectKey,
                    authoritative.EvidenceKind,
                    authoritative,
                    conflicts);
            })
            .ToArray();
    }

    private static decimal AuthorityScore(EvidenceRecord item, DateTimeOffset now)
    {
        var score = (int)EvidenceFreshnessPolicy.EffectiveConfidence(item, now) * 100m;
        score += item.SourceType switch
        {
            EvidenceSourceType.Runtime => 40m,
            EvidenceSourceType.Build => 38m,
            EvidenceSourceType.Test => 37m,
            EvidenceSourceType.Performance
                when !item.EvidenceKind.Contains("simulation", StringComparison.OrdinalIgnoreCase) => 35m,
            EvidenceSourceType.Quality => 32m,
            EvidenceSourceType.Unity or EvidenceSourceType.Maya or EvidenceSourceType.Unreal => 30m,
            EvidenceSourceType.GitHub => 25m,
            EvidenceSourceType.Collector => 20m,
            EvidenceSourceType.Performance => 10m,
            EvidenceSourceType.HumanReport => 0m,
            _ => 15m
        };

        if (item.PayloadJson?.Contains("\"realDevice\":true", StringComparison.OrdinalIgnoreCase) == true)
            score += 75m;

        var ageHours = Math.Max(0, (now - item.ObservedAtUtc).TotalHours);
        score += Math.Max(0m, 25m - (decimal)Math.Min(25d, ageHours / 24d));
        return score;
    }

    private static bool IsOpposite(EvidenceOutcome a, EvidenceOutcome b) =>
        (a == EvidenceOutcome.Succeeded && b == EvidenceOutcome.Failed)
        || (a == EvidenceOutcome.Failed && b == EvidenceOutcome.Succeeded);
}
