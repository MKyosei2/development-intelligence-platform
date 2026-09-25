// Purpose: Implements Management Analytics Service application behavior for management.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

public sealed class ManagementAnalyticsService
{
    private readonly IDevelopmentGraphRepository _repo; private readonly ISystemClock _clock;
    public ManagementAnalyticsService(IDevelopmentGraphRepository repo, ISystemClock clock) { _repo = repo; _clock = clock; }

    public async Task<CapacityOverviewDto> GetCapacityAsync(Guid projectId, int horizonWeeks, CancellationToken ct)
    {
        horizonWeeks = Math.Clamp(horizonWeeks, 1, 26); var now = StartOfWeek(_clock.UtcNow); var horizonEnd = now.AddDays(horizonWeeks * 7);
        var nodes = await _repo.ListNodesAsync(projectId, null, ct); var edges = await _repo.ListEdgesAsync(projectId, ct);
        var teams = nodes.Where(x => x.Type == DevelopmentGraphNodeType.Team).ToArray();
         var workstreams = nodes.Where(x => x.Type == DevelopmentGraphNodeType.Workstream).ToArray();
         var tasks = nodes.Where(x => x.Type == DevelopmentGraphNodeType.Task).ToArray();
        var allocationAdjustments = BuildAllocationAdjustments(nodes, now, horizonEnd);
        var teamById = teams.ToDictionary(x => x.Id); var wsById = workstreams.ToDictionary(x => x.Id);
        var wsTeam = edges.Where(x => x.Type == DevelopmentGraphEdgeType.AssignedTo && wsById.ContainsKey(x.SourceNodeId) && teamById.ContainsKey(x.TargetNodeId)).ToDictionary(x => x.SourceNodeId, x => x.TargetNodeId);
        var taskWs = edges.Where(x => x.Type == DevelopmentGraphEdgeType.BelongsTo && tasks.Any(t => t.Id == x.SourceNodeId) && wsById.ContainsKey(x.TargetNodeId)).GroupBy(x => x.SourceNodeId).ToDictionary(g => g.Key, g => g.First().TargetNodeId);
        var blockedTargets = OpenBlockerTargets(nodes, edges);
        var wsDtos = new List<WorkstreamCapacityDto>();
        foreach (var ws in workstreams)
        {
            var key = WorkManagementService.Strip(ws.Key);
             var teamId = wsTeam.GetValueOrDefault(ws.Id);
             var team = teamId != Guid.Empty && teamById.TryGetValue(teamId, out var tm) ? tm : null;
             var teamKey = team is null ? "unassigned" : WorkManagementService.Strip(team.Key);
             var teamName = team?.Name ?? "Unassigned";
            var capacityPerWeek = WorkManagementService.MetaDecimal(ws, "PlannedCapacityHoursPerWeek", 0m);
            if (capacityPerWeek <= 0m && team is not null) { var siblings = Math.Max(1, wsTeam.Values.Count(x => x == team.Id));
             var effective = EffectiveTeamWeekly(team);
             capacityPerWeek = effective / siblings;
             }
            capacityPerWeek = Math.Max(0m, capacityPerWeek + allocationAdjustments.GetValueOrDefault(key));
            var related = tasks.Where(t => taskWs.GetValueOrDefault(t.Id) == ws.Id && !WorkManagementService.IsDone(t.Status) && Overlaps(t, now, horizonEnd)).ToArray();
             var planned = related.Sum(t => WorkManagementService.MetaDecimal(t, "EstimateHours"));
             var remaining = related.Sum(t => WorkManagementService.MetaDecimal(t, "RemainingHours", WorkManagementService.MetaDecimal(t, "EstimateHours")));
            var weeklyCaps = team is null ? Enumerable.Repeat(capacityPerWeek, horizonWeeks).ToArray() : ApplyCalendarCapacity(nodes, teamKey, capacityPerWeek, Math.Max(0.1m, EffectiveTeamWeekly(team)), now, horizonWeeks); var cap = weeklyCaps.Sum();
            var weekly = BuildBuckets(related, weeklyCaps, blockedTargets, now, horizonWeeks);
             var util = cap <= 0m ? (remaining > 0m ? 999m : 0m) : remaining / cap * 100m;
             var blocked = related.Count(t => blockedTargets.Contains(t.Id) || WorkManagementService.IsBlocked(t.Status));
            wsDtos.Add(new(key, ws.Name, teamKey, teamName, cap, planned, remaining, Round(util), related.Length, blocked, CapacityStatus(util, blocked), weekly));
        }
        var teamDtos = new List<TeamCapacityDto>();
        foreach (var team in teams)
        {
            var relatedWs = workstreams.Where(w => wsTeam.GetValueOrDefault(w.Id) == team.Id).ToArray();
             var relatedIds = relatedWs.Select(x => x.Id).ToHashSet();
             var relatedTasks = tasks.Where(t => relatedIds.Contains(taskWs.GetValueOrDefault(t.Id)) && !WorkManagementService.IsDone(t.Status) && Overlaps(t, now, horizonEnd)).ToArray();
             var weeklyCap = Math.Max(0m, EffectiveTeamWeekly(team) + relatedWs.Sum(w => allocationAdjustments.GetValueOrDefault(WorkManagementService.Strip(w.Key))));
             var teamKey = WorkManagementService.Strip(team.Key);
             var weeklyCaps = ApplyCalendarCapacity(nodes, teamKey, weeklyCap, Math.Max(0.1m, EffectiveTeamWeekly(team)), now, horizonWeeks);
             var cap = weeklyCaps.Sum();
             var planned = relatedTasks.Sum(t => WorkManagementService.MetaDecimal(t, "EstimateHours"));
             var remaining = relatedTasks.Sum(t => WorkManagementService.MetaDecimal(t, "RemainingHours", WorkManagementService.MetaDecimal(t, "EstimateHours")));
             var util = cap <= 0m ? (remaining > 0m ? 999m : 0m) : remaining / cap * 100m;
             var blocked = relatedTasks.Count(t => blockedTargets.Contains(t.Id) || WorkManagementService.IsBlocked(t.Status));
             var weekly = BuildBuckets(relatedTasks, weeklyCaps, blockedTargets, now, horizonWeeks);
            var sharedKey = NullIfEmpty(WorkManagementService.MetaString(team, "SharedResourceKey")); var sharedPool = WorkManagementService.MetaDecimal(team, "SharedPoolCapacityHoursPerWeek");
            teamDtos.Add(new(WorkManagementService.Strip(team.Key), team.Name, cap, planned, remaining, Round(util), relatedWs.Length, relatedTasks.Length, blocked, CapacityStatus(util, blocked), weekly, sharedKey, sharedPool > 0m ? sharedPool : null));
        }
        var bottlenecks = teamDtos.Where(x => x.Status is "OverCapacity" or "Blocked").Select(x => $"Team '{x.Name}' is {x.UtilizationPercent:0.#}% utilized with {x.BlockedTasks} blocked task(s).").Concat(wsDtos.Where(x => x.Status is "OverCapacity" or "Blocked").Select(x => $"Workstream '{x.Name}' is {x.UtilizationPercent:0.#}% utilized with {x.BlockedTasks} blocked task(s).")).Distinct().Take(20).ToArray();
        return new(projectId, horizonWeeks, teamDtos.OrderByDescending(x => x.UtilizationPercent).ToArray(), wsDtos.OrderByDescending(x => x.UtilizationPercent).ToArray(), bottlenecks, _clock.UtcNow);
    }

