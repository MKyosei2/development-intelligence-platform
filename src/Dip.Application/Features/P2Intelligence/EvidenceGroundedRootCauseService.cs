using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.Incidents;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.P2Intelligence;

public sealed class EvidenceGroundedRootCauseService
{
    private readonly IEvidenceRepository _evidence;
    private readonly IP2IntelligenceRepository _intelligence;
    private readonly IAiRootCauseClient _ai;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly IncidentAggregationService _incidents;

    public EvidenceGroundedRootCauseService(
        IEvidenceRepository evidence,
        IP2IntelligenceRepository intelligence,
        IAiRootCauseClient ai,
        IUnitOfWork uow,
        ISystemClock clock,
        IncidentAggregationService incidents)
    {
        _evidence=evidence;_intelligence=intelligence;_ai=ai;_uow=uow;_clock=clock;_incidents=incidents;
    }

    public async Task<RootCauseResult> AnalyzeAsync(RootCauseCommand command,CancellationToken ct)
    {
        var all=await _evidence.ListForProjectAsync(command.ProjectId,_clock.UtcNow.AddDays(-90),ct);
        var selected=all
            .Where(x=>string.Equals(x.SubjectKey,command.SubjectKey,StringComparison.OrdinalIgnoreCase)
                ||x.Outcome is EvidenceOutcome.Failed or EvidenceOutcome.Warning)
            .OrderByDescending(x=>x.ObservedAtUtc)
            .Take(Math.Clamp(command.EvidenceLimit,5,100))
            .ToArray();

        if(selected.Length==0)
        {
            var empty=new RootCauseResult("EvidenceOnly","InsufficientEvidence",[],["Collect runtime/build/test evidence for the affected subject."],[],"There is not enough objective evidence for root-cause analysis.");
            await StoreAsync(command,empty,ct);return empty;
        }

        var bundle=JsonSerializer.Serialize(selected.Select(x=>new
        {
            id=x.Id,x.SubjectKey,x.SourceType,x.EvidenceKind,x.Outcome,
            effectiveConfidence=EvidenceFreshnessPolicy.EffectiveConfidence(x,_clock.UtcNow).ToString(),
            freshness=EvidenceFreshnessPolicy.Freshness(x,_clock.UtcNow),
            x.Title,x.Summary,x.SourceReference,x.ObservedAtUtc,
            payload=x.PayloadJson
        }));

        RootCauseResult result;
        var aiResponse=await _ai.AnalyzeAsync(new(
            command.Question??$"Identify evidence-grounded root cause candidates for {command.SubjectKey}.",bundle),ct);

        if(aiResponse is not null&&TryParseAi(aiResponse,selected,out var parsed))
        {
            result=parsed with{Mode=$"ExternalAI:{aiResponse.Model}"};
        }
        else
        {
            result=Deterministic(selected);
        }

        await StoreAsync(command,result,ct);
        if(result.Candidates.Count>0)
            await _incidents.AttachRootCauseAsync(command.ProjectId,command.SubjectKey,result.Summary,result,ct);
        return result;
    }

    public async Task<IReadOnlyList<SimilarIncidentDto>> FindSimilarIncidentsAsync(
        Guid projectId,string subjectKey,string query,int take,CancellationToken ct)
    {
        var records=await _intelligence.ListRecentAsync(projectId,200,ct);
        var queryTokens=Tokens(subjectKey+" "+query);
        return records
            .Where(x=>x.Kind is P2IntelligenceKind.RootCause or P2IntelligenceKind.RegressionOrigin
                or P2IntelligenceKind.FlakyTest or P2IntelligenceKind.SoakTest or P2IntelligenceKind.PerformanceDrift)
            .Select(x=>new
            {
                Record=x,
                Similarity=Similarity(queryTokens,Tokens(x.SubjectKey+" "+x.Summary+" "+x.Kind))
            })
            .Where(x=>x.Similarity>=.15m)
            .OrderByDescending(x=>x.Similarity)
            .Take(Math.Clamp(take,1,20))
            .Select(x=>new SimilarIncidentDto(x.Record.Id,x.Record.SubjectKey,x.Record.Kind.ToString(),x.Record.Summary,
                decimal.Round(x.Similarity*100m,2),x.Record.CreatedAtUtc))
            .ToArray();
    }

    private async Task StoreAsync(RootCauseCommand command,RootCauseResult result,CancellationToken ct)
    {
        var payload=JsonSerializer.Serialize(result);
        await _intelligence.AddAsync(new P2IntelligenceRecord(
            Guid.NewGuid(),command.ProjectId,command.SubjectKey,P2IntelligenceKind.RootCause,P2IntelligenceStatus.Informational,
            result.Candidates.Count==0?null:result.Candidates.Max(x=>x.ConfidencePercent),
            result.Mode.StartsWith("ExternalAI",StringComparison.Ordinal)?EvidenceConfidence.Low:EvidenceConfidence.Medium,
            result.Summary,payload,null,_clock.UtcNow),ct);

        if(result.SuggestedActions.Count>0)
        {
            await _intelligence.AddAsync(new P2IntelligenceRecord(
                Guid.NewGuid(),command.ProjectId,command.SubjectKey,P2IntelligenceKind.SuggestedFix,P2IntelligenceStatus.Informational,
                null,EvidenceConfidence.Low,
                "Evidence-grounded suggested actions are available for human review.",
                JsonSerializer.Serialize(new{actions=result.SuggestedActions,automaticChangeApplied=false}),null,_clock.UtcNow),ct);
        }

        // AI/root-cause output is contextual only. It cannot directly contradict
        // a claim or create a release blocker in Project Truth.
        await _evidence.AddAsync(new EvidenceRecord(
            Guid.NewGuid(),command.ProjectId,null,command.SubjectKey,
            result.Mode.StartsWith("ExternalAI",StringComparison.Ordinal)?EvidenceSourceType.AiAssistant:EvidenceSourceType.Analysis,
            "root-cause.analysis",EvidenceOutcome.Informational,
            "Root-cause candidates updated",
            result.Summary,
            EvidenceConfidence.Low,null,payload,_clock.UtcNow,_clock.UtcNow),ct);
        await _uow.SaveChangesAsync(ct);
    }

