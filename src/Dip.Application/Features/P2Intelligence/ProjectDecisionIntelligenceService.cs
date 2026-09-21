// Purpose: Builds project decision intelligence from evidence, confidence, risk, and operational context.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.P2Intelligence;

public sealed class ProjectDecisionIntelligenceService
{
    private readonly IProjectRepository _projects;
    private readonly IEvidenceRepository _evidence;
    private readonly IProjectClaimRepository _claims;
    private readonly IProjectTruthRepository _truth;
    private readonly IP2IntelligenceRepository _records;
    private readonly IDecisionTraceRepository _traces;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public ProjectDecisionIntelligenceService(
        IProjectRepository projects,IEvidenceRepository evidence,IProjectClaimRepository claims,IProjectTruthRepository truth,
        IP2IntelligenceRepository records,IDecisionTraceRepository traces,IUnitOfWork uow,ISystemClock clock)
    {
        _projects=projects;_evidence=evidence;_claims=claims;_truth=truth;_records=records;_traces=traces;_uow=uow;_clock=clock;
    }

    public async Task<IReadOnlyList<EvidenceFreshnessItem>> GetFreshnessAsync(Guid projectId,CancellationToken ct)
    {
        var evidence=await _evidence.ListForProjectAsync(projectId,_clock.UtcNow.AddDays(-90),ct);
        return evidence.OrderByDescending(x=>x.ObservedAtUtc).Take(200).Select(x=>new EvidenceFreshnessItem(
            x.Id,x.SubjectKey,x.EvidenceKind,x.ObservedAtUtc,
            Math.Max(0,(int)(_clock.UtcNow-x.ObservedAtUtc).TotalHours),
            EvidenceFreshnessPolicy.Freshness(x,_clock.UtcNow),
            EvidenceFreshnessPolicy.EffectiveConfidence(x,_clock.UtcNow).ToString())).ToArray();
    }

    public async Task<IReadOnlyList<EvidenceConflictItem>> GetConflictsAsync(Guid projectId,CancellationToken ct)
    {
        var evidence=await _evidence.ListForProjectAsync(projectId,_clock.UtcNow.AddDays(-90),ct);
        return EvidenceConflictPolicy.Resolve(evidence,_clock.UtcNow)
            .Where(x=>x.Conflicts.Count>0)
            .Select(x=>new EvidenceConflictItem(
                x.SubjectKey,x.EvidenceKind,x.Authoritative.Id,x.Conflicts.Select(c=>c.Id).ToArray(),
                "AuthorityWeighted",
                $"Selected {x.Authoritative.SourceType}/{x.Authoritative.Confidence} observed {x.Authoritative.ObservedAtUtc:u}; "
                +$"{x.Conflicts.Count} older or lower-authority opposite result(s) remain as historical context."))
            .ToArray();
    }

