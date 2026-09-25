// Purpose: Implements Management Spreadsheet Service application behavior for management.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Spreadsheets;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.Milestones;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

public sealed class ManagementSpreadsheetService
{
    public static readonly string[] EditableSheets = ["Teams", "Workstreams", "Tasks", "Milestones", "Blockers", "Requirements", "Dependencies", "ScopeBaselines", "Risks", "Decisions", "Actions", "ChangeRequests", "ResourceAllocations"];
    private readonly IDevelopmentGraphRepository _repo;
     private readonly WorkManagementService _work;
     private readonly ManagementAnalyticsService _analytics;
     private readonly MilestoneIntelligenceService _milestones;
     private readonly IGoogleSheetsConnector _google;
     private readonly ISystemClock _clock;
     private readonly DevelopmentGraphService _graph;
     private readonly ManagementOperationsService _operations;
    public ManagementSpreadsheetService(IDevelopmentGraphRepository repo, WorkManagementService work, ManagementAnalyticsService analytics, MilestoneIntelligenceService milestones, IGoogleSheetsConnector google, ISystemClock clock, DevelopmentGraphService graph, ManagementOperationsService operations) { _repo = repo;
     _work = work;
     _analytics = analytics;
     _milestones = milestones;
     _google = google;
     _clock = clock;
     _graph = graph;
     _operations = operations;
     }

