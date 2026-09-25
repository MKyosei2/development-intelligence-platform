// Purpose: Implements Portfolio Management Service application behavior for management.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Dashboard;

namespace Dip.Application.Features.Management;

public sealed class PortfolioManagementService
{
    private readonly IProjectRepository _projects;
    private readonly ManagementDashboardService _dashboard;
    private readonly ManagementOperationsService _operations;
    private readonly ISystemClock _clock;
    public PortfolioManagementService(IProjectRepository projects, ManagementDashboardService dashboard, ManagementOperationsService operations, ISystemClock clock)
    { _projects = projects; _dashboard = dashboard; _operations = operations; _clock = clock; }

    public async Task<PortfolioManagementDto> GetAsync(IReadOnlySet<Guid>? allowedProjectIds, int horizonWeeks, CancellationToken ct)
    {
        horizonWeeks = Math.Clamp(horizonWeeks, 1, 26);
         var projects = await _projects.ListAsync(ct);
        if (allowedProjectIds is not null) projects = projects.Where(x => allowedProjectIds.Contains(x.Id)).ToArray();
        var items = new List<PortfolioProjectDto>(); var capacities = new List<(Guid ProjectId, string Project, string PoolKey, string Team, decimal PoolCapacity, decimal Planned)>();
        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();
             var d = await _dashboard.GetAsync(project.Id, horizonWeeks, ct);
             var approvals = await _operations.GetApprovalsAsync(project.Id, ct);
             var sync = await _operations.GetSyncStateAsync(project.Id, ct);
             var peak = d.Capacity.Teams.Count == 0 ? 0m : d.Capacity.Teams.Max(x => x.UtilizationPercent);
            items.Add(new(project.Id, project.Name, d.OverallStatus, d.HealthScore, d.Schedule.Status, d.Schedule.DelayDays, peak, d.Risks.Critical, d.Blockers.Open, d.Decisions.DecisionsPending, d.Decisions.OverdueActions, approvals.Pending, sync.OpenConflicts));
            foreach (var team in d.Capacity.Teams.Where(x => !string.IsNullOrWhiteSpace(x.SharedResourceKey) && x.SharedPoolCapacityHoursPerWeek.HasValue)) capacities.Add((project.Id, project.Name, team.SharedResourceKey!, team.Name, team.SharedPoolCapacityHoursPerWeek!.Value, team.PlannedHours / horizonWeeks));
        }
        var shared = capacities.GroupBy(x => x.PoolKey, StringComparer.OrdinalIgnoreCase).Where(g => g.Select(x => x.ProjectId).Distinct().Count() > 1).Select(g =>
        {
            var configured = g.Select(x => Math.Round(x.PoolCapacity, 2)).Distinct().OrderBy(x => x).ToArray();
             var available = configured.Min();
             var planned = g.Sum(x => x.Planned);
             var shortage = Math.Max(0m, planned - available);
             var projects = g.Select(x => x.Project).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
             var members = string.Join(", ", g.Select(x => $"{x.Project}/{x.Team}").Distinct(StringComparer.OrdinalIgnoreCase));
             var mismatch = configured.Length > 1;
            var recommendation = mismatch ? $"Shared pool capacity is configured inconsistently ({string.Join(" / ", configured.Select(x => $"{x:0.#} h/week"))}). DIP conservatively uses {available:0.#} h/week. Align the pool limit before committing dates; members: {members}." : shortage > 0m ? $"Reduce scope, move dates, or re-plan {shortage:0.#} h/week across this shared resource pool before committing dates. Members: {members}." : $"Shared resource pool is within its explicit weekly capacity. Members: {members}.";
            return new SharedCapacityConflictDto(g.Key, Math.Round(available, 1), Math.Round(planned, 1), Math.Round(shortage, 1), projects, recommendation);
        }).Where(x => x.ShortageHoursPerWeek > 0m || x.Recommendation.Contains("inconsistently", StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.ShortageHoursPerWeek).ThenBy(x => x.TeamName).ToArray();
        var actions = new List<string>();
        foreach (var x in items.Where(x => x.OverallStatus is "Critical" or "Blocked" or "AtRisk").OrderByDescending(x => x.CriticalRisks).Take(8)) actions.Add($"{x.Name}: {x.OverallStatus}; {x.CriticalRisks} critical risk(s), {x.OpenBlockers} blocker(s), {x.DelayDays} day schedule delay.");
        foreach (var x in shared.Take(5)) actions.Add($"Shared resource pool '{x.TeamName}': {x.ShortageHoursPerWeek:0.#} h/week shortage across {string.Join(", ", x.Projects)}.");
        return new(items.Count, items.Count(x => x.OverallStatus is "AtRisk" or "Watch"), items.Count(x => x.OverallStatus is "Critical" or "Blocked"), items.OrderBy(x => Rank(x.OverallStatus)).ThenBy(x => x.Name).ToArray(), shared, actions, _clock.UtcNow);
    }

    private static int Rank(string status) => status switch { "Critical" or "Blocked" => 0, "AtRisk" => 1, "Watch" => 2, _ => 3 };
}