    public async Task<ScheduleRiskDto> GetScheduleRiskAsync(Guid projectId,CancellationToken ct)
    {
        var project=await _projects.GetByIdAsync(projectId,ct)??throw new KeyNotFoundException($"Project {projectId} not found.");
        var snapshots=await _truth.ListRecentAsync(projectId,100,ct);
        var latest=snapshots.FirstOrDefault();
        var evidence=await _evidence.ListForProjectAsync(projectId,_clock.UtcNow.AddDays(-180),ct);
        var resolvedDurations=ResolutionDurations(evidence);
        var expectedDays=resolvedDurations.Length==0?5m:decimal.Round(resolvedDurations.Average(),2);
        var critical=latest?.BlockerCount??0;
        var open=critical+(latest?.ContradictionCount??0)+(latest?.UnverifiedClaimCount??0);
        var daysRemaining=project.ReleaseDateUtc is null?30m:(decimal)Math.Max(0,(project.ReleaseDateUtc.Value-_clock.UtcNow).TotalDays);
        var workDays=Math.Max(expectedDays,1m)*Math.Max(1,critical)+Math.Max(0,open-critical)*Math.Max(1m,expectedDays*.35m);
        var schedulePressure=daysRemaining<=0?100m:Math.Clamp(workDays/daysRemaining*100m,0m,100m);
        var truthPenalty=latest is null?25m:latest.RiskLevel switch
        {
            ProjectRiskLevel.Critical=>35m,ProjectRiskLevel.High=>25m,ProjectRiskLevel.Medium=>12m,_=>0m
        };
        var risk=Math.Clamp(decimal.Round(schedulePressure*.7m+truthPenalty,2),0m,100m);
        var status=risk>=75m?"HIGH":risk>=45m?"MEDIUM":"LOW";
        var confidence=snapshots.Count>=10&&resolvedDurations.Length>=3?"HIGH":snapshots.Count>=3?"MEDIUM":"LOW";
        var dto=new ScheduleRiskDto(status,risk,confidence,project.ReleaseDateUtc,daysRemaining,expectedDays,critical,open,
            $"Estimated unresolved-work pressure is {workDays:0.#} day-equivalents versus {daysRemaining:0.#} day(s) to the configured release target. This is a risk estimate, not a release-date guarantee.");
        await StoreAsync(projectId,"project",P2IntelligenceKind.ScheduleRisk,status=="HIGH"?P2IntelligenceStatus.Warning:P2IntelligenceStatus.Informational,risk,
            confidence=="HIGH"?EvidenceConfidence.High:confidence=="MEDIUM"?EvidenceConfidence.Medium:EvidenceConfidence.Low,
            $"Schedule risk is {status} ({risk:0.#}%).",JsonSerializer.Serialize(dto),ct);
        return dto;
    }

    public async Task<CompletionForecastDto> GetCompletionForecastAsync(Guid projectId,CancellationToken ct)
    {
        var snapshots=(await _truth.ListRecentAsync(projectId,200,ct)).OrderBy(x=>x.EvaluatedAtUtc).ToArray();
        if(snapshots.Length<2)
        {
            var unknown=new CompletionForecastDto("InsufficientHistory",null,null,null,"LOW","At least two Project Truth snapshots are required for a completion forecast.");
            await StoreAsync(projectId,"project",P2IntelligenceKind.CompletionForecast,P2IntelligenceStatus.Informational,null,EvidenceConfidence.Low,unknown.Explanation,JsonSerializer.Serialize(unknown),ct);
            return unknown;
        }
        var latest=snapshots[^1];
        var horizon=snapshots.Where(x=>x.EvaluatedAtUtc>=_clock.UtcNow.AddDays(-30)).ToArray();
        if(horizon.Length<2)horizon=snapshots.TakeLast(Math.Min(20,snapshots.Length)).ToArray();
        var days=Math.Max(.25,(horizon[^1].EvaluatedAtUtc-horizon[0].EvaluatedAtUtc).TotalDays);
        var velocity=(horizon[^1].VerifiedProgress-horizon[0].VerifiedProgress)/(decimal)days;
        if(velocity<=.05m)
        {
            var stalled=new CompletionForecastDto("NoPositiveVelocity",null,null,null,"LOW","Evidence-backed progress has not shown enough positive velocity for a defensible completion-date range.");
            await StoreAsync(projectId,"project",P2IntelligenceKind.CompletionForecast,P2IntelligenceStatus.Warning,null,EvidenceConfidence.Low,stalled.Explanation,JsonSerializer.Serialize(stalled),ct);
            return stalled;
        }
        var remaining=Math.Max(0m,100m-latest.VerifiedProgress);
        var likelyDays=remaining/velocity;
        var earliest=_clock.UtcNow.AddDays((double)Math.Max(0m,likelyDays*.75m));
        var most=_clock.UtcNow.AddDays((double)Math.Max(0m,likelyDays));
        var latestDate=_clock.UtcNow.AddDays((double)Math.Max(0m,likelyDays*1.5m));
        var confidence=horizon.Length>=10&&latest.EvidenceConfidence>=EvidenceConfidence.High?"HIGH":horizon.Length>=4?"MEDIUM":"LOW";
        var dto=new CompletionForecastDto("EstimatedRange",earliest,most,latestDate,confidence,
            $"Forecast uses evidence-backed progress velocity of {velocity:0.##} percentage point(s)/day. It is deliberately a range, not a promised date.");
        await StoreAsync(projectId,"project",P2IntelligenceKind.CompletionForecast,P2IntelligenceStatus.Informational,null,
            confidence=="HIGH"?EvidenceConfidence.High:EvidenceConfidence.Medium,dto.Explanation,JsonSerializer.Serialize(dto),ct);
        return dto;
    }