    public async Task<ManagementScheduleDto> GetScheduleAsync(Guid projectId, CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, null, ct);
         var allEdges = await _repo.ListEdgesAsync(projectId, ct);
         var scheduleNodes = nodes.Where(x => x.Type is DevelopmentGraphNodeType.Task or DevelopmentGraphNodeType.Milestone).ToArray();
         var ids = scheduleNodes.Select(x => x.Id).ToHashSet();
         var deps = allEdges.Where(e => ids.Contains(e.SourceNodeId) && ids.Contains(e.TargetNodeId) && e.Type is DevelopmentGraphEdgeType.Blocks or DevelopmentGraphEdgeType.DependsOn or DevelopmentGraphEdgeType.ScheduledFor).ToArray();
         var blockerTargets = OpenBlockerTargets(nodes, allEdges);
        if (scheduleNodes.Length == 0) return new(projectId, "NoSchedule", null, null, 0, 0, 0, [], [], ["No tasks or milestones are registered."], _clock.UtcNow);
        var indegree = scheduleNodes.ToDictionary(x => x.Id, _ => 0);
        foreach (var e in deps) indegree[e.TargetNodeId]++;
         var q = new Queue<Guid>(indegree.Where(x => x.Value == 0).Select(x => x.Key));
         var order = new List<Guid>();
        while (q.Count > 0) { var id = q.Dequeue();
         order.Add(id);
        foreach (var e in deps.Where(x => x.SourceNodeId == id)) { if (--indegree[e.TargetNodeId] == 0) q.Enqueue(e.TargetNodeId);
         } }
        if (order.Count != scheduleNodes.Length) { var cyclic = indegree.Where(x => x.Value > 0).Select(x => scheduleNodes.First(n => n.Id == x.Key).Name).ToArray(); return new(projectId, "CycleDetected", null, null, 0, 0, cyclic.Length, [], [], [$"Schedule dependency cycle detected: {string.Join(", ", cyclic.Take(10))}"], _clock.UtcNow); }
        var models = scheduleNodes.ToDictionary(x => x.Id, ParseSchedule);
         var startDay = scheduleNodes.Min(n => models[n.Id].Start).UtcDateTime.Date;
         var es = scheduleNodes.ToDictionary(x => x.Id, _ => 0);
         var ef = scheduleNodes.ToDictionary(x => x.Id, _ => 0);
        foreach (var id in order) { var m = models[id];
         var plannedOffset = Math.Max(0, (int)Math.Floor((m.Start.UtcDateTime.Date - startDay).TotalDays));
         var predFinish = deps.Where(x => x.TargetNodeId == id).Select(x => ef[x.SourceNodeId]).DefaultIfEmpty(0).Max();
         es[id] = Math.Max(plannedOffset, predFinish);
         ef[id] = es[id] + m.DurationDays;
         }
        var total = ef.Values.Max();
         var lf = scheduleNodes.ToDictionary(x => x.Id, _ => total);
         var ls = scheduleNodes.ToDictionary(x => x.Id, _ => 0);
        foreach (var id in order.AsEnumerable().Reverse()) { var succ = deps.Where(x => x.SourceNodeId == id).Select(x => x.TargetNodeId).ToArray();
         lf[id] = succ.Length == 0 ? total : succ.Min(x => ls[x]);
         ls[id] = lf[id] - models[id].DurationDays;
         }
        var items = new List<ManagementScheduleItem>(); var warnings = new List<string>(); var now = _clock.UtcNow;
        foreach (var id in order) { var m = models[id];
         var slack = Math.Max(0, ls[id] - es[id]);
         var forecast = new DateTimeOffset(startDay, TimeSpan.Zero).AddDays(ef[id]);
         var blockedBy = allEdges.Count(e => e.TargetNodeId == id && ((e.SourceNodeId != Guid.Empty && blockerTargets.Contains(id) && nodes.Any(n => n.Id == e.SourceNodeId && n.Type == DevelopmentGraphNodeType.Blocker)) || (ids.Contains(e.SourceNodeId) && e.Type == DevelopmentGraphEdgeType.Blocks && !WorkManagementService.IsDone(nodes.First(n => n.Id == e.SourceNodeId).Status))));
         var status = m.Node.Status;
        if (blockerTargets.Contains(id) && !WorkManagementService.IsDone(status)) status = "Blocked";
         var key = WorkManagementService.Strip(m.Node.Key);
         items.Add(new(id, m.Node.Type.ToString(), key, m.Node.Name, m.WorkstreamKey, m.Start, m.Due, forecast, m.DurationDays, m.Completion, status, slack, slack == 0, blockedBy));
        if (m.Due < now && m.Completion < 100m) warnings.Add($"{m.Node.Type} '{m.Node.Name}' is overdue.");
         }
        var plannedFinish = models.Values.Max(x => x.Due);
         var forecastFinish = items.Max(x => x.ForecastFinishUtc);
         var delay = Math.Max(0, (int)Math.Ceiling((forecastFinish - plannedFinish).TotalDays));
         var critical = items.Where(x => x.IsCritical).Select(x => x.Key).ToArray();
         var atRisk = items.Count(x => x.Status.Equals("Blocked", StringComparison.OrdinalIgnoreCase) || (x.DueAtUtc < now.AddDays(Math.Max(2, x.SlackDays + 1)) && x.CompletionPercent < 100m));
         var statusOverall = items.Any(x => x.Status.Equals("Blocked", StringComparison.OrdinalIgnoreCase)) ? "Blocked" : delay > 0 || atRisk > 0 ? "AtRisk" : "OnTrack";
        return new(projectId, statusOverall, plannedFinish, forecastFinish, delay, critical.Length, atRisk, items, critical, warnings.Distinct().Take(30).ToArray(), _clock.UtcNow);
    }

    public async Task<BlockerOverviewDto> GetBlockersAsync(Guid projectId, CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, null, ct);
         var edges = await _repo.ListEdgesAsync(projectId, ct);
         var byId = nodes.ToDictionary(x => x.Id);
         var blockers = nodes.Where(x => x.Type == DevelopmentGraphNodeType.Blocker).ToArray();
         var list = new List<BlockerImpactDto>();
         var now = _clock.UtcNow;
        foreach (var b in blockers)
        {
            var opened = WorkManagementService.MetaDate(b, "OpenedAtUtc", b.FirstObservedAtUtc);
             var direct = edges.Where(x => x.SourceNodeId == b.Id && x.Type == DevelopmentGraphEdgeType.Blocks).Select(x => x.TargetNodeId).Distinct().ToArray();
             var downstream = TraverseDownstream(direct, edges, byId);
             var severity = WorkManagementService.MetaString(b, "Severity", "Medium");
             var age = Math.Max(0, (int)Math.Floor((now - opened).TotalDays));
             var reason = WorkManagementService.MetaString(b, "Reason", "");
             var open = !WorkManagementService.IsDone(b.Status);
             var action = !open ? "No action required; blocker is resolved." : severity.Equals("Critical", StringComparison.OrdinalIgnoreCase) || age >= 5 ?
             $"Escalate now: {direct.Length} direct item(s), {downstream.Count} downstream item(s), open {age} day(s)." :
             "Assign owner and due date; review in the next management cycle.";
            list.Add(new(WorkManagementService.Strip(b.Key), b.Name, reason, severity, b.Status, age, direct.Length, downstream.Count, direct.Where(byId.ContainsKey).Select(x => byId[x].Name).ToArray(), downstream.Where(byId.ContainsKey).Select(x => byId[x].Name).Take(50).ToArray(), action));
        }
        var openItems = list.Where(x => !WorkManagementService.IsDone(x.Status)).ToArray(); return new(projectId, openItems.Length, openItems.Count(x => x.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase)), openItems.Count(x => x.AgeDays >= 5), list.OrderByDescending(x => !WorkManagementService.IsDone(x.Status)).ThenByDescending(x => SeverityRank(x.Severity)).ThenByDescending(x => x.AgeDays).ToArray(), now);
    }

    public async Task<DependencyOverviewDto> GetDependenciesAsync(Guid projectId, CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, null, ct);
         var edges = await _repo.ListEdgesAsync(projectId, ct);
         var managed = nodes.Where(x => x.Type is DevelopmentGraphNodeType.Task or DevelopmentGraphNodeType.Workstream or DevelopmentGraphNodeType.Milestone or DevelopmentGraphNodeType.Requirement).ToDictionary(x => x.Id);
         var wsOf = BuildWorkstreamMap(nodes, edges);
         var links = new List<DependencyLinkDto>();
        foreach (var e in edges.Where(x => managed.ContainsKey(x.SourceNodeId) && managed.ContainsKey(x.TargetNodeId) && x.Type is DevelopmentGraphEdgeType.Blocks or DevelopmentGraphEdgeType.DependsOn or DevelopmentGraphEdgeType.ScheduledFor or DevelopmentGraphEdgeType.Satisfies))
        { var s = managed[e.SourceNodeId];
         var t = managed[e.TargetNodeId];
         var sw = wsOf.GetValueOrDefault(s.Id);
         var tw = wsOf.GetValueOrDefault(t.Id);
         var cross = sw != Guid.Empty && tw != Guid.Empty && sw != tw;
         links.Add(new(s.Type.ToString(), WorkManagementService.Strip(s.Key), s.Name, t.Type.ToString(), WorkManagementService.Strip(t.Key), t.Name, e.Type.ToString(), e.Critical, cross));
         }
        var dependencyEdges = edges.Where(x => managed.ContainsKey(x.SourceNodeId) && managed.ContainsKey(x.TargetNodeId) && x.Type is DevelopmentGraphEdgeType.Blocks or DevelopmentGraphEdgeType.DependsOn).ToArray();
         var cycle = FindCycle(managed.Keys, dependencyEdges);
         return new(projectId, links.Count, links.Count(x => x.Critical), links.Count(x => x.CrossWorkstream), cycle.Count > 0, cycle.Where(managed.ContainsKey).Select(x => managed[x].Name).ToArray(), links, _clock.UtcNow);
    }

    public async Task<ScopeOverviewDto> GetScopeAsync(Guid projectId, CancellationToken ct)
    {
        var requirements = await _repo.ListNodesAsync(projectId, DevelopmentGraphNodeType.Requirement, ct);
         var baselines = await _repo.ListNodesAsync(projectId, DevelopmentGraphNodeType.ScopeBaseline, ct);
         var latest = baselines.OrderByDescending(x => x.LastObservedAtUtc).FirstOrDefault();
         var baselineCount = latest is null ? requirements.Count(WorkManagementService.IsInCurrentScope) : (int)WorkManagementService.MetaDecimal(latest, "BaselineRequirements", 0m);
         var current = requirements.Count(WorkManagementService.IsInCurrentScope);
         var added = Math.Max(0, current - baselineCount);
         var deferred = requirements.Count(x => !WorkManagementService.IsInCurrentScope(x));
         var growth = baselineCount <= 0 ? (current > 0 ? 100m : 0m) : (current - baselineCount) / (decimal)baselineCount * 100m;
         var dtos = requirements.Select(r => new RequirementDto(WorkManagementService.Strip(r.Key), r.Name, WorkManagementService.MetaString(r, "Priority", "Medium"), r.Status, WorkManagementService.MetaString(r, "ScopeState", "Included"), WorkManagementService.MetaDecimal(r, "CompletionPercent"), NullIfEmpty(WorkManagementService.MetaString(r, "AcceptanceSummary", "")), r.ExternalReference)).OrderByDescending(x => PriorityRank(x.Priority)).ToArray();
         var highIncomplete = dtos.Count(x => (x.Priority is "Critical" or "High") && x.CompletionPercent < 100m && !WorkManagementService.IsDone(x.Status) && !x.ScopeState.Equals("Deferred", StringComparison.OrdinalIgnoreCase));
         var warnings = new List<string>();
        if (growth > 10m) warnings.Add($"Scope has grown {growth:0.#}% from the latest baseline.");
        if (highIncomplete > 0) warnings.Add($"{highIncomplete} high-priority in-scope requirement(s) are incomplete.");
         return new(projectId, latest is null ? null : WorkManagementService.Strip(latest.Key), baselineCount, current, added, deferred, Round(growth), highIncomplete, dtos, warnings, _clock.UtcNow);
    }

    public async Task<ManagementMilestoneOverviewDto> GetMilestonesAsync(Guid projectId, CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, null, ct);
         var edges = await _repo.ListEdgesAsync(projectId, ct);
         var milestones = nodes.Where(x => x.Type == DevelopmentGraphNodeType.Milestone).ToArray();
         var tasks = nodes.Where(x => x.Type == DevelopmentGraphNodeType.Task).ToDictionary(x => x.Id);
         var blockers = OpenBlockerTargets(nodes, edges);
         var now = _clock.UtcNow;
         var result = new List<ManagementMilestoneDto>();
        foreach (var m in milestones) { var taskIds = edges.Where(e => e.TargetNodeId == m.Id && e.Type == DevelopmentGraphEdgeType.ScheduledFor && tasks.ContainsKey(e.SourceNodeId)).Select(e => e.SourceNodeId).Distinct().ToArray();
         var linked = taskIds.Select(id => tasks[id]).ToArray();
         var linkedCompletion = linked.Length == 0 ? WorkManagementService.MetaDecimal(m, "CompletionPercent") : linked.Average(t => WorkManagementService.MetaDecimal(t, "CompletionPercent"));
         var blocked = linked.Count(t => blockers.Contains(t.Id) || WorkManagementService.IsBlocked(t.Status));
         var start = WorkManagementService.MetaDate(m, "PlannedStartUtc", m.FirstObservedAtUtc);
         var due = WorkManagementService.MetaDate(m, "DueAtUtc", DateTimeOffset.MaxValue);
         var recorded = WorkManagementService.MetaDecimal(m, "CompletionPercent");
         var past = due < now && Math.Max(recorded, linkedCompletion) < 100m;
         var readiness = WorkManagementService.IsDone(m.Status) || Math.Max(recorded, linkedCompletion) >= 100m ? "Completed" : WorkManagementService.IsBlocked(m.Status) || blockers.Contains(m.Id) || blocked > 0 ? "Blocked" : past || due < now.AddDays(7) && Math.Max(recorded, linkedCompletion) < 80m ? "AtRisk" : "OnTrack";
         result.Add(new(WorkManagementService.Strip(m.Key), m.Name, m.Status, start, due, Round(recorded), Round(linkedCompletion), linked.Length, blocked, past, readiness));
         }
        return new(projectId, result.Count, result.Count(x => x.Readiness == "AtRisk"), result.Count(x => x.Readiness == "Blocked"), result.Count(x => x.Readiness == "Completed"), result.OrderBy(x => x.DueAtUtc).ToArray(), now);
    }

    public async Task<RiskRegisterDto> GetRisksAsync(Guid projectId, CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, null, ct);
         var edges = await _repo.ListEdgesAsync(projectId, ct);
         var byId = nodes.ToDictionary(x => x.Id);
         var now = _clock.UtcNow;
         var list = new List<RiskRegisterItemDto>();
        foreach (var risk in nodes.Where(x => x.Type == DevelopmentGraphNodeType.Risk))
        {
            var probability = Math.Clamp(WorkManagementService.MetaDecimal(risk, "ProbabilityPercent"), 0m, 100m);
             var impact = Math.Clamp(WorkManagementService.MetaDecimal(risk, "ImpactPercent"), 0m, 100m);
             var exposure = Round(probability * impact / 100m);
             var level = ExposureLevel(exposure);
             var review = WorkManagementService.MetaDate(risk, "ReviewDueUtc", DateTimeOffset.MaxValue);
             var due = review == DateTimeOffset.MaxValue ? (DateTimeOffset?)null : review;
             var overdue = due.HasValue && due.Value < now && !WorkManagementService.IsDone(risk.Status);
            var affected = edges.Where(e => e.SourceNodeId == risk.Id && e.Type == DevelopmentGraphEdgeType.Affects && byId.ContainsKey(e.TargetNodeId)).Select(e => $"{byId[e.TargetNodeId].Type}:{WorkManagementService.Strip(byId[e.TargetNodeId].Key)}").Distinct().Take(50).ToArray();
            var action = WorkManagementService.IsDone(risk.Status) ? "No action required; risk is closed." : overdue ?
            $"Risk review is overdue. Review mitigation and owner now; current exposure is {exposure:0.#}/100." : level is "Critical" or "High" ?
            $"Mitigation decision required. Exposure is {exposure:0.#}/100 and affects {affected.Length} linked item(s)." : "Monitor trigger and review on the planned cadence.";
            list.Add(new(WorkManagementService.Strip(risk.Key), risk.Name, WorkManagementService.MetaString(risk, "Description"), risk.Status, WorkManagementService.MetaString(risk, "Category", "Delivery"), probability, impact, exposure, level, NullIfEmpty(WorkManagementService.MetaString(risk, "OwnerRole")), due, overdue, NullIfEmpty(WorkManagementService.MetaString(risk, "Trigger")), NullIfEmpty(WorkManagementService.MetaString(risk, "Mitigation")), affected, action));
        }
        var open = list.Where(x => !WorkManagementService.IsDone(x.Status)).ToArray(); return new(projectId, open.Length, open.Count(x => x.ExposureLevel == "Critical"), open.Count(x => x.ReviewOverdue), Round(open.Sum(x => x.ExposureScore)), list.OrderByDescending(x => !WorkManagementService.IsDone(x.Status)).ThenByDescending(x => x.ExposureScore).ToArray(), now);
    }

    public async Task<DecisionActionOverviewDto> GetDecisionsAsync(Guid projectId, CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, null, ct);
         var edges = await _repo.ListEdgesAsync(projectId, ct);
         var byId = nodes.ToDictionary(x => x.Id);
         var decisions = nodes.Where(x => x.Type == DevelopmentGraphNodeType.ManagementDecision).ToArray();
         var actions = nodes.Where(x => x.Type == DevelopmentGraphNodeType.ActionItem).ToArray();
         var now = _clock.UtcNow;
        var actionDtos = actions.Select(a =>
        {
            var dueRaw = WorkManagementService.MetaDate(a, "DueAtUtc", DateTimeOffset.MaxValue);
             DateTimeOffset? due = dueRaw == DateTimeOffset.MaxValue ? null : dueRaw;
             var overdue = due.HasValue && due.Value < now && !WorkManagementService.IsDone(a.Status);
             var decisionKey = NullIfEmpty(WorkManagementService.MetaString(a, "DecisionKey"));
             var target = edges.Where(e => e.SourceNodeId == a.Id && e.Type is DevelopmentGraphEdgeType.RelatesTo or DevelopmentGraphEdgeType.Mitigates).Select(e => byId.TryGetValue(e.TargetNodeId, out var t) ? $"{t.Type}:{t.Name}" : null).FirstOrDefault(x => x is not null);
             var targetRef = ReadManagementReference(a, "Target");
            return new ManagementActionDto(WorkManagementService.Strip(a.Key), a.Name, a.Status, WorkManagementService.MetaString(a, "Priority", "Medium"), NullIfEmpty(WorkManagementService.MetaString(a, "OwnerRole")), due, overdue, Round(WorkManagementService.MetaDecimal(a, "CompletionPercent")), decisionKey, target, targetRef?.Type, targetRef?.Key);
        }).OrderByDescending(x => x.Overdue).ThenByDescending(x => PriorityRank(x.Priority)).ThenBy(x => x.DueAtUtc ?? DateTimeOffset.MaxValue).ToArray();
        var decisionDtos = decisions.Select(d =>
        {
            var dueRaw = WorkManagementService.MetaDate(d, "DecisionDueUtc", DateTimeOffset.MaxValue);
             DateTimeOffset? due = dueRaw == DateTimeOffset.MaxValue ? null : dueRaw;
             var decidedRaw = WorkManagementService.MetaDate(d, "DecidedAtUtc", DateTimeOffset.MaxValue);
             DateTimeOffset? decided = decidedRaw == DateTimeOffset.MaxValue ? null : decidedRaw;
             var related = edges.Where(e => e.SourceNodeId == d.Id && e.Type == DevelopmentGraphEdgeType.RelatesTo && byId.ContainsKey(e.TargetNodeId)).Select(e => $"{byId[e.TargetNodeId].Type}:{WorkManagementService.Strip(byId[e.TargetNodeId].Key)}").Distinct().Take(30).ToArray();
             var openActions = edges.Where(e => e.SourceNodeId == d.Id && e.Type == DevelopmentGraphEdgeType.ResultsIn && byId.TryGetValue(e.TargetNodeId, out var t) && t.Type == DevelopmentGraphNodeType.ActionItem && !WorkManagementService.IsDone(t.Status)).Count();
            return new ManagementDecisionDto(WorkManagementService.Strip(d.Key), d.Name, WorkManagementService.MetaString(d, "Decision"), WorkManagementService.MetaString(d, "Rationale"), d.Status, NullIfEmpty(WorkManagementService.MetaString(d, "DecisionOwnerRole")), due, decided, openActions, related);
        }).OrderByDescending(x => !WorkManagementService.IsDone(x.Status)).ThenBy(x => x.DecisionDueUtc ?? DateTimeOffset.MaxValue).ToArray();
        var pending = decisionDtos.Count(x => !WorkManagementService.IsDone(x.Status) && !x.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase));
         var openActions = actionDtos.Count(x => !WorkManagementService.IsDone(x.Status));
         var overdue = actionDtos.Count(x => x.Overdue);
         return new(projectId, pending, openActions, overdue, decisionDtos, actionDtos, now);
    }

    public async Task<ResourceAllocationOverviewDto> GetResourceAllocationsAsync(Guid projectId, int horizonWeeks, CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, DevelopmentGraphNodeType.ResourceAllocation, ct); var allocations = nodes.Select(a =>
        {
            var start = WorkManagementService.MetaDate(a, "StartUtc", a.FirstObservedAtUtc);
             var end = WorkManagementService.MetaDate(a, "EndUtc", a.LastObservedAtUtc);
             return new ResourceAllocationDto(WorkManagementService.Strip(a.Key), a.Status, NullIfEmpty(WorkManagementService.MetaString(a, "FromWorkstreamKey")), WorkManagementService.MetaString(a, "ToWorkstreamKey"), Round(WorkManagementService.MetaDecimal(a, "CapacityHoursPerWeek")), start, end, NullIfEmpty(WorkManagementService.MetaString(a, "Rationale")), NullIfEmpty(WorkManagementService.MetaString(a, "DecisionKey")));
        }).OrderByDescending(x => !WorkManagementService.IsDone(x.Status)).ThenBy(x => x.StartUtc).ToArray();
        var capacity = await GetCapacityAsync(projectId, horizonWeeks, ct); var recs = new List<ResourceAllocationRecommendationDto>(); foreach (var ws in capacity.Workstreams.Where(x => x.UtilizationPercent > 100m || x.Status == "Blocked"))
        {
            var shortage = ws.Weekly.Count == 0 ? 0m : ws.Weekly.Max(x => Math.Max(0m, x.PlannedHours - x.CapacityHours)); if (shortage <= 0m && ws.UtilizationPercent > 100m) shortage = Math.Max(1m, ws.RemainingHours / Math.Max(1, horizonWeeks) * (ws.UtilizationPercent - 100m) / Math.Max(1m, ws.UtilizationPercent));
            recs.Add(new(ws.Key, ws.Name, ws.TeamName, ws.UtilizationPercent, Round(shortage), ws.Status == "Blocked" ? "Workstream is blocked while carrying active demand; remove the blocker before adding capacity, then reassess." : $"Demand exceeds planned capacity. Consider adding approximately {Round(shortage):0.#} h/week or reducing scope."));
        }
        return new(projectId, allocations, recs.OrderByDescending(x => x.CurrentUtilizationPercent).Take(20).ToArray(), _clock.UtcNow);
    }

    public async Task<ScopeChangeOverviewDto> GetScopeChangesAsync(Guid projectId, CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, DevelopmentGraphNodeType.ChangeRequest, ct); var list = nodes.Select(c =>
        {
            var requested = WorkManagementService.MetaDate(c, "RequestedAtUtc", c.FirstObservedAtUtc);
             var e = WorkManagementService.MetaDecimal(c, "EngineeringHoursDelta");
             var a = WorkManagementService.MetaDecimal(c, "ArtHoursDelta");
             var q = WorkManagementService.MetaDecimal(c, "QaHoursDelta");
             var days = (int)WorkManagementService.MetaDecimal(c, "ScheduleDaysDelta");
             var risk = WorkManagementService.MetaDecimal(c, "RiskDeltaPercent");
             var impact = $"Engineering {Signed(e)}h, Art {Signed(a)}h, QA {Signed(q)}h, schedule {Signed(days)} day(s), risk {Signed(risk)}%.";
            return new ChangeRequestDto(WorkManagementService.Strip(c.Key), c.Name, WorkManagementService.MetaString(c, "Description"), c.Status, WorkManagementService.MetaString(c, "Priority", "Medium"), NullIfEmpty(WorkManagementService.MetaString(c, "RequestedByRole")), requested, e, a, q, days, risk, WorkManagementService.MetaStringArray(c, "RequirementKeys"), WorkManagementService.MetaStringArray(c, "WorkstreamKeys"), NullIfEmpty(WorkManagementService.MetaString(c, "DecisionKey")), impact);
        }).OrderByDescending(x => !IsFinalChange(x.Status)).ThenByDescending(x => PriorityRank(x.Priority)).ThenByDescending(x => x.RequestedAtUtc).ToArray(); var pending = list.Where(x => !IsFinalChange(x.Status) && !x.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase)).ToArray();
        return new(projectId, pending.Length, list.Count(x => x.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase)), list.Count(x => x.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase)), Round(pending.Sum(x => x.EngineeringHoursDelta)), Round(pending.Sum(x => x.ArtHoursDelta)), Round(pending.Sum(x => x.QaHoursDelta)), pending.Sum(x => x.ScheduleDaysDelta), Round(pending.Sum(x => x.RiskDeltaPercent)), list, _clock.UtcNow);
    }

    public async Task<EscalationOverviewDto> GetAlertsAsync(Guid projectId, int horizonWeeks, CancellationToken ct)
    {
        var now = _clock.UtcNow;
         var alerts = new List<ManagementAlertDto>();
         var blockers = await GetBlockersAsync(projectId, ct);
         var capacity = await GetCapacityAsync(projectId, horizonWeeks, ct);
         var risks = await GetRisksAsync(projectId, ct);
         var decisions = await GetDecisionsAsync(projectId, ct);
         var schedule = await GetScheduleAsync(projectId, ct);
         var changes = await GetScopeChangesAsync(projectId, ct);
        foreach (var b in blockers.Items.Where(x => !WorkManagementService.IsDone(x.Status) && (x.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase) || x.AgeDays >= 5))) alerts.Add(new($"blocker:{b.Key}", b.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase) ? "Critical" : "High", "Blocker", b.Title, $"Open {b.AgeDays} day(s); {b.DirectlyBlocked} direct and {b.DownstreamAffected} downstream item(s) affected.", b.ManagementAction, now, null, b.Key));
        foreach (var w in capacity.Workstreams.Where(x => x.UtilizationPercent > 110m)) alerts.Add(new($"capacity:{w.Key}", w.UtilizationPercent > 125m ? "Critical" : "High", "Capacity", $"{w.Name} is over capacity", $"Planned utilization is {w.UtilizationPercent:0.#}% across the selected horizon.", "Reallocate capacity, reduce scope, or move dates. Validate skill/ownership fit before moving people.", now, null, w.Key));
        foreach (var r in risks.Items.Where(x => !WorkManagementService.IsDone(x.Status) && (x.ExposureLevel is "Critical" or "High" || x.ReviewOverdue))) alerts.Add(new($"risk:{r.Key}", r.ExposureLevel == "Critical" ? "Critical" : "High", "Risk", r.Title, $"Exposure {r.ExposureScore:0.#}/100" + (r.ReviewOverdue ? "; review is overdue." : "."), r.ManagementAction, now, r.ReviewDueUtc, r.Key));
        foreach (var a in decisions.Actions.Where(x => x.Overdue)) alerts.Add(new($"action:{a.Key}", a.Priority == "Critical" ? "Critical" : "High", "Action", a.Title, $"Action is overdue; status is {a.Status} and completion is {a.CompletionPercent:0.#}%.", "Escalate to the owning role, confirm the due date, or explicitly re-plan the action.", now, a.DueAtUtc, a.Key));
        if (schedule.Status is "Blocked" or "CycleDetected") alerts.Add(new("schedule:blocked", "Critical", "Schedule", "Schedule cannot proceed reliably", schedule.Status == "CycleDetected" ? "A dependency cycle prevents a reliable finish forecast." : $"The schedule is blocked; {schedule.AtRiskItems} item(s) require attention.", "Resolve the blocking dependency/decision before committing to a release date.", now, null, "schedule"));
         else if (schedule.DelayDays > 0) alerts.Add(new("schedule:delay", "High", "Schedule", "Forecast date has slipped", $"Current forecast is {schedule.DelayDays} day(s) later than the planned finish.", "Review critical-path work, resource constraints, and scope changes before accepting the new date.", now, schedule.PlannedFinishUtc, "schedule"));
        foreach (var c in changes.Changes.Where(x => !IsFinalChange(x.Status) && (x.ScheduleDaysDelta > 0 || x.RiskDeltaPercent >= 10m) && (x.Priority is "Critical" or "High"))) alerts.Add(new($"change:{c.Key}", c.Priority == "Critical" ? "Critical" : "High", "ScopeChange", c.Title, c.ManagementImpact, "Approve, reject, or defer the change with an explicit decision before it silently enters scope.", now, null, c.Key));
        var ordered = alerts.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderByDescending(x => SeverityRank(x.Severity)).ThenBy(x => x.DueAtUtc ?? DateTimeOffset.MaxValue).ToArray(); return new(projectId, ordered.Count(x => x.Severity == "Critical"), ordered.Count(x => x.Severity == "High"), ordered.Length, ordered, now);
    }

    private static Dictionary<string, decimal> BuildAllocationAdjustments(IReadOnlyList<DevelopmentGraphNode> nodes, DateTimeOffset start, DateTimeOffset end)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in nodes.Where(x => x.Type == DevelopmentGraphNodeType.ResourceAllocation && IsActiveAllocation(x.Status)))
        {
            var from = WorkManagementService.MetaString(a, "FromWorkstreamKey");
             var to = WorkManagementService.MetaString(a, "ToWorkstreamKey");
             var hours = Math.Max(0m, WorkManagementService.MetaDecimal(a, "CapacityHoursPerWeek"));
             var allocationStart = WorkManagementService.MetaDate(a, "StartUtc", a.FirstObservedAtUtc);
             var allocationEnd = WorkManagementService.MetaDate(a, "EndUtc", a.LastObservedAtUtc);
            if (allocationStart >= end || allocationEnd < start) continue;
            if (!string.IsNullOrWhiteSpace(to)) map[to] = map.GetValueOrDefault(to) + hours; if (!string.IsNullOrWhiteSpace(from)) map[from] = map.GetValueOrDefault(from) - hours;
        }
        return map;
    }
    private static bool IsActiveAllocation(string status) => status.Equals("Approved", StringComparison.OrdinalIgnoreCase) || status.Equals("Active", StringComparison.OrdinalIgnoreCase) || status.Equals("Committed", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<WorkloadBucket> BuildBuckets(IEnumerable<DevelopmentGraphNode> tasks, IReadOnlyList<decimal> weeklyCapacities, HashSet<Guid> blockedTargets, DateTimeOffset start, int weeks)
    { var list = new List<WorkloadBucket>();
    for (var i = 0;
     i < weeks;
     i++) { var ws = start.AddDays(i * 7);
     var we = ws.AddDays(7);
     var active = tasks.Where(t => Overlaps(t, ws, we)).ToArray();
     decimal planned = 0m;
    foreach (var t in active) { var taskStart = WorkManagementService.MetaDate(t, "PlannedStartUtc", ws);
     var due = WorkManagementService.MetaDate(t, "DueAtUtc", we);
     var durationWeeks = Math.Max(1m, (decimal)Math.Ceiling(Math.Max(1d, (due - taskStart).TotalDays) / 7d));
     planned += WorkManagementService.MetaDecimal(t, "RemainingHours", WorkManagementService.MetaDecimal(t, "EstimateHours")) / durationWeeks;
     } var weeklyCapacity = i < weeklyCapacities.Count ? Math.Max(0m, weeklyCapacities[i]) : 0m;
     var util = weeklyCapacity <= 0m ? (planned > 0m ? 999m : 0m) : planned / weeklyCapacity * 100m;
     var blocked = active.Count(t => blockedTargets.Contains(t.Id) || WorkManagementService.IsBlocked(t.Status));
     list.Add(new(ws, Round(weeklyCapacity), Round(planned), Round(util), active.Length, blocked, CapacityStatus(util, blocked)));
     } return list;
     }

    private static IReadOnlyList<decimal> ApplyCalendarCapacity(IReadOnlyList<DevelopmentGraphNode> nodes, string teamKey, decimal baseWeekly, decimal baseTeamWeekly, DateTimeOffset start, int weeks)
    {
        var calendars = nodes.Where(x => x.Type == DevelopmentGraphNodeType.WorkCalendar && !x.Status.Equals("Archived", StringComparison.OrdinalIgnoreCase) && !x.Status.Equals("Inactive", StringComparison.OrdinalIgnoreCase)).Where(x => { var tk = WorkManagementService.MetaString(x, "TeamKey");
         return string.IsNullOrWhiteSpace(tk) || tk.Equals(teamKey, StringComparison.OrdinalIgnoreCase);
         }).ToArray();
        if (calendars.Length == 0) return Enumerable.Repeat(Math.Max(0m, baseWeekly), weeks).ToArray(); var result = new decimal[weeks];
        for (var i = 0; i < weeks; i++)
        {
            var ws = start.AddDays(i * 7); var we = ws.AddDays(7); decimal teamLoss = 0m;
            foreach (var calendar in calendars)
            {
                teamLoss += Math.Max(0m, WorkManagementService.MetaDecimal(calendar, "RecurringMeetingHoursPerWeek")); teamLoss += Math.Max(0m, WorkManagementService.MetaDecimal(calendar, "SupportDutyHoursPerWeek"));
                try { using var doc = JsonDocument.Parse(calendar.MetadataJson);
                if (doc.RootElement.TryGetProperty("Exceptions", out var ex) && ex.ValueKind == JsonValueKind.Array)
                foreach (var item in ex.EnumerateArray()) { if (!item.TryGetProperty("StartsAtUtc", out var s) || !s.TryGetDateTimeOffset(out var es) ||
                !item.TryGetProperty("EndsAtUtc", out var e) || !e.TryGetDateTimeOffset(out var ee) || es >= we || ee <= ws) continue;
                if (item.TryGetProperty("Hours", out var h) && h.TryGetDecimal(out var hours)) { var totalSeconds = Math.Max(1d, (ee - es).TotalSeconds);
                 var overlapStart = es > ws ? es : ws;
                 var overlapEnd = ee < we ? ee : we;
                 var overlapSeconds = Math.Max(0d, (overlapEnd - overlapStart).TotalSeconds);
                 teamLoss += Math.Max(0m, hours) * (decimal)Math.Clamp(overlapSeconds / totalSeconds, 0d, 1d);
                 } } }
                catch (JsonException) {
                    // Intentionally ignored to preserve the existing best-effort or fallback behavior.
                }
            }
            var ratio = baseTeamWeekly <= 0m ? 1m : Math.Clamp((baseTeamWeekly - teamLoss) / baseTeamWeekly, 0m, 1m); result[i] = Math.Max(0m, baseWeekly * ratio);
        }
        return result;
    }
    private static bool Overlaps(DevelopmentGraphNode t, DateTimeOffset ws, DateTimeOffset we) { if (WorkManagementService.IsDone(t.Status)) return false;
     var start = WorkManagementService.MetaDate(t, "PlannedStartUtc", ws);
     var due = WorkManagementService.MetaDate(t, "DueAtUtc", we);
     return start < we && due >= ws;
     }
    private static decimal EffectiveTeamWeekly(DevelopmentGraphNode team) { var raw = WorkManagementService.MetaDecimal(team, "CapacityHoursPerWeek");
     var buffer = Math.Clamp(WorkManagementService.MetaDecimal(team, "BufferPercent", 10m), 0m, 80m);
     return raw * (1m - buffer / 100m);
     }
    private static ManagementReference? ReadManagementReference(DevelopmentGraphNode node, string property)
    {
        try { using var doc = JsonDocument.Parse(node.MetadataJson);
        if (!doc.RootElement.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object) return null;
         var type = value.TryGetProperty("Type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
         var key = value.TryGetProperty("Key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
         return string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(key) ? null : new ManagementReference(type!, key!);
         }
        catch { return null;
         }
    }
    private static string CapacityStatus(decimal util, int blocked) => blocked > 0 && util >= 90m ? "Blocked" : util > 110m ? "OverCapacity" : util > 90m ? "NearCapacity" : "Healthy";
    private static HashSet<Guid> OpenBlockerTargets(IReadOnlyList<DevelopmentGraphNode> nodes, IReadOnlyList<DevelopmentGraphEdge> edges) { var open = nodes.Where(x => x.Type == DevelopmentGraphNodeType.Blocker && !WorkManagementService.IsDone(x.Status)).Select(x => x.Id).ToHashSet(); return edges.Where(x => open.Contains(x.SourceNodeId) && x.Type == DevelopmentGraphEdgeType.Blocks).Select(x => x.TargetNodeId).ToHashSet(); }
    private static Dictionary<Guid, Guid> BuildWorkstreamMap(IReadOnlyList<DevelopmentGraphNode> nodes, IReadOnlyList<DevelopmentGraphEdge> edges) { var tasks = nodes.Where(x => x.Type == DevelopmentGraphNodeType.Task).Select(x => x.Id).ToHashSet();
     var ws = nodes.Where(x => x.Type == DevelopmentGraphNodeType.Workstream).Select(x => x.Id).ToHashSet();
     var map = new Dictionary<Guid, Guid>();
    foreach (var id in ws) map[id] = id;
    foreach (var e in edges.Where(e => tasks.Contains(e.SourceNodeId) && ws.Contains(e.TargetNodeId) && e.Type == DevelopmentGraphEdgeType.BelongsTo)) map[e.SourceNodeId] = e.TargetNodeId;
     return map;
     }
    private static HashSet<Guid> TraverseDownstream(IEnumerable<Guid> roots, IReadOnlyList<DevelopmentGraphEdge> edges, IReadOnlyDictionary<Guid, DevelopmentGraphNode> byId) { var seen = new HashSet<Guid>();
     var q = new Queue<Guid>(roots);
    while (q.Count > 0) { var id = q.Dequeue();
    foreach (var e in edges.Where(x => x.SourceNodeId == id && x.Type is DevelopmentGraphEdgeType.Blocks or DevelopmentGraphEdgeType.DependsOn or
    DevelopmentGraphEdgeType.ScheduledFor)) { if (!byId.ContainsKey(e.TargetNodeId) || !seen.Add(e.TargetNodeId)) continue;
     q.Enqueue(e.TargetNodeId);
     } }
    foreach (var r in roots) seen.Remove(r);
     return seen;
     }
    private static List<Guid> FindCycle(IEnumerable<Guid> ids, IReadOnlyList<DevelopmentGraphEdge> edges) { var state = ids.ToDictionary(x => x, _ => 0);
     var parent = new Dictionary<Guid, Guid>();
    foreach (var root in ids) { if (state[root] != 0) continue;
     var cycle = Dfs(root, state, parent, edges);
    if (cycle.Count > 0) return cycle;
     } return [];
     }
    private static List<Guid> Dfs(Guid u, Dictionary<Guid, int> state, Dictionary<Guid, Guid> parent, IReadOnlyList<DevelopmentGraphEdge> edges) { state[u] = 1;
    foreach (var e in edges.Where(x => x.SourceNodeId == u)) { var v = e.TargetNodeId;
    if (!state.ContainsKey(v)) continue;
    if (state[v] == 0) { parent[v] = u;
     var c = Dfs(v, state, parent, edges);
    if (c.Count > 0) return c;
     }
    else if (state[v] == 1) { var c = new List<Guid> { v };
     var cur = u;
    while (cur != v && parent.TryGetValue(cur, out var p)) { c.Add(cur);
     cur = p;
     } c.Add(v);
     c.Reverse();
     return c;
     } } state[u] = 2;
     return [];
     }
    private static ScheduleModel ParseSchedule(DevelopmentGraphNode n) { var start = WorkManagementService.MetaDate(n, "PlannedStartUtc", n.FirstObservedAtUtc);
     var due = WorkManagementService.MetaDate(n, "DueAtUtc", start.AddDays(1));
     var duration = n.Type == DevelopmentGraphNodeType.Milestone ? (int)WorkManagementService.MetaDecimal(n, "DurationDays", Math.Max(1, (decimal)Math.Ceiling((due - start).TotalDays))) : Math.Max(1, (int)Math.Ceiling(Math.Max(1d, (due - start).TotalDays)));
     var completion = WorkManagementService.MetaDecimal(n, "CompletionPercent");
     var ws = WorkManagementService.MetaString(n, "WorkstreamKey", "");
     return new(n, start, due, duration, completion, ws);
     }
    private static DateTimeOffset StartOfWeek(DateTimeOffset value) { var d = value.UtcDateTime.Date;
     var offset = ((int)d.DayOfWeek + 6) % 7;
     return new DateTimeOffset(d.AddDays(-offset), TimeSpan.Zero);
     }
    private static decimal Round(decimal x) => Math.Round(x, 1, MidpointRounding.AwayFromZero);
    private static int SeverityRank(string x) => x.Equals("Critical", StringComparison.OrdinalIgnoreCase) ? 4 : x.Equals("High", StringComparison.OrdinalIgnoreCase) ? 3 : x.Equals("Medium", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
    private static int PriorityRank(string x) => SeverityRank(x);
    private static string ExposureLevel(decimal x) => x >= 70m ? "Critical" : x >= 45m ? "High" : x >= 20m ? "Medium" : "Low";
    private static bool IsFinalChange(string status) => status.Equals("Approved", StringComparison.OrdinalIgnoreCase) || status.Equals("Rejected", StringComparison.OrdinalIgnoreCase) || status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) || status.Equals("Closed", StringComparison.OrdinalIgnoreCase);
    private static string Signed(decimal x) => x > 0m ? "+" + x.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) : x.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    private static string Signed(int x) => x > 0 ? "+" + x.ToString(System.Globalization.CultureInfo.InvariantCulture) : x.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string? NullIfEmpty(string x) => string.IsNullOrWhiteSpace(x) ? null : x;
    private sealed record ScheduleModel(DevelopmentGraphNode Node, DateTimeOffset Start, DateTimeOffset Due, int DurationDays, decimal Completion, string WorkstreamKey);
}
