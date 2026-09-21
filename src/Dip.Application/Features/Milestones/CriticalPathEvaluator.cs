// Purpose: Calculates milestone critical paths and schedule risk as a pure deterministic function.
namespace Dip.Application.Features.Milestones;

public sealed record MilestoneCriticalPathInput(
    Guid Id,
    string Key,
    string Name,
    int DurationDays,
    DateTimeOffset DueAtUtc,
    decimal CompletionPercent,
    string State);

public sealed record MilestoneDependencyInput(
    Guid SourceNodeId,
    Guid TargetNodeId);

public static class CriticalPathEvaluator
{
    public static CriticalPathResult Evaluate(
        IReadOnlyList<MilestoneCriticalPathInput> milestones,
        IReadOnlyList<MilestoneDependencyInput> dependencies,
        DateTimeOffset now)
    {
        if (milestones.Count == 0)
        {
            return new(
                "NoMilestones",
                0,
                0,
                0,
                [],
                [],
                [],
                "No milestones are registered in the Development Graph.");
        }

        var byId = milestones.ToDictionary(item => item.Id);
        var indegree = milestones.ToDictionary(item => item.Id, _ => 0);
        foreach (var edge in dependencies)
            indegree[edge.TargetNodeId]++;

        var queue = new Queue<Guid>(indegree.Where(item => item.Value == 0).Select(item => item.Key));
        var order = new List<Guid>(milestones.Count);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            order.Add(id);
            foreach (var edge in dependencies.Where(item => item.SourceNodeId == id))
            {
                if (--indegree[edge.TargetNodeId] == 0)
                    queue.Enqueue(edge.TargetNodeId);
            }
        }

        if (order.Count != milestones.Count)
        {
            return new(
                "CycleDetected",
                0,
                0,
                0,
                [],
                [],
                ["Milestone dependency graph contains a cycle. Remove the cycle before relying on schedule forecasts."],
                "Critical path cannot be calculated while milestone dependencies contain a cycle.");
        }

        var earliestStart = milestones.ToDictionary(item => item.Id, _ => 0);
        var earliestFinish = milestones.ToDictionary(item => item.Id, _ => 0);
        foreach (var id in order)
        {
            var predecessors = dependencies
                .Where(edge => edge.TargetNodeId == id)
                .Select(edge => edge.SourceNodeId)
                .ToArray();
            earliestStart[id] = predecessors.Length == 0
                ? 0
                : predecessors.Max(predecessor => earliestFinish[predecessor]);
            earliestFinish[id] = earliestStart[id] + byId[id].DurationDays;
        }

        var totalDuration = earliestFinish.Values.Max();
        var latestFinish = milestones.ToDictionary(item => item.Id, _ => totalDuration);
        var latestStart = milestones.ToDictionary(item => item.Id, _ => 0);
        foreach (var id in order.AsEnumerable().Reverse())
        {
            var successors = dependencies
                .Where(edge => edge.SourceNodeId == id)
                .Select(edge => edge.TargetNodeId)
                .ToArray();
            latestFinish[id] = successors.Length == 0
                ? totalDuration
                : successors.Min(successor => latestStart[successor]);
            latestStart[id] = latestFinish[id] - byId[id].DurationDays;
        }

        var calculated = order
            .Select(id =>
            {
                var milestone = byId[id];
                var slack = Math.Max(0, latestStart[id] - earliestStart[id]);
                return new CriticalPathMilestone(
                    milestone.Key,
                    milestone.Name,
                    milestone.DurationDays,
                    earliestStart[id],
                    earliestFinish[id],
                    latestStart[id],
                    latestFinish[id],
                    slack,
                    slack == 0,
                    milestone.DueAtUtc,
                    milestone.CompletionPercent,
                    milestone.State);
            })
            .ToArray();

        var criticalPath = calculated
            .Where(item => item.IsCritical)
            .Select(item => item.Key)
            .ToArray();
        var blockers = new List<string>();
        foreach (var milestone in calculated.Where(item => item.DueAtUtc < now && item.CompletionPercent < 100m))
        {
            blockers.Add(
                $"Milestone '{milestone.Name}' is past due with {milestone.CompletionPercent:0.#}% evidence-backed completion.");
        }

        foreach (var milestone in calculated.Where(item =>
                     string.Equals(item.State, "Blocked", StringComparison.OrdinalIgnoreCase)))
        {
            blockers.Add($"Milestone '{milestone.Name}' is explicitly blocked.");
        }

        var atRisk = calculated.Count(item =>
            item.DueAtUtc < now.AddDays(Math.Max(3, item.SlackDays + 2))
            && item.CompletionPercent < 100m);
        var status = blockers.Count > 0 ? "Blocked" : atRisk > 0 ? "AtRisk" : "OnTrack";

        return new(
            status,
            totalDuration,
            criticalPath.Length,
            atRisk,
            calculated,
            criticalPath,
            blockers,
            $"Critical path spans {totalDuration} planned day(s) across {criticalPath.Length} critical milestone(s); {atRisk} milestone(s) are at risk.");
    }
}
