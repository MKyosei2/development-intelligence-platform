// Purpose: Implements application behavior for P2 Ev ID en ce Po li CI es.
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.P2Intelligence;

/// <summary>
/// P2 freshness policy. An old observation is never silently treated as a
/// current measurement. Simulation decays faster than measured evidence.
/// </summary>
public static class EvidenceFreshnessPolicy
{
    public static TimeSpan FreshWindow(EvidenceRecord item)
    {
        if (item.SourceType == EvidenceSourceType.HumanReport) return TimeSpan.FromDays(7);
        if (item.EvidenceKind.Contains("simulation",StringComparison.OrdinalIgnoreCase)
            || item.EvidenceKind.Contains("prediction",StringComparison.OrdinalIgnoreCase)
            || item.EvidenceKind.Contains("forecast",StringComparison.OrdinalIgnoreCase))
            return TimeSpan.FromDays(3);
        return item.SourceType switch
        {
            EvidenceSourceType.Build or EvidenceSourceType.Test or EvidenceSourceType.Runtime or EvidenceSourceType.Quality => TimeSpan.FromDays(7),
            EvidenceSourceType.Unity or EvidenceSourceType.Maya or EvidenceSourceType.Unreal => TimeSpan.FromDays(14),
            EvidenceSourceType.GitHub => TimeSpan.FromDays(7),
            EvidenceSourceType.Performance => TimeSpan.FromDays(7),
            _ => TimeSpan.FromDays(10)
        };
    }

    public static string Freshness(EvidenceRecord item,DateTimeOffset now)
    {
        var age=now-item.ObservedAtUtc;
        var fresh=FreshWindow(item);
        if(age<=fresh) return "Fresh";
        if(age<=TimeSpan.FromTicks(fresh.Ticks*2)) return "Aging";
        return "Stale";
    }

    public static EvidenceConfidence EffectiveConfidence(EvidenceRecord item,DateTimeOffset now)
    {
        var freshness=Freshness(item,now);
        var value=(int)item.Confidence;
        value-=freshness switch { "Aging"=>1, "Stale"=>2, _=>0 };
        return (EvidenceConfidence)Math.Clamp(value,(int)EvidenceConfidence.Unverified,(int)EvidenceConfidence.Verified);
    }

    public static bool IsFreshEnoughForVerification(EvidenceRecord item,DateTimeOffset now)=>
        Freshness(item,now)!="Stale" && EffectiveConfidence(item,now)>=EvidenceConfidence.High;
}

public sealed record ResolvedEvidenceGroup(
    string SubjectKey,string EvidenceKind,EvidenceRecord Authoritative,IReadOnlyList<EvidenceRecord> Conflicts);

public static class EvidenceConflictPolicy
{
    public static IReadOnlyList<ResolvedEvidenceGroup> Resolve(IEnumerable<EvidenceRecord> evidence,DateTimeOffset now)
    {
        return evidence
            .GroupBy(x=>$"{x.SubjectKey}\u001f{x.EvidenceKind}",StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered=group.OrderByDescending(x=>AuthorityScore(x,now))
                    .ThenByDescending(x=>x.ObservedAtUtc).ToArray();
                var authoritative=ordered[0];
                var conflicts=ordered.Skip(1)
                    .Where(x=>IsOpposite(authoritative.Outcome,x.Outcome))
                    .ToArray();
                return new ResolvedEvidenceGroup(authoritative.SubjectKey,authoritative.EvidenceKind,authoritative,conflicts);
            })
            .ToArray();
    }

    private static decimal AuthorityScore(EvidenceRecord item,DateTimeOffset now)
    {
        var score=(int)EvidenceFreshnessPolicy.EffectiveConfidence(item,now)*100m;
        score+=item.SourceType switch
        {
            EvidenceSourceType.Runtime=>40m,
            EvidenceSourceType.Build=>38m,
            EvidenceSourceType.Test=>37m,
            EvidenceSourceType.Performance when !item.EvidenceKind.Contains("simulation",StringComparison.OrdinalIgnoreCase)=>35m,
            EvidenceSourceType.Quality=>32m,
            EvidenceSourceType.Unity or EvidenceSourceType.Maya or EvidenceSourceType.Unreal=>30m,
            EvidenceSourceType.GitHub=>25m,
            EvidenceSourceType.Collector=>20m,
            EvidenceSourceType.Performance=>10m,
            EvidenceSourceType.HumanReport=>0m,
            _=>15m
        };
        if(item.PayloadJson?.Contains("\"realDevice\":true",StringComparison.OrdinalIgnoreCase)==true) score+=75m;
        var ageHours=Math.Max(0,(now-item.ObservedAtUtc).TotalHours);
        score+=Math.Max(0m,25m-(decimal)Math.Min(25d,ageHours/24d));
        return score;
    }

    private static bool IsOpposite(EvidenceOutcome a,EvidenceOutcome b)=>
        (a==EvidenceOutcome.Succeeded&&b==EvidenceOutcome.Failed)
        ||(a==EvidenceOutcome.Failed&&b==EvidenceOutcome.Succeeded);
}