    private static RootCauseResult Deterministic(IReadOnlyCollection<EvidenceRecord> evidence)
    {
        var failures=evidence
            .Where(x=>x.Outcome is EvidenceOutcome.Failed or EvidenceOutcome.Warning)
            .GroupBy(x=>new{x.EvidenceKind,x.SourceReference})
            .OrderByDescending(g=>g.Count())
            .ThenByDescending(g=>g.Max(x=>x.ObservedAtUtc))
            .Take(3)
            .ToArray();

        var candidates=failures.Select(g=>
        {
            var ids=g.Select(x=>x.Id).Take(10).ToArray();
            var representative=g.OrderByDescending(x=>x.ObservedAtUtc).First();
            var baseConfidence=representative.Confidence switch
            {
                EvidenceConfidence.Verified=>82m,EvidenceConfidence.High=>72m,EvidenceConfidence.Medium=>55m,_=>35m
            };
            var confidence=Math.Min(92m,baseConfidence+(g.Count()-1)*4m);
            var location=string.IsNullOrWhiteSpace(g.Key.SourceReference)?"the affected subsystem":g.Key.SourceReference;
            return new RootCauseCandidate(
                $"{g.Key.EvidenceKind} near {location}",confidence,
                $"{g.Count()} related warning/failure evidence item(s) point to this area. This is a candidate, not a proven cause.",ids);
        }).ToArray();

        var actions=candidates.Select(c=>$"Review {c.Title} and reproduce it against the linked evidence before changing code.").Take(3).ToArray();
        var summary=candidates.Length==0
            ?"Evidence exists, but no warning/failure cluster is strong enough to rank a root-cause candidate."
            :$"{candidates.Length} evidence-grounded root-cause candidate(s) were ranked. No code change was applied automatically.";
        return new("DeterministicEvidenceRanking",candidates.Length==0?"InsufficientEvidence":"CandidatesAvailable",candidates,actions,
            candidates.SelectMany(x=>x.EvidenceIds).Distinct().ToArray(),summary);
    }

    private static bool TryParseAi(AiRootCauseResponse response,IReadOnlyCollection<EvidenceRecord> supplied,out RootCauseResult result)
    {
        result=new("ExternalAI","InvalidResponse",[],[],[],"");
        try
        {
            using var doc=JsonDocument.Parse(response.Json);
            var allowed=supplied.Select(x=>x.Id).ToHashSet();
            var candidates=new List<RootCauseCandidate>();
            if(doc.RootElement.TryGetProperty("candidates",out var array)&&array.ValueKind==JsonValueKind.Array)
            {
                foreach(var item in array.EnumerateArray().Take(5))
                {
                    var title=item.TryGetProperty("title",out var t)?t.GetString()??"Candidate":"Candidate";
                    var confidence=item.TryGetProperty("confidencePercent",out var c)&&c.TryGetDecimal(out var cv)?Math.Clamp(cv,0m,95m):40m;
                    var rationale=item.TryGetProperty("rationale",out var r)?r.GetString()??"":"";
                    var ids=new List<Guid>();
                    if(item.TryGetProperty("evidenceIds",out var eids)&&eids.ValueKind==JsonValueKind.Array)
                        foreach(var e in eids.EnumerateArray())
                            if(Guid.TryParse(e.GetString(),out var id)&&allowed.Contains(id))ids.Add(id);
                    if(ids.Count==0)continue; // Ungrounded AI output is discarded.
                    candidates.Add(new(title,confidence,rationale,ids));
                }
            }
            var actions=new List<string>();
            if(doc.RootElement.TryGetProperty("suggestedActions",out var aa)&&aa.ValueKind==JsonValueKind.Array)
                actions.AddRange(aa.EnumerateArray().Select(x=>x.GetString()).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x!).Take(5));
            var summary=doc.RootElement.TryGetProperty("summary",out var s)?s.GetString()??"Evidence-grounded AI analysis completed.":"Evidence-grounded AI analysis completed.";
            if(candidates.Count==0)return false;
            result=new($"ExternalAI:{response.Model}","CandidatesAvailable",candidates,actions,
                candidates.SelectMany(x=>x.EvidenceIds).Distinct().ToArray(),summary);
            return true;
        }
        catch(JsonException){return false;}
    }

    private static HashSet<string> Tokens(string text)=>text
        .ToLowerInvariant()
        .Split([' ','/','\\','.',':',';','-','_','(',')','[',']'],StringSplitOptions.RemoveEmptyEntries)
        .Where(x=>x.Length>=3)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static decimal Similarity(HashSet<string> a,HashSet<string> b)
    {
        if(a.Count==0||b.Count==0)return 0m;
        var intersection=a.Count(x=>b.Contains(x));var union=a.Count+b.Count-intersection;
        return union==0?0m:(decimal)intersection/union;
    }
}