    public async Task<ManagementWorkbookDto> BuildWorkbookAsync(Guid projectId, CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, null, ct);
         var capacity = await _analytics.GetCapacityAsync(projectId, 8, ct);
         var schedule = await _analytics.GetScheduleAsync(projectId, ct);
         var blockers = await _analytics.GetBlockersAsync(projectId, ct);
         var deps = await _analytics.GetDependenciesAsync(projectId, ct);
         var milestoneSummary = await _analytics.GetMilestonesAsync(projectId, ct);
         var scope = await _analytics.GetScopeAsync(projectId, ct);
         var risks = await _analytics.GetRisksAsync(projectId, ct);
         var decisions = await _analytics.GetDecisionsAsync(projectId, ct);
         var resources = await _analytics.GetResourceAllocationsAsync(projectId, 8, ct);
         var scopeChanges = await _analytics.GetScopeChangesAsync(projectId, ct);
         var alerts = await _analytics.GetAlertsAsync(projectId, 8, ct);
         var sheets = new List<SpreadsheetSheetDto>();
        var maxUtil = capacity.Teams.Count == 0 ? 0m : capacity.Teams.Max(x => x.UtilizationPercent); var health = alerts.Critical > 0 || schedule.Status == "Blocked" || blockers.Critical > 0 ? "Critical" : alerts.High > 0 || schedule.Status == "AtRisk" || maxUtil > 100m || scope.ScopeGrowthPercent > 10m || risks.Critical > 0 ? "AtRisk" : "Healthy";
        sheets.Add(Sheet("Executive", ["Metric", "Value", "Management meaning"], [
            ["Project health",health,"Overall management status derived from schedule, blockers, capacity and scope."],
            ["Forecast finish",schedule.ForecastFinishUtc?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)??"Not available","Current dependency-aware finish forecast."],
            ["Forecast delay (days)",schedule.DelayDays.ToString(CultureInfo.InvariantCulture),"Difference between planned and forecast finish."],
            ["Open blockers",blockers.Open.ToString(CultureInfo.InvariantCulture),"Items preventing work from flowing."],
            ["Critical blockers",blockers.Critical.ToString(CultureInfo.InvariantCulture),"Blockers requiring immediate management attention."],
            ["Peak team utilization %",F(maxUtil),"Over 100% indicates planned demand exceeds available capacity."],
            ["Scope growth %",F(scope.ScopeGrowthPercent),"Growth from the latest scope baseline."],
            ["Milestones at risk",milestoneSummary.AtRisk.ToString(CultureInfo.InvariantCulture),"Milestones likely to miss current expectations."],
            ["Milestones blocked",milestoneSummary.Blocked.ToString(CultureInfo.InvariantCulture),"Milestones directly or indirectly blocked."],
            ["Critical-path items",schedule.CriticalItems.ToString(CultureInfo.InvariantCulture),"Items with no calculated schedule slack."],
            ["Open risks",risks.Open.ToString(CultureInfo.InvariantCulture),"Future conditions that may affect delivery if they occur."],
            ["Critical risks",risks.Critical.ToString(CultureInfo.InvariantCulture),"Risks with critical probability/impact exposure."],
            ["Pending decisions",decisions.DecisionsPending.ToString(CultureInfo.InvariantCulture),"Management decisions still awaiting resolution."],
            ["Overdue actions",decisions.OverdueActions.ToString(CultureInfo.InvariantCulture),"Management actions past their due date."],
            ["Pending scope change days",scopeChanges.PendingScheduleDays.ToString(CultureInfo.InvariantCulture),"Potential schedule impact from unresolved change requests."],
            ["Active escalations",alerts.Total.ToString(CultureInfo.InvariantCulture),"Management alerts requiring review or action."]
        ]));
        sheets.Add(Sheet("Teams", ["Key", "Name", "CapacityHoursPerWeek", "BufferPercent", "Status", "SharedResourceKey", "SharedPoolCapacityHoursPerWeek"], nodes.Where(x => x.Type == DevelopmentGraphNodeType.Team).OrderBy(x => x.Name).Select(x => (IReadOnlyList<string>)[WorkManagementService.Strip(x.Key), x.Name, F(WorkManagementService.MetaDecimal(x, "CapacityHoursPerWeek")), F(WorkManagementService.MetaDecimal(x, "BufferPercent", 10m)), x.Status, WorkManagementService.MetaString(x, "SharedResourceKey"), WorkManagementService.MetaDecimal(x, "SharedPoolCapacityHoursPerWeek") > 0m ? F(WorkManagementService.MetaDecimal(x, "SharedPoolCapacityHoursPerWeek")) : ""]).ToArray(), true));
        sheets.Add(Sheet("Workstreams", ["Key", "Name", "TeamKey", "PlannedCapacityHoursPerWeek", "PlannedStart", "Due", "CompletionPercent", "Status"], nodes.Where(x => x.Type == DevelopmentGraphNodeType.Workstream).OrderBy(x => x.Name).Select(x => (IReadOnlyList<string>)[WorkManagementService.Strip(x.Key), x.Name, WorkManagementService.MetaString(x, "TeamKey"), F(WorkManagementService.MetaDecimal(x, "PlannedCapacityHoursPerWeek")), D(WorkManagementService.MetaDate(x, "PlannedStartUtc", x.FirstObservedAtUtc)), D(WorkManagementService.MetaDate(x, "DueAtUtc", x.LastObservedAtUtc)), F(WorkManagementService.MetaDecimal(x, "CompletionPercent")), x.Status]).ToArray(), true));
        sheets.Add(Sheet("Tasks", ["Key", "Name", "WorkstreamKey", "EstimateHours", "RemainingHours", "PlannedStart", "Due", "CompletionPercent", "Status", "Priority", "OwnerLabel", "ExternalReference"], nodes.Where(x => x.Type == DevelopmentGraphNodeType.Task).OrderBy(x => x.Name).Select(x => (IReadOnlyList<string>)[WorkManagementService.Strip(x.Key), x.Name, WorkManagementService.MetaString(x, "WorkstreamKey"), F(WorkManagementService.MetaDecimal(x, "EstimateHours")), F(WorkManagementService.MetaDecimal(x, "RemainingHours")), D(WorkManagementService.MetaDate(x, "PlannedStartUtc", x.FirstObservedAtUtc)), D(WorkManagementService.MetaDate(x, "DueAtUtc", x.LastObservedAtUtc)), F(WorkManagementService.MetaDecimal(x, "CompletionPercent")), x.Status, WorkManagementService.MetaString(x, "Priority", "Medium"), WorkManagementService.MetaString(x, "OwnerLabel"), x.ExternalReference ?? ""]).ToArray(), true));
        sheets.Add(Sheet("Schedule", ["Type", "Key", "Name", "Workstream", "PlannedStart", "Due", "ForecastFinish", "DurationDays", "CompletionPercent", "Status", "SlackDays", "Critical", "BlockedBy"], schedule.Items.Select(x => (IReadOnlyList<string>)[x.Type, x.Key, x.Name, x.WorkstreamKey, D(x.PlannedStartUtc), D(x.DueAtUtc), D(x.ForecastFinishUtc), x.DurationDays.ToString(CultureInfo.InvariantCulture), F(x.CompletionPercent), x.Status, x.SlackDays.ToString(CultureInfo.InvariantCulture), x.IsCritical ? "Yes" : "No", x.BlockedByCount.ToString(CultureInfo.InvariantCulture)]).ToArray()));
        sheets.Add(BuildGanttSheet(schedule));
        sheets.Add(Sheet("Milestones", ["Key", "Name", "PlannedStart", "Due", "DurationDays", "CompletionPercent", "State", "ExternalReference"], nodes.Where(x => x.Type == DevelopmentGraphNodeType.Milestone).OrderBy(x => WorkManagementService.MetaDate(x, "DueAtUtc", DateTimeOffset.MaxValue)).Select(x => (IReadOnlyList<string>)[WorkManagementService.Strip(x.Key), x.Name, D(WorkManagementService.MetaDate(x, "PlannedStartUtc", x.FirstObservedAtUtc)), D(WorkManagementService.MetaDate(x, "DueAtUtc", x.LastObservedAtUtc)), ((int)WorkManagementService.MetaDecimal(x, "DurationDays", 1m)).ToString(CultureInfo.InvariantCulture), F(WorkManagementService.MetaDecimal(x, "CompletionPercent")), x.Status, x.ExternalReference ?? ""]).ToArray(), true));
        var blockerImpact = blockers.Items.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        sheets.Add(Sheet("Blockers", ["Key", "Title", "Reason", "Severity", "OpenedAt", "Status", "BlockedTasks", "BlockedWorkstreams", "BlockedMilestones", "AgeDays", "DownstreamAffected", "ManagementAction"], nodes.Where(x => x.Type == DevelopmentGraphNodeType.Blocker).OrderBy(x => x.Name).Select(x => { var key = WorkManagementService.Strip(x.Key);
         blockerImpact.TryGetValue(key, out var impact);
         return (IReadOnlyList<string>)[key, x.Name, WorkManagementService.MetaString(x, "Reason"), WorkManagementService.MetaString(x, "Severity", "Medium"),
         D(WorkManagementService.MetaDate(x, "OpenedAtUtc", x.FirstObservedAtUtc)), x.Status, string.Join(";", WorkManagementService.MetaStringArray(x,
         "BlockedTaskKeys")), string.Join(";", WorkManagementService.MetaStringArray(x, "BlockedWorkstreamKeys")), string.Join(";",
         WorkManagementService.MetaStringArray(x, "BlockedMilestoneKeys")), (impact?.AgeDays ?? 0).ToString(CultureInfo.InvariantCulture), (impact?.DownstreamAffected ??
         0).ToString(CultureInfo.InvariantCulture), impact?.ManagementAction ?? ""];
         }).ToArray(), true));
        sheets.Add(Sheet("Dependencies", ["SourceType", "SourceKey", "SourceName", "TargetType", "TargetKey", "TargetName", "Relation", "Critical", "CrossWorkstream"], deps.Dependencies.Select(x => (IReadOnlyList<string>)[x.SourceType, x.SourceKey, x.SourceName, x.TargetType, x.TargetKey, x.TargetName, x.Relation, x.Critical ? "Yes" : "No", x.CrossWorkstream ? "Yes" : "No"]).ToArray(), true));
        sheets.Add(Sheet("Requirements", ["Key", "Title", "Priority", "Status", "ScopeState", "CompletionPercent", "AcceptanceSummary", "ExternalReference"], scope.Requirements.Select(x => (IReadOnlyList<string>)[x.Key, x.Title, x.Priority, x.Status, x.ScopeState, F(x.CompletionPercent), x.AcceptanceSummary ?? "", x.ExternalReference ?? ""]).ToArray(), true));
        sheets.Add(Sheet("Scope", ["Metric", "Value"], [
            ["Baseline",scope.BaselineKey??"None"],["BaselineRequirements",scope.BaselineRequirements.ToString(CultureInfo.InvariantCulture)],["CurrentIncludedRequirements",scope.CurrentIncludedRequirements.ToString(CultureInfo.InvariantCulture)],["AddedAfterBaseline",scope.AddedAfterBaseline.ToString(CultureInfo.InvariantCulture)],["DeferredOrRemoved",scope.DeferredOrRemoved.ToString(CultureInfo.InvariantCulture)],["ScopeGrowthPercent",F(scope.ScopeGrowthPercent)],["IncompleteHighPriority",scope.IncompleteHighPriority.ToString(CultureInfo.InvariantCulture)]
        ]));
        sheets.Add(Sheet("Capacity", ["Level", "Key", "Name", "CapacityHours", "RemainingHours", "UtilizationPercent", "ActiveTasks", "BlockedTasks", "Status"], capacity.Teams.Select(x => (IReadOnlyList<string>)["Team", x.Key, x.Name, F(x.CapacityHours), F(x.RemainingHours), F(x.UtilizationPercent), x.ActiveTasks.ToString(CultureInfo.InvariantCulture), x.BlockedTasks.ToString(CultureInfo.InvariantCulture), x.Status]).Concat(capacity.Workstreams.Select(x => (IReadOnlyList<string>)["Workstream", x.Key, x.Name, F(x.CapacityHours), F(x.RemainingHours), F(x.UtilizationPercent), x.ActiveTasks.ToString(CultureInfo.InvariantCulture), x.BlockedTasks.ToString(CultureInfo.InvariantCulture), x.Status])).ToArray()));
        sheets.Add(BuildWorkloadSheet(capacity));
        sheets.Add(Sheet("Risks", ["Key", "Title", "Description", "ProbabilityPercent", "ImpactPercent", "Status", "Category", "OwnerRole", "ReviewDue", "Trigger", "Mitigation", "AffectedItems", "ExposureScore", "ExposureLevel", "ReviewOverdue", "ManagementAction"], risks.Items.Select(x => (IReadOnlyList<string>)[x.Key, x.Title, x.Description, F(x.ProbabilityPercent), F(x.ImpactPercent), x.Status, x.Category, x.OwnerRole ?? "", x.ReviewDueUtc.HasValue ? D(x.ReviewDueUtc.Value) : "", x.Trigger ?? "", x.Mitigation ?? "", string.Join(";", x.AffectedItems), F(x.ExposureScore), x.ExposureLevel, x.ReviewOverdue ? "Yes" : "No", x.ManagementAction]).ToArray(), true));
        sheets.Add(Sheet("Decisions", ["Key", "Title", "Decision", "Rationale", "Status", "DecisionOwnerRole", "DecisionDue", "DecidedAt", "RelatedItems", "OpenActions"], decisions.Decisions.Select(x => (IReadOnlyList<string>)[x.Key, x.Title, x.Decision, x.Rationale, x.Status, x.DecisionOwnerRole ?? "", x.DecisionDueUtc.HasValue ? D(x.DecisionDueUtc.Value) : "", x.DecidedAtUtc.HasValue ? D(x.DecidedAtUtc.Value) : "", string.Join(";", x.RelatedItems), x.OpenActions.ToString(CultureInfo.InvariantCulture)]).ToArray(), true));
        sheets.Add(Sheet("Actions", ["Key", "Title", "Status", "Priority", "OwnerRole", "Due", "CompletionPercent", "DecisionKey", "TargetType", "TargetKey", "Overdue"], nodes.Where(x => x.Type == DevelopmentGraphNodeType.ActionItem).OrderBy(x => x.Name).Select(x => { var target = ReadReference(x, "Target");
         var due = WorkManagementService.MetaDate(x, "DueAtUtc", DateTimeOffset.MaxValue);
         var dto = decisions.Actions.FirstOrDefault(a => a.Key.Equals(WorkManagementService.Strip(x.Key), StringComparison.OrdinalIgnoreCase));
         return (IReadOnlyList<string>)[WorkManagementService.Strip(x.Key), x.Name, x.Status, WorkManagementService.MetaString(x, "Priority", "Medium"), WorkManagementService.MetaString(x, "OwnerRole"), due == DateTimeOffset.MaxValue ? "" : D(due), F(WorkManagementService.MetaDecimal(x, "CompletionPercent")), WorkManagementService.MetaString(x, "DecisionKey"), target?.Type ?? "", target?.Key ?? "", dto?.Overdue == true ? "Yes" : "No"];
         }).ToArray(), true));
        sheets.Add(Sheet("ChangeRequests", ["Key", "Title", "Description", "Status", "Priority", "RequestedByRole", "RequestedAt", "EngineeringHoursDelta",
        "ArtHoursDelta", "QaHoursDelta", "ScheduleDaysDelta", "RiskDeltaPercent", "RequirementKeys", "WorkstreamKeys", "DecisionKey", "ManagementImpact"],
        scopeChanges.Changes.Select(x => (IReadOnlyList<string>)[x.Key, x.Title, x.Description, x.Status, x.Priority, x.RequestedByRole ?? "", D(x.RequestedAtUtc),
        F(x.EngineeringHoursDelta), F(x.ArtHoursDelta), F(x.QaHoursDelta), x.ScheduleDaysDelta.ToString(CultureInfo.InvariantCulture), F(x.RiskDeltaPercent),
        string.Join(";", x.RequirementKeys), string.Join(";", x.WorkstreamKeys), x.DecisionKey ?? "", x.ManagementImpact]).ToArray(), true));
        sheets.Add(Sheet("ResourceAllocations", ["Key", "Status", "FromWorkstreamKey", "ToWorkstreamKey", "CapacityHoursPerWeek", "Start", "End", "Rationale", "DecisionKey"], resources.Allocations.Select(x => (IReadOnlyList<string>)[x.Key, x.Status, x.FromWorkstreamKey ?? "", x.ToWorkstreamKey, F(x.CapacityHoursPerWeek), D(x.StartUtc), D(x.EndUtc), x.Rationale ?? "", x.DecisionKey ?? ""]).ToArray(), true));
        sheets.Add(Sheet("ResourceRecommendations", ["WorkstreamKey", "WorkstreamName", "TeamName", "CurrentUtilizationPercent", "SuggestedAdditionalHoursPerWeek", "Reason"], resources.Recommendations.Select(x => (IReadOnlyList<string>)[x.WorkstreamKey, x.WorkstreamName, x.TeamName, F(x.CurrentUtilizationPercent), F(x.SuggestedAdditionalHoursPerWeek), x.Reason]).ToArray()));
        sheets.Add(Sheet("Escalations", ["Severity", "Category", "Title", "Explanation", "RecommendedAction", "Due", "SourceKey"], alerts.Alerts.Select(x => (IReadOnlyList<string>)[x.Severity, x.Category, x.Title, x.Explanation, x.RecommendedAction, x.DueAtUtc.HasValue ? D(x.DueAtUtc.Value) : "", x.SourceKey]).ToArray()));
        sheets.Add(Sheet("ManagementSummary", ["Metric", "Value"], [
            ["ProjectHealth",health],["OpenRisks",risks.Open.ToString(CultureInfo.InvariantCulture)],["CriticalRisks",risks.Critical.ToString(CultureInfo.InvariantCulture)],["PendingDecisions",decisions.DecisionsPending.ToString(CultureInfo.InvariantCulture)],["OpenActions",decisions.OpenActions.ToString(CultureInfo.InvariantCulture)],["OverdueActions",decisions.OverdueActions.ToString(CultureInfo.InvariantCulture)],["ResourceRecommendations",resources.Recommendations.Count.ToString(CultureInfo.InvariantCulture)],["PendingChangeRequests",scopeChanges.Proposed.ToString(CultureInfo.InvariantCulture)],["PendingScheduleImpactDays",scopeChanges.PendingScheduleDays.ToString(CultureInfo.InvariantCulture)],["CriticalEscalations",alerts.Critical.ToString(CultureInfo.InvariantCulture)],["HighEscalations",alerts.High.ToString(CultureInfo.InvariantCulture)]
        ]));
        sheets.Add(Sheet("ScopeBaselines", ["Key", "Name", "CapturedAt", "BaselineRequirements"], nodes.Where(x => x.Type == DevelopmentGraphNodeType.ScopeBaseline).OrderByDescending(x => x.LastObservedAtUtc).Select(x => (IReadOnlyList<string>)[WorkManagementService.Strip(x.Key), x.Name, D(WorkManagementService.MetaDate(x, "CapturedAtUtc", x.FirstObservedAtUtc)), F(WorkManagementService.MetaDecimal(x, "BaselineRequirements"))]).ToArray(), true));
        sheets.Add(Sheet("RawGraph", ["Type", "Key", "Name", "Status", "Provider", "ExternalReference", "LastObservedUtc"], nodes.OrderBy(x => x.Type).ThenBy(x => x.Name).Select(x => (IReadOnlyList<string>)[x.Type.ToString(), WorkManagementService.Strip(x.Key), x.Name, x.Status, x.Provider ?? "", x.ExternalReference ?? "", x.LastObservedAtUtc.ToString("O", CultureInfo.InvariantCulture)]).ToArray()));
        return new(projectId, sheets, _clock.UtcNow);
    }

    public async Task<byte[]> ExportXlsxAsync(Guid projectId, CancellationToken ct) =>
        ManagementXlsxCodec.Write(await BuildWorkbookAsync(projectId, ct));

    public async Task<SpreadsheetImportResult> ImportXlsxAsync(
        Guid projectId,
        Stream stream,
        CancellationToken ct)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        ArgumentNullException.ThrowIfNull(stream);
        ct.ThrowIfCancellationRequested();

        // Decoding is intentionally completed before the mutation boundary.
        // Schema analysis uses SpreadsheetSchemaInferenceService and never calls
        // ApplyRowsAsync, so Analyze cannot persist project state.
        var workbook = ManagementXlsxCodec.Read(stream);
        return await ApplyRowsAsync(projectId, workbook, ct);
    }

    public async Task<GoogleSheetsSyncResult> SyncGoogleAsync(GoogleSheetsSyncCommand c, CancellationToken ct)
    {
        if (c.ProjectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(c));
        if (string.IsNullOrWhiteSpace(c.SpreadsheetId)) throw new ArgumentException("SpreadsheetId is required.", nameof(c));
        if (string.IsNullOrWhiteSpace(c.AccessToken)) throw new ArgumentException("AccessToken is required.", nameof(c));
        var warnings = new List<string>(); var pulled = 0; var pushed = 0; var conflicts = 0; SpreadsheetImportResult? imported = null;
        var workbook = await BuildWorkbookAsync(c.ProjectId, ct);
        if (!c.DryRun) await _google.EnsureSheetsAsync(c.SpreadsheetId, c.AccessToken, workbook.Sheets.Select(x => x.Name).ToArray(), ct);
        var editable = workbook.Sheets.Where(x => x.Editable).ToArray();
        var external = c.PullEditableSheets || c.DryRun
            ? await _google.PullAsync(c.SpreadsheetId, c.AccessToken, editable.Select(x => x.Name).ToArray(), ct)
            : new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase);
        var pullTables = new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase);
        var pushGrids = new List<GoogleSheetGrid>();
        var snapshots = new List<(string Sheet, string LocalHash, string ExternalHash)>();
        var resolvedApplications = new List<(string ConflictKey, string Operation, string Sheet)>();
        var syncState = await _operations.GetSyncStateAsync(c.ProjectId, ct);
        foreach (var sheet in editable)
        {
            ct.ThrowIfCancellationRequested();
            var localRows = new List<IReadOnlyList<string>> { sheet.Headers }.Concat(sheet.Rows).ToArray();
            IReadOnlyList<IReadOnlyList<string>> extRows = external.TryGetValue(sheet.Name, out var rows) ? rows : Array.Empty<IReadOnlyList<string>>();
            var localHash = GridHash(localRows); var externalHash = GridHash(extRows); var snapshot = await ReadSheetSnapshotAsync(c.ProjectId, c.SpreadsheetId, sheet.Name, ct);
            var rule = await _operations.AuthorityForAsync(c.ProjectId, $"GoogleSheets:{sheet.Name}", ct) ?? await _operations.AuthorityForAsync(c.ProjectId, sheet.Name, ct);
            var authority = rule?.Authority ?? "Manual";
             var preferAuthority = rule?.ConflictPolicy.Equals("PreferAuthority", StringComparison.OrdinalIgnoreCase) == true;
             var itemKey = $"{c.SpreadsheetId}:{sheet.Name}";
            var resolved = syncState.Conflicts.Where(x => x.Provider.Equals("GoogleSheets", StringComparison.OrdinalIgnoreCase) && x.DataType.Equals(sheet.Name, StringComparison.OrdinalIgnoreCase) && x.ItemKey.Equals(itemKey, StringComparison.OrdinalIgnoreCase) && x.Status.Equals("Resolved", StringComparison.OrdinalIgnoreCase) && x.ResolvedAtUtc.HasValue && (snapshot is null || x.ResolvedAtUtc.Value > snapshot.Value.SyncedAtUtc) && (x.Resolution is "DIP" or "External")).OrderByDescending(x => x.ResolvedAtUtc).FirstOrDefault();
            var effectiveAuthority = resolved?.Resolution == "DIP" ? "DIP" : resolved?.Resolution == "External" ? "GoogleSheets" : preferAuthority ? authority : "Manual";
            var localChanged = snapshot is null || !localHash.Equals(snapshot.Value.LocalHash, StringComparison.Ordinal); var externalChanged = snapshot is null || !externalHash.Equals(snapshot.Value.ExternalHash, StringComparison.Ordinal);
            if (localHash.Equals(externalHash, StringComparison.Ordinal))
            { snapshots.Add((sheet.Name, localHash, externalHash)); continue; }
            string operation;
            if (snapshot is null && extRows.Count == 0)
                operation = effectiveAuthority.Equals("GoogleSheets", StringComparison.OrdinalIgnoreCase) || effectiveAuthority.Equals("External", StringComparison.OrdinalIgnoreCase) ? "Pull" : "Push";
            else if (snapshot is null && localRows.Length <= 1 && extRows.Count > 1)
                operation = effectiveAuthority.Equals("DIP", StringComparison.OrdinalIgnoreCase) ? "Push" : "Pull";
            else if (localChanged && externalChanged)
            {
                if (effectiveAuthority.Equals("DIP", StringComparison.OrdinalIgnoreCase)) operation = "Push";
                else if (effectiveAuthority.Equals("GoogleSheets", StringComparison.OrdinalIgnoreCase) || effectiveAuthority.Equals("External", StringComparison.OrdinalIgnoreCase)) operation = "Pull";
                else operation = "Conflict";
            }
            else if (externalChanged)
                operation = effectiveAuthority.Equals("DIP", StringComparison.OrdinalIgnoreCase) ? "Push" : "Pull";
            else if (localChanged)
                operation = effectiveAuthority.Equals("GoogleSheets", StringComparison.OrdinalIgnoreCase) || effectiveAuthority.Equals("External", StringComparison.OrdinalIgnoreCase) ? "Pull" : "Push";
            else operation = "None";
            if (operation == "Pull" && !c.PullEditableSheets) { warnings.Add($"{sheet.Name}: external change detected but PullEditableSheets is disabled."); continue; }
            if (operation == "Push" && !c.PushCalculatedSheets) { warnings.Add($"{sheet.Name}: DIP change detected but push is disabled for this run."); continue; }
            if (operation == "Conflict")
            {
                conflicts++; warnings.Add($"{sheet.Name}: both DIP and Google Sheets changed since the last sync. Resolve the conflict or set a Source Authority before retrying.");
                if (!c.DryRun) await _operations.RecordSyncConflictAsync(c.ProjectId, "GoogleSheets", sheet.Name, itemKey, $"sha256:{localHash}", $"sha256:{externalHash}", ct);
                continue;
            }
            if (operation == "Pull")
            { pullTables[sheet.Name] = extRows;
             pulled++;
             snapshots.Add((sheet.Name, externalHash, externalHash));
            if (resolved is not null) resolvedApplications.Add((resolved.Key, "Pull", sheet.Name));
             }
            else if (operation == "Push")
            { pushGrids.Add(new(sheet.Name, localRows));
             pushed++;
             snapshots.Add((sheet.Name, localHash, localHash));
            if (resolved is not null) resolvedApplications.Add((resolved.Key, "Push", sheet.Name));
             }
            else snapshots.Add((sheet.Name, localHash, externalHash));
        }
        var pullVerified = true;
        if (!c.DryRun && pullTables.Count > 0) { imported = await ApplyRowsAsync(c.ProjectId, pullTables, ct);
         warnings.AddRange(imported.Warnings);
         pullVerified = imported.Warnings.Count == 0;
        if (!pullVerified) { snapshots.RemoveAll(x => pullTables.ContainsKey(x.Sheet));
         warnings.Add("Google Sheets pull completed with import warnings; affected sheet snapshots and resolved-conflict states were not advanced so the operation can be reviewed and retried.");
         } }
        if (!c.DryRun && pushGrids.Count > 0) await _google.PushAsync(c.SpreadsheetId, c.AccessToken, pushGrids, ct);
        if (!c.DryRun)
        foreach (var resolved in resolvedApplications.Where(x => x.Operation == "Push" || (x.Operation == "Pull" && pullVerified))) await _operations.MarkConflictAppliedAsync(c.ProjectId, resolved.ConflictKey, ct);
        if (c.PushCalculatedSheets)
        {
            var calculated = workbook.Sheets.Where(x => !x.Editable).Select(x => new GoogleSheetGrid(x.Name, new List<IReadOnlyList<string>> { x.Headers }.Concat(x.Rows).ToArray())).ToArray();
            if (!c.DryRun && calculated.Length > 0) await _google.PushAsync(c.SpreadsheetId, c.AccessToken, calculated, ct); pushed += calculated.Length;
        }
        if (!c.DryRun)
        foreach (var snapshot in snapshots) await WriteSheetSnapshotAsync(c.ProjectId, c.SpreadsheetId, snapshot.Sheet, snapshot.LocalHash, snapshot.ExternalHash, ct);
        return new(pulled, pushed, imported, warnings.Distinct().Take(100).ToArray(), _clock.UtcNow, conflicts, c.DryRun);
    }

    private async Task<(string LocalHash, string ExternalHash, DateTimeOffset SyncedAtUtc)?> ReadSheetSnapshotAsync(Guid projectId, string spreadsheetId, string sheet, CancellationToken ct)
    {
        var node = await _repo.FindNodeAsync(projectId, DevelopmentGraphNodeType.ExternalSyncState, SheetSnapshotKey(spreadsheetId, sheet), ct); if (node is null) return null;
        try { using var doc = JsonDocument.Parse(node.MetadataJson);
         var root = doc.RootElement;
         var local = root.TryGetProperty("LocalHash", out var l) ? l.GetString() : null;
         var external = root.TryGetProperty("ExternalHash", out var e) ? e.GetString() : null;
         var synced = root.TryGetProperty("SyncedAtUtc", out var at) && at.TryGetDateTimeOffset(out var parsed) ? parsed : node.LastObservedAtUtc;
         return string.IsNullOrWhiteSpace(local) || string.IsNullOrWhiteSpace(external) ? null : (local, external, synced);
         }
        catch { return null;
         }
    }
    private async Task WriteSheetSnapshotAsync(Guid projectId, string spreadsheetId, string sheet, string localHash, string externalHash, CancellationToken ct)
    {
        var metadata = JsonSerializer.Serialize(new { Kind = "GoogleSheetsSnapshot", SpreadsheetId = spreadsheetId, Sheet = sheet, LocalHash = localHash, ExternalHash = externalHash, SyncedAtUtc = _clock.UtcNow });
        await _graph.UpsertNodeAsync(new(projectId, DevelopmentGraphNodeType.ExternalSyncState, SheetSnapshotKey(spreadsheetId, sheet), $"Google Sheets snapshot {sheet}", "Active", "GoogleSheets", spreadsheetId, metadata), ct);
    }
    private static string SheetSnapshotKey(string spreadsheetId, string sheet)
    {
        var raw = $"{spreadsheetId}:{sheet}"; var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()[..24]; return $"sheet-sync:{hash}";
    }
    private static string GridHash(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var normalized = string.Join("\n", rows.Select(r => string.Join("\u001f", r.Select(x => (x ?? string.Empty).TrimEnd())))); return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    public async Task<SpreadsheetImportResult> ApplyRowsAsync(Guid projectId, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> tables, CancellationToken ct)
    {
        var warnings = new List<string>();
         var teamCount = 0;
         var wsCount = 0;
         var taskCount = 0;
         var milestoneCount = 0;
         var blockerCount = 0;
         var reqCount = 0;
         var riskCount = 0;
         var decisionCount = 0;
         var actionCount = 0;
         var changeCount = 0;
         var allocationCount = 0;
        foreach (var row in Data(tables, "Teams")) { try { var poolCap = Dec(row, "SharedPoolCapacityHoursPerWeek", 0m);
         await _work.UpsertTeamAsync(new(projectId, V(row, "Key"), V(row, "Name"), Dec(row, "CapacityHoursPerWeek"), Dec(row, "BufferPercent", 10m), V(row, "Status", "Active"), null, Null(V(row, "SharedResourceKey")), poolCap > 0m ? poolCap : null), ct);
         teamCount++;
         }
        catch (Exception ex) { warnings.Add("Teams: " + ex.Message);
         } }
        foreach (var row in Data(tables, "Workstreams")) { try { await _work.UpsertWorkstreamAsync(new(projectId, V(row, "Key"), V(row, "Name"), V(row, "TeamKey"), Dec(row, "PlannedCapacityHoursPerWeek"), Date(row, "PlannedStart"), Date(row, "Due"), Dec(row, "CompletionPercent"), V(row, "Status", "Active")), ct);
         wsCount++;
         }
        catch (Exception ex) { warnings.Add("Workstreams: " + ex.Message);
         } }
        foreach (var row in Data(tables, "Tasks")) { try { await _work.UpsertTaskAsync(new(projectId, V(row, "Key"), V(row, "Name"), V(row, "WorkstreamKey"), Dec(row, "EstimateHours"), Dec(row, "RemainingHours"), Date(row, "PlannedStart"), Date(row, "Due"), Dec(row, "CompletionPercent"), V(row, "Status", "Planned"), V(row, "Priority", "Medium"), Null(V(row, "OwnerLabel")), Null(V(row, "ExternalReference"))), ct);
         taskCount++;
         }
        catch (Exception ex) { warnings.Add("Tasks: " + ex.Message);
         } }
        foreach (var row in Data(tables, "Milestones")) { try { var state = Enum.TryParse<MilestoneState>(V(row, "State", "Planned"), true, out var parsed) ? parsed : MilestoneState.Planned;
         await _milestones.UpsertAsync(new(projectId, V(row, "Key"), V(row, "Name"), Date(row, "PlannedStart"), Date(row, "Due"), (int)Dec(row, "DurationDays", 1m), Dec(row, "CompletionPercent"), state, Null(V(row, "ExternalReference"))), ct);
         milestoneCount++;
         }
        catch (Exception ex) { warnings.Add("Milestones: " + ex.Message);
         } }
        foreach (var row in Data(tables, "Blockers")) { try { await _work.UpsertBlockerAsync(new(projectId, V(row, "Key"), V(row, "Title"), V(row, "Reason"), V(row, "Severity", "Medium"), Date(row, "OpenedAt", _clock.UtcNow), V(row, "Status", "Open"), Split(row, "BlockedTasks"), Split(row, "BlockedWorkstreams"), Split(row, "BlockedMilestones")), ct);
         blockerCount++;
         }
        catch (Exception ex) { warnings.Add("Blockers: " + ex.Message);
         } }
        foreach (var row in Data(tables, "Requirements")) { try { await _work.UpsertRequirementAsync(new(projectId, V(row, "Key"), V(row, "Title"), V(row, "Priority", "Medium"), V(row, "Status", "Planned"), V(row, "ScopeState", "Included"), Dec(row, "CompletionPercent"), Null(V(row, "AcceptanceSummary")), Null(V(row, "ExternalReference"))), ct);
         reqCount++;
         }
        catch (Exception ex) { warnings.Add("Requirements: " + ex.Message);
         } }
        foreach (var row in Data(tables, "Dependencies")) { try { var relation = V(row, "Relation", "Blocks");
         var sourceType = V(row, "SourceType");
         var targetType = V(row, "TargetType");
        if (relation.Equals("ScheduledFor", StringComparison.OrdinalIgnoreCase) && sourceType.Equals("Task", StringComparison.OrdinalIgnoreCase) && targetType.Equals("Milestone", StringComparison.OrdinalIgnoreCase)) await _work.LinkTaskToMilestoneAsync(new(projectId, V(row, "SourceKey"), V(row, "TargetKey")), ct);
         else if (relation.Equals("Satisfies", StringComparison.OrdinalIgnoreCase) && targetType.Equals("Requirement", StringComparison.OrdinalIgnoreCase)) await _work.LinkRequirementAsync(new(projectId, V(row, "TargetKey"), sourceType, V(row, "SourceKey")), ct);
         else await _work.LinkDependencyAsync(new(projectId, sourceType, V(row, "SourceKey"), targetType, V(row, "TargetKey"), V(row, "Critical", "No").Equals("Yes", StringComparison.OrdinalIgnoreCase)), ct);
         }
        catch (Exception ex) { warnings.Add("Dependencies: " + ex.Message);
         } }
        foreach (var row in Data(tables, "ScopeBaselines")) { try { var key = V(row, "Key");
        if (!string.IsNullOrWhiteSpace(key)) await _work.CreateScopeBaselineAsync(new(projectId, key, V(row, "Name", key), Date(row, "CapturedAt", _clock.UtcNow)), ct);
         }
        catch (Exception ex) { warnings.Add("ScopeBaselines: " + ex.Message);
         } }
        foreach (var row in Data(tables, "Risks")) { try { await _work.UpsertRiskAsync(new(projectId, V(row, "Key"), V(row, "Title"), V(row, "Description"), Dec(row, "ProbabilityPercent"), Dec(row, "ImpactPercent"), V(row, "Status", "Open"), V(row, "Category", "Delivery"), Null(V(row, "OwnerRole")), NullableDate(row, "ReviewDue"), Null(V(row, "Trigger")), Null(V(row, "Mitigation")), ParseReferences(V(row, "AffectedItems"))), ct);
         riskCount++;
         }
        catch (Exception ex) { warnings.Add("Risks: " + ex.Message);
         } }
        foreach (var row in Data(tables, "Decisions")) { try { await _work.UpsertManagementDecisionAsync(new(projectId, V(row, "Key"), V(row, "Title"), V(row, "Decision"), V(row, "Rationale"), V(row, "Status", "Proposed"), Null(V(row, "DecisionOwnerRole")), NullableDate(row, "DecisionDue"), NullableDate(row, "DecidedAt"), ParseReferences(V(row, "RelatedItems"))), ct);
         decisionCount++;
         }
        catch (Exception ex) { warnings.Add("Decisions: " + ex.Message);
         } }
        foreach (var row in Data(tables, "Actions")) { try { ManagementReference? target = null;
         var targetType = V(row, "TargetType");
         var targetKey = V(row, "TargetKey");
        if (!string.IsNullOrWhiteSpace(targetType) && !string.IsNullOrWhiteSpace(targetKey)) target = new(targetType, targetKey);
         await _work.UpsertActionItemAsync(new(projectId, V(row, "Key"), V(row, "Title"), V(row, "Status", "Open"), V(row, "Priority", "Medium"), Null(V(row, "OwnerRole")), NullableDate(row, "Due"), Dec(row, "CompletionPercent"), Null(V(row, "DecisionKey")), target), ct);
         actionCount++;
         }
        catch (Exception ex) { warnings.Add("Actions: " + ex.Message);
         } }
        foreach (var row in Data(tables, "ChangeRequests")) { try { await _work.UpsertChangeRequestAsync(new(projectId, V(row, "Key"), V(row, "Title"), V(row, "Description"), V(row, "Status", "Proposed"), V(row, "Priority", "Medium"), Null(V(row, "RequestedByRole")), NullableDate(row, "RequestedAt"), Dec(row, "EngineeringHoursDelta"), Dec(row, "ArtHoursDelta"), Dec(row, "QaHoursDelta"), (int)Dec(row, "ScheduleDaysDelta"), Dec(row, "RiskDeltaPercent"), Split(row, "RequirementKeys"), Split(row, "WorkstreamKeys"), Null(V(row, "DecisionKey"))), ct);
         changeCount++;
         }
        catch (Exception ex) { warnings.Add("ChangeRequests: " + ex.Message);
         } }
        foreach (var row in Data(tables, "ResourceAllocations")) { try { await _work.UpsertResourceAllocationAsync(new(projectId, V(row, "Key"), V(row, "ToWorkstreamKey"), Dec(row, "CapacityHoursPerWeek"), Date(row, "Start"), Date(row, "End"), V(row, "Status", "Proposed"), Null(V(row, "FromWorkstreamKey")), Null(V(row, "Rationale")), Null(V(row, "DecisionKey"))), ct);
         allocationCount++;
         }
        catch (Exception ex) { warnings.Add("ResourceAllocations: " + ex.Message);
         } }
        return new(teamCount, wsCount, taskCount, milestoneCount, blockerCount, reqCount, riskCount, decisionCount, actionCount, changeCount, allocationCount, warnings.Take(100).ToArray());
    }

    private static SpreadsheetSheetDto BuildWorkloadSheet(CapacityOverviewDto capacity)
    {
        var starts = capacity.Teams.SelectMany(x => x.Weekly).Select(x => x.WeekStartUtc).Concat(capacity.Workstreams.SelectMany(x => x.Weekly).Select(x => x.WeekStartUtc)).Distinct().OrderBy(x => x).ToArray();
         var headers = new List<string> { "Level", "Key", "Name" };
         headers.AddRange(starts.Select(x => x.ToString("MM-dd", CultureInfo.InvariantCulture)));
         var rows = new List<IReadOnlyList<string>>();
        foreach (var t in capacity.Teams) { var row = new List<string> { "Team", t.Key, t.Name };
         row.AddRange(starts.Select(s => F(t.Weekly.FirstOrDefault(w => w.WeekStartUtc == s)?.UtilizationPercent ?? 0m)));
         rows.Add(row);
         }
        foreach (var w in capacity.Workstreams) { var row = new List<string> { "Workstream", w.Key, w.Name };
         row.AddRange(starts.Select(s => F(w.Weekly.FirstOrDefault(b => b.WeekStartUtc == s)?.UtilizationPercent ?? 0m)));
         rows.Add(row);
         }
        return Sheet("Workload", headers, rows);
    }

    private static SpreadsheetSheetDto BuildGanttSheet(ManagementScheduleDto schedule)
    {
        if (schedule.Items.Count == 0) return Sheet("Gantt", ["Type", "Key", "Name"], []);
        var start = StartOfWeek(schedule.Items.Min(x => x.PlannedStartUtc));
         var end = schedule.Items.Max(x => x.ForecastFinishUtc);
         var weeks = Math.Clamp((int)Math.Ceiling(Math.Max(1d, (end - start).TotalDays) / 7d), 1, 26);
         var headers = new List<string> { "Type", "Key", "Name" };
        for (var i = 0;
         i < weeks;
         i++) headers.Add(start.AddDays(i * 7).ToString("MM-dd", CultureInfo.InvariantCulture));
         var rows = new List<IReadOnlyList<string>>();
        foreach (var item in schedule.Items.OrderBy(x => x.PlannedStartUtc).ThenBy(x => x.Name)) { var row = new List<string> { item.Type, item.Key, item.Name };
        for (var i = 0;
         i < weeks;
         i++) { var ws = start.AddDays(i * 7);
         var we = ws.AddDays(7);
         row.Add(item.PlannedStartUtc < we && item.ForecastFinishUtc >= ws ? (item.IsCritical ? "CRITICAL" : "■") : "");
         } rows.Add(row);
         }
        return Sheet("Gantt", headers, rows);
    }
    private static DateTimeOffset StartOfWeek(DateTimeOffset value) { var d = value.UtcDateTime.Date;
     var offset = ((int)d.DayOfWeek + 6) % 7;
     return new DateTimeOffset(d.AddDays(-offset), TimeSpan.Zero);
     }
    private static SpreadsheetSheetDto Sheet(string name, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, bool editable = false) => new(name, headers, rows, editable);
    private static IEnumerable<IReadOnlyDictionary<string, string>> Data(IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> tables, string name) { if (!tables.TryGetValue(name, out var rows) || rows.Count < 2) return [];
     var headers = rows[0].Select(x => x.Trim()).ToArray();
     return rows.Skip(1).Where(r => r.Any(x => !string.IsNullOrWhiteSpace(x))).Select(r => (IReadOnlyDictionary<string, string>)headers.Select((h, i) => (h, v: i < r.Count ? r[i] : "")).Where(x => !string.IsNullOrWhiteSpace(x.h)).ToDictionary(x => x.h, x => x.v, StringComparer.OrdinalIgnoreCase)).ToArray();
     }
    private static string V(IReadOnlyDictionary<string, string> row, string key, string fallback = "") => row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
    private static decimal Dec(IReadOnlyDictionary<string, string> row, string key, decimal fallback = 0m) => decimal.TryParse(V(row, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : decimal.TryParse(V(row, key), NumberStyles.Any, CultureInfo.CurrentCulture, out x) ? x : fallback;
    private static DateTimeOffset Date(IReadOnlyDictionary<string, string> row, string key) => Date(row, key, DateTimeOffset.UtcNow);
    private static DateTimeOffset Date(IReadOnlyDictionary<string, string> row, string key, DateTimeOffset fallback) => DateTimeOffset.TryParse(V(row, key), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var x) ? x : DateTimeOffset.TryParse(V(row, key), out x) ? x : fallback;
    private static DateTimeOffset? NullableDate(IReadOnlyDictionary<string, string> row, string key) => DateTimeOffset.TryParse(V(row, key), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var x) ? x : DateTimeOffset.TryParse(V(row, key), out x) ? x : null;
    private static IReadOnlyList<ManagementReference> ParseReferences(string value) => value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.Split(':', 2, StringSplitOptions.TrimEntries)).Where(x => x.Length == 2 && !string.IsNullOrWhiteSpace(x[0]) && !string.IsNullOrWhiteSpace(x[1])).Select(x => new ManagementReference(x[0], x[1])).ToArray();
    private static ManagementReference? ReadReference(Dip.Domain.Entities.DevelopmentGraphNode node, string name) { try { using var d = System.Text.Json.JsonDocument.Parse(node.MetadataJson);
    if (!d.RootElement.TryGetProperty(name, out var v) || v.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
    if (!v.TryGetProperty("Type", out var t) || !v.TryGetProperty("Key", out var k)) return null;
     var type = t.GetString();
     var key = k.GetString();
     return string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(key) ? null : new(type, key);
     }
    catch { return null;
     } }
    private static string[] Split(IReadOnlyDictionary<string, string> row, string key) => V(row, key).Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string? Null(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
    private static string F(decimal x) => x.ToString("0.##", CultureInfo.InvariantCulture);
    private static string D(DateTimeOffset x) => x == DateTimeOffset.MaxValue ? "" : x.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
