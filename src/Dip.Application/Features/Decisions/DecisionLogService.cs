// Purpose: Coordinates Decision Log application responsibilities for decisions.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Decisions;

public sealed record LogDecisionCommand(Guid ProjectId, string DecisionType, string SubjectKey, string PreviousValue, string NewValue, string Reason, IReadOnlyList<Guid>? EvidenceIds = null, string DetailJson = "{}");
public sealed record DecisionLogDto(Guid Id, string DecisionType, string SubjectKey, string PreviousValue, string NewValue, string Reason, IReadOnlyList<Guid> EvidenceIds, string DetailJson, DateTimeOffset CreatedAtUtc);
public sealed record DecisionBriefDto(Guid ProjectId, string Status, IReadOnlyList<string> DecisionsRequired, IReadOnlyList<string> Risks, IReadOnlyList<string> RecentDecisions, IReadOnlyList<string> RecommendedActions, string Summary, DateTimeOffset GeneratedAtUtc);

public sealed class DecisionLogService
{
    private readonly IDecisionTraceRepository _traces;
     private readonly IReleaseReadinessRepository _release;
     private readonly IChangeRiskRepository _risk;
     private readonly IDevelopmentGraphRepository _graph;
     private readonly IUnitOfWork _uow;
     private readonly ISystemClock _clock;
    public DecisionLogService(IDecisionTraceRepository traces, IReleaseReadinessRepository release, IChangeRiskRepository risk, IDevelopmentGraphRepository graph, IUnitOfWork uow, ISystemClock clock) { _traces = traces;
     _release = release;
     _risk = risk;
     _graph = graph;
     _uow = uow;
     _clock = clock;
     }
    public async Task<DecisionLogDto> LogAsync(LogDecisionCommand c, CancellationToken ct)
    {
        EnsureJson(c.DetailJson);
         var ids = c.EvidenceIds ?? Array.Empty<Guid>();
         var e = new DecisionTrace(Guid.NewGuid(), c.ProjectId, c.DecisionType, c.SubjectKey, c.PreviousValue, c.NewValue, c.Reason, JsonSerializer.Serialize(ids), c.DetailJson, _clock.UtcNow);
         await _traces.AddAsync(e, ct);
         await _uow.SaveChangesAsync(ct);
         return Map(e);
    }
    public async Task<IReadOnlyList<DecisionLogDto>> ListAsync(Guid projectId, int take, CancellationToken ct) => (await _traces.ListRecentAsync(projectId, Math.Clamp(take, 1, 200), ct)).Select(Map).ToArray();
    public async Task<DecisionBriefDto> BuildBriefAsync(Guid projectId, CancellationToken ct)
    {
        var release = await _release.GetLatestAsync(projectId, ct);
         var risk = await _risk.GetLatestAsync(projectId, null, ct);
         var nodes = await _graph.ListNodesAsync(projectId, null, ct);
         var traces = await _traces.ListRecentAsync(projectId, 8, ct);
         var required = new List<string>();
         var risks = new List<string>();
         var actions = new List<string>();
        if (release is not null && release.Decision == ReleaseDecision.NoGo) { required.Add("Release decision: current evidence supports NO-GO until blockers are resolved.");
         actions.AddRange(Deserialize(release.RequiredActionsJson).Take(4));
         }
        else if (release is not null && release.Decision == ReleaseDecision.ConditionalGo) required.Add("Release decision: review CONDITIONAL GO warnings and record an explicit decision.");
        if (risk is not null && risk.RiskLevel is ChangeRiskLevel.High or ChangeRiskLevel.Critical) { risks.Add($"Latest change risk is {risk.RiskLevel} ({risk.RiskScore:0.#}/100)."); actions.Add("Complete high-risk change review and targeted QA before merge/release."); }
        var blockedMilestones = nodes.Where(x => x.Type == DevelopmentGraphNodeType.Milestone && x.Status.Equals("Blocked", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (blockedMilestones.Length > 0) { required.Add($"{blockedMilestones.Length} milestone(s) are blocked and require scope/dependency decisions.");
         risks.AddRange(blockedMilestones.Take(4).Select(x => $"Blocked milestone: {x.Name}"));
         }
        var notReady = nodes.Where(x => x.Type == DevelopmentGraphNodeType.Feature && x.Status is "NotReady" or "Blocked").ToArray();
        if (notReady.Length > 0) { risks.Add($"{notReady.Length} feature(s) are not release-ready.");
         actions.Add("Resolve required acceptance-criteria gaps or remove affected features from release scope.");
         }
        var criticalAssets = nodes.Count(x => x.Type == DevelopmentGraphNodeType.Asset && x.Status == "Critical");
        if (criticalAssets > 0) { risks.Add($"{criticalAssets} critical asset issue(s) are open.");
         actions.Add("Require asset-pipeline validation for critical assets.");
         }
        var recent = traces.Select(x => $"{x.DecisionType}: {x.SubjectKey} → {x.NewValue} ({x.Reason})").ToArray();
         var status = required.Count > 0 ? "ActionRequired" : risks.Count > 0 ? "Attention" : "Stable";
         var summary = $"{status}: {required.Count} decision(s) require attention, {risks.Count} material risk signal(s), {recent.Length} recent decision trace(s).";
         return new(projectId, status, required, risks, recent, actions.Distinct().ToArray(), summary, _clock.UtcNow);
    }
    private static DecisionLogDto Map(DecisionTrace x) => new(x.Id, x.DecisionType, x.SubjectKey, x.PreviousValue, x.NewValue, x.Reason, DeserializeGuids(x.EvidenceIdsJson), x.DetailJson, x.CreatedAtUtc);
    private static IReadOnlyList<Guid> DeserializeGuids(string json) { try { return JsonSerializer.Deserialize<Guid[]>(json) ?? []; } catch { return []; } }
    private static IReadOnlyList<string> Deserialize(string json) { try { return JsonSerializer.Deserialize<string[]>(json) ?? []; } catch { return []; } }
    private static void EnsureJson(string json) { try { using var _ = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
     }
    catch (JsonException ex) { throw new ArgumentException("DetailJson must contain valid JSON.", nameof(json), ex);
     } }
}
