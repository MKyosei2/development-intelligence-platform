using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Scenarios;

public sealed record WhatIfSimulationCommand(Guid ProjectId,string ScenarioName,IReadOnlyList<string> ExcludedFeatureKeys,int ScheduleBufferDays=0,decimal QaCapacityMultiplier=1m);
public sealed record WhatIfSimulationResult(string ScenarioName,decimal BaselineRisk,decimal SimulatedRisk,int BaselineOpenFeatures,int SimulatedOpenFeatures,int EstimatedScheduleDeltaDays,decimal EstimatedQaLoadChangePercent,IReadOnlyList<string> ChangedAssumptions,IReadOnlyList<string> Tradeoffs,string Summary);

public sealed class WhatIfSimulationService
{
    private readonly IDevelopmentGraphRepository _graph;private readonly IChangeRiskRepository _risk;private readonly IReleaseReadinessRepository _release;private readonly IP2IntelligenceRepository _records;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public WhatIfSimulationService(IDevelopmentGraphRepository graph,IChangeRiskRepository risk,IReleaseReadinessRepository release,IP2IntelligenceRepository records,IUnitOfWork uow,ISystemClock clock){_graph=graph;_risk=risk;_release=release;_records=records;_uow=uow;_clock=clock;}
    public async Task<WhatIfSimulationResult> SimulateAsync(WhatIfSimulationCommand c,CancellationToken ct)
    {
        var features=await _graph.ListNodesAsync(c.ProjectId,DevelopmentGraphNodeType.Feature,ct);var latestRisk=await _risk.GetLatestAsync(c.ProjectId,null,ct);var release=await _release.GetLatestAsync(c.ProjectId,ct);var baseline=Math.Clamp((latestRisk?.RiskScore??25m)+(release?.Decision==ReleaseDecision.NoGo?25m:release?.Decision==ReleaseDecision.ConditionalGo?10m:0m)+features.Count(x=>x.Status is "NotReady" or "Blocked")*5m,0m,100m);
        var excluded=features.Where(f=>c.ExcludedFeatureKeys.Any(k=>f.Key.Equals($"feature:{k}",StringComparison.OrdinalIgnoreCase)||f.Key.Equals(k,StringComparison.OrdinalIgnoreCase))).ToArray();decimal removedRisk=0m;foreach(var f in excluded){removedRisk+=ReadDecimal(f.MetadataJson,"BaseRisk",20m)*.45m;if(f.Status is "NotReady" or "Blocked")removedRisk+=8m;}
        var capacity=Math.Clamp(c.QaCapacityMultiplier,.25m,4m);var qaDelta=decimal.Round((capacity-1m)*100m,2);var scheduleDelta=-excluded.Length*2+c.ScheduleBufferDays-(int)Math.Round((double)((capacity-1m)*Math.Max(1,features.Count)/3m),MidpointRounding.AwayFromZero);var simulated=Math.Clamp(decimal.Round(baseline-removedRisk-(capacity-1m)*8m+Math.Max(0,-c.ScheduleBufferDays)*2m,2),0m,100m);var assumptions=new List<string>{$"Excluded feature(s): {excluded.Length}.",$"Schedule buffer adjustment: {c.ScheduleBufferDays:+#;-#;0} day(s).",$"QA capacity multiplier: {capacity:0.##}x."};var tradeoffs=new List<string>();if(excluded.Length>0)tradeoffs.Add("Reduced release scope lowers integration and QA exposure but removes planned product value.");if(capacity>1m)tradeoffs.Add("Higher QA capacity can reduce validation latency but may require staffing or automation cost.");if(c.ScheduleBufferDays>0)tradeoffs.Add("Additional schedule buffer reduces timing pressure but delays delivery.");if(tradeoffs.Count==0)tradeoffs.Add("Scenario changes are small; baseline risk remains the dominant signal.");var summary=$"Scenario '{c.ScenarioName}' changes modeled release risk from {baseline:0.##} to {simulated:0.##} and schedule by {scheduleDelta:+#;-#;0} day(s). This is deterministic decision support, not a delivery guarantee.";var result=new WhatIfSimulationResult(c.ScenarioName,baseline,simulated,features.Count,Math.Max(0,features.Count-excluded.Length),scheduleDelta,qaDelta,assumptions,tradeoffs,summary);
        await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),c.ProjectId,$"scenario:{c.ScenarioName}",P2IntelligenceKind.WhatIfSimulation,P2IntelligenceStatus.Informational,simulated,EvidenceConfidence.Medium,summary,JsonSerializer.Serialize(result),null,_clock.UtcNow),ct);await _uow.SaveChangesAsync(ct);return result;
    }
    private static decimal ReadDecimal(string json,string name,decimal fallback){try{using var d=JsonDocument.Parse(json);return d.RootElement.TryGetProperty(name,out var x)&&x.TryGetDecimal(out var value)?value:fallback;}catch{return fallback;}}
}