    public async Task<IReadOnlyList<StagnationItem>> GetStagnationAsync(Guid projectId,CancellationToken ct)
    {
        var claims=await _claims.ListByProjectAsync(projectId,ct);
        var evidence=await _evidence.ListForProjectAsync(projectId,_clock.UtcNow.AddDays(-90),ct);
        var latestClaims=claims.GroupBy(x=>x.SubjectKey,StringComparer.OrdinalIgnoreCase)
            .Select(g=>g.OrderByDescending(x=>x.ClaimedAtUtc).First()).ToArray();
        var items=new List<StagnationItem>();
        foreach(var claim in latestClaims)
        {
            var last=evidence.Where(x=>x.SourceType!=EvidenceSourceType.HumanReport&&x.SubjectKey.Equals(claim.SubjectKey,StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x=>x.ObservedAtUtc).FirstOrDefault();
            var anchor=last?.ObservedAtUtc??claim.ClaimedAtUtc;
            var days=Math.Max(0,(int)Math.Floor((_clock.UtcNow-anchor).TotalDays));
            if(days<3)continue;
            var status=days>=7?"HIGH":days>=5?"MEDIUM":"WATCH";
            items.Add(new(claim.SubjectKey,last?.ObservedAtUtc,claim.ClaimedAtUtc,days,status,
                $"The latest report exists, but no newer objective evidence for this subject has been observed for {days} day(s). This indicates evidence stagnation, not misconduct."));
        }
        var result=items.OrderByDescending(x=>x.DaysWithoutObjectiveEvidence).Take(50).ToArray();
        await StoreAsync(projectId,"project",P2IntelligenceKind.Stagnation,result.Any(x=>x.Status=="HIGH")?P2IntelligenceStatus.Warning:P2IntelligenceStatus.Informational,
            result.Length==0?0m:Math.Min(100m,result.Max(x=>x.DaysWithoutObjectiveEvidence)*10m),EvidenceConfidence.High,
            result.Length==0?"No evidence stagnation threshold was exceeded.":$"{result.Length} subject(s) have stale objective activity relative to their latest report.",
            JsonSerializer.Serialize(result),ct);
        return result;
    }

    public async Task<IReadOnlyList<DependencyRiskItem>> AnalyzeDependenciesAsync(DependencyRiskCommand command,CancellationToken ct)
    {
        var claims=await _claims.ListByProjectAsync(command.ProjectId,ct);
        var evidence=await _evidence.ListForProjectAsync(command.ProjectId,_clock.UtcNow.AddDays(-30),ct);
        var latestClaims=claims.GroupBy(x=>x.SubjectKey,StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g=>g.Key,g=>g.OrderByDescending(x=>x.ClaimedAtUtc).First(),StringComparer.OrdinalIgnoreCase);
        var resolved=EvidenceConflictPolicy.Resolve(evidence,_clock.UtcNow).Select(x=>x.Authoritative).ToArray();
        var items=new List<DependencyRiskItem>();
        foreach(var pair in command.Dependencies)
        {
            foreach(var dependency in pair.Value)
            {
                latestClaims.TryGetValue(dependency,out var claim);
                var failed=resolved.Any(x=>x.SubjectKey.Equals(dependency,StringComparison.OrdinalIgnoreCase)
                    &&x.Outcome==EvidenceOutcome.Failed&&EvidenceFreshnessPolicy.EffectiveConfidence(x,_clock.UtcNow)>=EvidenceConfidence.High);
                var verified=claim?.VerificationStatus==ClaimVerificationStatus.Confirmed&&!failed;
                items.Add(new(pair.Key,dependency,verified?"CLEAR":"AT_RISK",
                    verified?"Dependency has a confirmed current report without authoritative failure evidence."
                    :failed?"Dependency has authoritative failure evidence.":"Dependency is not currently confirmed by objective evidence."));
            }
        }
        await StoreAsync(command.ProjectId,"project",P2IntelligenceKind.DependencyRisk,items.Any(x=>x.Status=="AT_RISK")?P2IntelligenceStatus.Warning:P2IntelligenceStatus.Passed,
            items.Count==0?0m:(decimal)items.Count(x=>x.Status=="AT_RISK")/items.Count*100m,EvidenceConfidence.High,
            $"{items.Count(x=>x.Status=="AT_RISK")} of {items.Count} declared dependency edge(s) are currently at risk.",JsonSerializer.Serialize(items),ct);
        return items;
    }

    public async Task<DailyChangeDto> GetDailyChangeAsync(Guid projectId,CancellationToken ct)
    {
        var since=_clock.UtcNow.AddHours(-24);
        var evidence=await _evidence.ListForProjectAsync(projectId,since,ct);
        var claims=(await _claims.ListByProjectAsync(projectId,ct)).Where(x=>x.ClaimedAtUtc>=since).ToArray();
        var highlights=evidence.OrderByDescending(x=>x.ObservedAtUtc)
            .Where(x=>x.Outcome is EvidenceOutcome.Failed or EvidenceOutcome.Warning or EvidenceOutcome.Changed)
            .Take(5).Select(x=>$"{x.SubjectKey}: {x.Title}").ToArray();
        var dto=new DailyChangeDto(evidence.Count,evidence.Count(x=>x.Outcome==EvidenceOutcome.Failed),
            evidence.Count(x=>x.Outcome==EvidenceOutcome.Succeeded),claims.Length,
            claims.Count(x=>x.VerificationStatus==ClaimVerificationStatus.Contradicted),highlights);
        await StoreAsync(projectId,"project",P2IntelligenceKind.DailyChange,P2IntelligenceStatus.Informational,null,EvidenceConfidence.High,
            $"Last 24h: {dto.NewEvidence} new evidence item(s), {dto.NewFailures} failure(s), {dto.ResolvedOrSucceeded} success(es).",JsonSerializer.Serialize(dto),ct);
        return dto;
    }

    public async Task<HealthChangeDto> GetHealthChangeAsync(Guid projectId,CancellationToken ct)
    {
        var snapshots=await _truth.ListRecentAsync(projectId,20,ct);
        var current=snapshots.ElementAtOrDefault(0);var previous=snapshots.ElementAtOrDefault(1);
        if(current is null)
            return new(null,null,null,"Project Truth has not been evaluated yet.",[]);
        if(previous is null)
            return new(null,current.HealthScore,null,"Only one Project Truth snapshot exists.",["Initial health established."]);
        var diff=current.HealthScore-previous.HealthScore;
        var reasons=new List<string>();
        if(current.BlockerCount!=previous.BlockerCount)reasons.Add($"Blockers: {previous.BlockerCount} → {current.BlockerCount}.");
        if(current.ContradictionCount!=previous.ContradictionCount)reasons.Add($"Report/evidence contradictions: {previous.ContradictionCount} → {current.ContradictionCount}.");
        if(current.EvidenceConfidence!=previous.EvidenceConfidence)reasons.Add($"Evidence confidence: {previous.EvidenceConfidence} → {current.EvidenceConfidence}.");
        if(current.VerifiedProgress!=previous.VerifiedProgress)reasons.Add($"Evidence-backed progress: {previous.VerifiedProgress:0.#}% → {current.VerifiedProgress:0.#}%.");
        if(reasons.Count==0)reasons.Add("The score changed through the aggregate Project Truth calculation without a categorical count change.");
        var dto=new HealthChangeDto(previous.HealthScore,current.HealthScore,diff,$"Health changed by {diff:+0.##;-0.##;0} point(s).",reasons);
        await StoreAsync(projectId,"project",P2IntelligenceKind.HealthChange,P2IntelligenceStatus.Informational,
            Math.Clamp(Math.Abs(diff),0m,100m),EvidenceConfidence.Verified,dto.Explanation,JsonSerializer.Serialize(dto),ct);
        return dto;
    }

    public async Task<DecisionBriefDto> GetDecisionBriefAsync(Guid projectId,DependencyRiskCommand? dependencyCommand,CancellationToken ct)
    {
        var schedule=await GetScheduleRiskAsync(projectId,ct);
        var forecast=await GetCompletionForecastAsync(projectId,ct);
        var stagnation=await GetStagnationAsync(projectId,ct);
        var dependencies=dependencyCommand is null?Array.Empty<DependencyRiskItem>():await AnalyzeDependenciesAsync(dependencyCommand,ct);
        var evidence=await _evidence.ListForProjectAsync(projectId,_clock.UtcNow.AddDays(-30),ct);
        var claims=await _claims.ListByProjectAsync(projectId,ct);
        var priorities=new List<DecisionPriority>();
        foreach(var group in EvidenceConflictPolicy.Resolve(evidence,_clock.UtcNow))
        {
            var item=group.Authoritative;
            var effective=EvidenceFreshnessPolicy.EffectiveConfidence(item,_clock.UtcNow);
            if(item.Outcome!=EvidenceOutcome.Failed||effective<EvidenceConfidence.High||EvidenceFreshnessPolicy.Freshness(item,_clock.UtcNow)=="Stale")continue;
            priorities.Add(new(1,item.SubjectKey,item.Title,item.Summary,"Review and resolve the linked authoritative failure evidence before relying on the affected status.",effective.ToString(),[item.Id]));
        }
        foreach(var claim in claims.GroupBy(x=>x.SubjectKey,StringComparer.OrdinalIgnoreCase).Select(g=>g.OrderByDescending(x=>x.ClaimedAtUtc).First())
            .Where(x=>x.VerificationStatus==ClaimVerificationStatus.Contradicted))
            priorities.Add(new(2,claim.SubjectKey,"Reported state conflicts with evidence","The latest status report is currently contradicted by objective evidence.","Re-check the work item and update either the implementation or the report after verifying current evidence.","High",[]));
        foreach(var s in stagnation.Where(x=>x.Status is "HIGH" or "MEDIUM"))
            priorities.Add(new(3,s.SubjectKey,"Objective activity appears stale",s.Explanation,"Request or collect fresh automated evidence for this work item.","Medium",[]));
        foreach(var d in dependencies.Where(x=>x.Status=="AT_RISK"))
            priorities.Add(new(3,d.SubjectKey,$"Dependency risk: {d.Dependency}",d.Explanation,"Resolve or verify the dependency before planning downstream completion.","High",[]));

        var ordered=priorities.OrderBy(x=>x.Priority).ThenBy(x=>x.SubjectKey).Take(10)
            .Select((x,i)=>x with{Priority=i+1}).ToArray();
        var overall=schedule.Status=="HIGH"||ordered.Any(x=>x.Priority<=2)?"ATTENTION_REQUIRED":schedule.Status=="MEDIUM"?"WATCH":"STABLE";
        var summary=ordered.Length==0
            ?"No high-priority evidence-backed action is currently identified."
            :$"{ordered.Length} priority item(s) were ranked from objective evidence. The highest-priority item is '{ordered[0].Title}'.";
        var dto=new DecisionBriefDto(projectId,overall,summary,ordered,schedule,forecast,stagnation,dependencies);
        await StoreAsync(projectId,"project",P2IntelligenceKind.DecisionAssistant,overall=="ATTENTION_REQUIRED"?P2IntelligenceStatus.Warning:P2IntelligenceStatus.Informational,
            null,EvidenceConfidence.High,summary,JsonSerializer.Serialize(dto),ct);
        return dto;
    }

    public async Task<IReadOnlyList<P2IntelligenceDto>> GetHistoricalRiskAsync(Guid projectId,CancellationToken ct)
    {
        var evidence=await _evidence.ListForProjectAsync(projectId,_clock.UtcNow.AddDays(-30),ct);
        var groups=evidence.GroupBy(x=>x.SubjectKey,StringComparer.OrdinalIgnoreCase).Select(g=>
        {
            var total=g.Count(x=>x.SourceType!=EvidenceSourceType.HumanReport);
            var failures=g.Count(x=>x.Outcome is EvidenceOutcome.Failed or EvidenceOutcome.Warning);
            var score=total==0?0m:decimal.Round((decimal)failures/total*100m,2);
            var status=score>=30m?P2IntelligenceStatus.Warning:P2IntelligenceStatus.Informational;
            return new P2IntelligenceRecord(Guid.NewGuid(),projectId,g.Key,P2IntelligenceKind.HistoricalRisk,status,score,EvidenceConfidence.High,
                $"Historical 30-day warning/failure ratio is {score:0.##}% ({failures}/{total} objective evidence item(s)).",
                JsonSerializer.Serialize(new{subject=g.Key,total,failures,ratioPercent=score}),null,_clock.UtcNow);
        }).OrderByDescending(x=>x.Score).Take(50).ToArray();
        foreach(var record in groups)await _records.AddAsync(record,ct);
        await _uow.SaveChangesAsync(ct);
        return groups.Select(x=>new P2IntelligenceDto(x.Id,x.SubjectKey,x.Kind.ToString(),x.Status.ToString(),x.Score,x.Confidence.ToString(),x.Summary,x.PayloadJson,x.SourceReference,x.CreatedAtUtc)).ToArray();
    }

    public Task<IReadOnlyList<DecisionTrace>> GetDecisionTracesAsync(Guid projectId,int take,CancellationToken ct)=>
        _traces.ListRecentAsync(projectId,take,ct);

    private async Task StoreAsync(Guid projectId,string subject,P2IntelligenceKind kind,P2IntelligenceStatus status,decimal? score,EvidenceConfidence confidence,string summary,string json,CancellationToken ct)
    {
        await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),projectId,subject,kind,status,score,confidence,summary,json,null,_clock.UtcNow),ct);
        await _uow.SaveChangesAsync(ct);
    }

    private static decimal[] ResolutionDurations(IReadOnlyCollection<EvidenceRecord> evidence)
    {
        var durations=new List<decimal>();
        foreach(var group in evidence.GroupBy(x=>$"{x.SubjectKey}\u001f{x.EvidenceKind}",StringComparer.OrdinalIgnoreCase))
        {
            var ordered=group.OrderBy(x=>x.ObservedAtUtc).ToArray();
            for(var i=0;i<ordered.Length;i++)
            {
                if(ordered[i].Outcome!=EvidenceOutcome.Failed)continue;
                var success=ordered.Skip(i+1).FirstOrDefault(x=>x.Outcome==EvidenceOutcome.Succeeded);
                if(success is not null)
                {
                    durations.Add((decimal)Math.Max(.05,(success.ObservedAtUtc-ordered[i].ObservedAtUtc).TotalDays));
                    break;
                }
            }
        }
        return durations.ToArray();
    }
}
