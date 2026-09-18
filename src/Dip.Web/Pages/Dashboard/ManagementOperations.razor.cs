// Purpose: Implements DIP web behavior for M an ag em en tO pe ra ti on s.
using Dip.Application.Features.Management;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Dip.Web.Pages.Dashboard;

public partial class ManagementOperations
{
    [Parameter] public Guid ProjectId { get; set; }

    private readonly string[] _tabs = ["Alerts & approvals", "Risk / decisions / actions", "Scope & resources", "RACI & calendar", "Sync & source of truth"];
    private string _tab = "Alerts & approvals";
    private bool _loading = true;
    private string? _error;
    private string? _notice;

    private DailyCockpitDto? _cockpit;
    private ManagementDashboardDto? _management;
    private RaciOverviewDto? _raci;
    private ManagementRoleDirectoryDto? _roles;
    private WorkCalendarOverviewDto? _calendars;
    private ApprovalOverviewDto? _approvals;
    private SyncStateOverviewDto? _sync;
    private OperatingCadenceDto? _cadence;
    private ManagementSyncResult? _lastSync;
    private GoogleSheetsSyncResult? _lastSheets;
    private GoogleCalendarSyncResult? _lastCalendarSync;

    private string _cockpitOwnerRole = "", _sheetId = "", _sheetToken = "", _gcalId = "primary", _gcalToken = "";
    private DateTime _gcalFrom = DateTime.Today, _gcalTo = DateTime.Today.AddDays(56);
    private string _riskKey = "", _riskTitle = "", _riskDescription = "", _riskOwner = "", _riskStatus = "Open", _riskCategory = "Delivery", _riskTrigger = "", _riskMitigation = "", _riskAffected = "";
    private decimal _riskProbability = 50, _riskImpact = 50;
    private DateTime _riskReview = DateTime.Today.AddDays(7).AddHours(17);
    private string _decisionKey = "", _decisionTitle = "", _decisionValue = "", _decisionRationale = "", _decisionOwner = "", _decisionStatus = "Proposed", _decisionRelated = "";
    private DateTime _decisionDue = DateTime.Today.AddDays(1).AddHours(17);
    private DateTime? _decisionDecided;
    private string _actionKey = "", _actionTitle = "", _actionPriority = "Medium", _actionStatus = "Open", _actionOwner = "", _actionDecisionKey = "", _actionTargetType = "", _actionTargetKey = "";
    private DateTime _actionDue = DateTime.Today.AddDays(1).AddHours(17);
    private decimal _actionCompletion;
    private string _changeKey = "", _changeTitle = "", _changeDescription = "", _changePriority = "Medium", _changeStatus = "Proposed", _changeRequestedBy = "", _changeRequirementKeys = "", _changeWorkstreamKeys = "", _changeDecisionKey = "";
    private decimal _changeEng, _changeArt, _changeQa, _changeRisk;
    private int _changeDays;
    private DateTime? _changeRequestedAt;
    private string _allocationKey = "", _allocationFrom = "", _allocationTo = "", _allocationStatus = "Proposed", _allocationRationale = "", _allocationDecisionKey = "";
    private decimal _allocationHours = 8;
    private DateTime _allocationStart = DateTime.Today, _allocationEnd = DateTime.Today.AddDays(14);
    private string _poolTeamKey = "", _sharedResourceKey = "";
    private decimal _sharedPoolCapacity = 40m;
    private string _raciKey = "", _raciType = "action", _raciTarget = "", _raciR = "", _raciA = "", _raciC = "", _raciI = "";
    private string _roleBindingKey = "", _roleBindingActor = "", _roleBindingRole = "", _roleBindingName = "";
    private string _calendarKey = "default", _calendarName = "Project calendar", _calendarTeam = "", _calendarExceptions = "";
    private decimal _calendarHoursDay = 8, _calendarMeetings, _calendarSupport;
    private string _approvalKey = "", _approvalTitle = "", _approvalType = "Release", _approvalTargetType = "release", _approvalTargetKey = "", _approvalRoles = "Project Manager,Tech Lead", _approvalActorRole = "Project Manager";
    private DateTime _approvalDue = DateTime.Today.AddDays(1).AddHours(17);
    private string _authorityKey = "actions", _authorityDataType = "Action", _authority = "Manual", _conflictPolicy = "Manual", _syncProvider = "GitHub", _syncDirection = "Bidirectional";

    // Flexible spreadsheet intake is deliberately kept in this existing management page.
    // The workbook is buffered only for the duration of the interactive circuit and is
    // analyzed/dry-run before the user applies a mutation.
    private const long MaxSpreadsheetBytes = 25L * 1024L * 1024L;
    private byte[]? _spreadsheetBytes;
    private string? _spreadsheetFileName;
    private string _spreadsheetDomainOverride = "";
    private bool _spreadsheetBusy;
    private string? _spreadsheetError;
    private SpreadsheetSchemaAnalysisDto? _spreadsheetAnalysis;
    private FlexibleSpreadsheetImportResult? _spreadsheetPreview;
    private FlexibleSpreadsheetImportResult? _spreadsheetImport;
    private bool CanUseSpreadsheet => _spreadsheetBytes is { Length: > 0 };

    protected override Task OnParametersSetAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            var cockpit = Api.GetDailyCockpitAsync(ProjectId, 8, Null(_cockpitOwnerRole));
            var mgmt = Api.GetManagementDashboardAsync(ProjectId);
            var raci = Api.GetRaciAsync(ProjectId);
            var roles = Api.GetManagementRolesAsync(ProjectId);
            var calendars = Api.GetWorkCalendarsAsync(ProjectId);
            var approvals = Api.GetApprovalsAsync(ProjectId);
            var sync = Api.GetManagementSyncStateAsync(ProjectId);
            await Task.WhenAll(cockpit, mgmt, raci, roles, calendars, approvals, sync);
            _cockpit = await cockpit;
            _management = await mgmt;
            _raci = await raci;
            _roles = await roles;
            if (string.IsNullOrWhiteSpace(_roleBindingActor)) _roleBindingActor = _roles.CurrentActor;
            var boundRoles = CurrentActorRoles;
            if (boundRoles.Count == 0) _approvalActorRole = "";
            else if (!boundRoles.Contains(_approvalActorRole, StringComparer.OrdinalIgnoreCase)) _approvalActorRole = boundRoles[0];
            _calendars = await calendars;
            _approvals = await approvals;
            _sync = await sync;
            _cadence = await Api.GetOperatingCadenceAsync(ProjectId, "daily");
        }
        catch (Exception ex) { _error = ex.Message; }
        finally { _loading = false; }
    }

    private async Task Mutate(Func<Task> action, string message)
    {
        _notice = null;
        _error = null;
        try { await action(); _notice = message; await LoadAsync(); }
        catch (Exception ex) { _error = ex.Message; }
    }

    private Task LoadCadence(string mode) => Mutate(async () => _cadence = await Api.GetOperatingCadenceAsync(ProjectId, mode), $"Loaded {mode} review.");
    private Task SaveRisk() => Mutate(() => Api.UpsertRiskAsync(new(ProjectId, _riskKey, _riskTitle, _riskDescription, _riskProbability, _riskImpact, _riskStatus, _riskCategory, Null(_riskOwner), Utc(_riskReview), Null(_riskTrigger), Null(_riskMitigation), ParseReferences(_riskAffected), null)), "Risk saved.");
    private Task SaveDecision() => Mutate(() => Api.UpsertManagementDecisionAsync(new(ProjectId, _decisionKey, _decisionTitle, _decisionValue, _decisionRationale, _decisionStatus, Null(_decisionOwner), Utc(_decisionDue), UtcNullable(_decisionDecided), ParseReferences(_decisionRelated), null)), "Decision saved.");
    private Task SaveAction() => Mutate(() => Api.UpsertManagementActionAsync(new(ProjectId, _actionKey, _actionTitle, _actionStatus, _actionPriority, Null(_actionOwner), Utc(_actionDue), Math.Clamp(_actionStatus == "Completed" ? 100m : _actionCompletion, 0m, 100m), Null(_actionDecisionKey), ActionTarget(), null)), "Action saved.");
    private Task SaveChange() => Mutate(() => Api.UpsertChangeRequestAsync(new(ProjectId, _changeKey, _changeTitle, _changeDescription, _changeStatus, _changePriority, Null(_changeRequestedBy), UtcNullable(_changeRequestedAt), _changeEng, _changeArt, _changeQa, _changeDays, _changeRisk, SplitList(_changeRequirementKeys), SplitList(_changeWorkstreamKeys), Null(_changeDecisionKey), null)), "Scope change saved.");
    private Task SaveAllocation() => Mutate(() => Api.UpsertResourceAllocationAsync(new(ProjectId, _allocationKey, _allocationTo, _allocationHours, Utc(_allocationStart), Utc(_allocationEnd), _allocationStatus, Null(_allocationFrom), Null(_allocationRationale), Null(_allocationDecisionKey))), "Allocation saved.");
    private Task SaveSharedPool() => Mutate(() => Api.UpsertSharedResourceMembershipAsync(ProjectId, new(ProjectId, _poolTeamKey, _sharedResourceKey, _sharedPoolCapacity)), "Shared resource pool membership saved.");
    private Task ClearSharedPool() => Mutate(() => Api.UpsertSharedResourceMembershipAsync(ProjectId, new(ProjectId, _poolTeamKey, null, null)), "Shared resource pool membership removed.");
    private Task SaveRaci() => Mutate(() => Api.UpsertRaciAsync(ProjectId, new(ProjectId, _raciKey, new(_raciType, _raciTarget), Null(_raciR), Null(_raciA), Split(_raciC), Split(_raciI))), "RACI saved.");
    private Task SaveRoleBinding() => Mutate(() => Api.UpsertManagementRoleAsync(ProjectId, new(ProjectId, _roleBindingKey, _roleBindingActor, _roleBindingRole, Null(_roleBindingName))), "Management role binding saved.");
    private Task SaveCalendar() => Mutate(() => Api.UpsertWorkCalendarAsync(ProjectId, new(ProjectId, _calendarKey, _calendarName, Null(_calendarTeam), _calendarHoursDay, null, _calendarMeetings, _calendarSupport, ParseExceptions(_calendarExceptions), "Asia/Tokyo")), "Work calendar saved and capacity recalculated.");
    private Task RunGoogleCalendar(bool dry) => Mutate(async () => { _lastCalendarSync = await Api.SyncManagementGoogleCalendarAsync(ProjectId, new(ProjectId, _calendarKey, _gcalId, _gcalToken, Utc(_gcalFrom), Utc(_gcalTo), Null(_calendarTeam), "Asia/Tokyo", dry)); if (!dry) _gcalToken = ""; }, dry ? "Google Calendar dry run completed." : "Google Calendar synchronized into capacity.");
    private Task CreateApproval() => Mutate(() => Api.CreateApprovalAsync(ProjectId, new(ProjectId, _approvalKey, _approvalType, _approvalTitle, new(_approvalTargetType, _approvalTargetKey), Split(_approvalRoles), "web", null, Utc(_approvalDue))), "Approval gate created.");
    private Task Respond(ApprovalRequestDto approval, bool approve) => Mutate(() => Api.RespondApprovalAsync(ProjectId, approval.Key, new(ProjectId, approval.Key, "web", _approvalActorRole, approve ? "Approve" : "Reject", null)), approve ? "Approval recorded." : "Rejection recorded.");
    private Task Alert(string key, string disposition) => Mutate(() => Api.SetAlertDispositionAsync(ProjectId, key, new(ProjectId, key, "web", disposition, disposition == "snooze" ? DateTimeOffset.UtcNow.AddHours(2) : null, null)), "Alert state updated.");
    private Task SaveAuthority() => Mutate(() => Api.UpsertSourceAuthorityAsync(ProjectId, new(ProjectId, _authorityKey, _authorityDataType, _authority, _conflictPolicy)), "Source authority saved.");
    private Task RunSync(bool dry) => Mutate(async () => _lastSync = await Api.RunManagementSyncAsync(ProjectId, new(ProjectId, _syncProvider, Enum.Parse<ManagementSyncDirection>(_syncDirection), dry)), dry ? "Sync dry run completed." : "Synchronization completed.");
    private Task RunSheets(bool dry) => Mutate(async () => { _lastSheets = await Api.SyncManagementGoogleSheetsAsync(new(ProjectId, _sheetId, _sheetToken, true, true, dry)); if (!dry) _sheetToken = ""; }, dry ? "Google Sheets dry run completed." : "Google Sheets synchronized with conflict protection.");
    private Task ResolveConflict(string key, string resolution) => Mutate(() => Api.ResolveManagementSyncConflictAsync(ProjectId, key, new(ProjectId, key, resolution, "web")), "Sync conflict resolved.");
    private Task Archive(string type, string key) => Mutate(() => Api.ArchiveManagementItemAsync(ProjectId, new(ProjectId, type, key, "web", "Archived from management GUI")), "Item archived with history retained.");

    private async Task OnSpreadsheetSelected(InputFileChangeEventArgs args)
    {
        _spreadsheetError = null;
        _spreadsheetAnalysis = null;
        _spreadsheetPreview = null;
        _spreadsheetImport = null;
        var file = args.File;
        _spreadsheetFileName = file.Name;
        if (file.Size <= 0 || file.Size > MaxSpreadsheetBytes)
        {
            _spreadsheetBytes = null;
            _spreadsheetError = $"Workbook must be between 1 byte and {FormatBytes((int)MaxSpreadsheetBytes)}.";
            return;
        }

        try
        {
            await using var input = file.OpenReadStream(MaxSpreadsheetBytes);
            using var memory = new MemoryStream((int)Math.Min(file.Size, MaxSpreadsheetBytes));
            await input.CopyToAsync(memory);
            _spreadsheetBytes = memory.ToArray();
        }
        catch (Exception ex)
        {
            _spreadsheetBytes = null;
            _spreadsheetError = ex.Message;
        }
    }

    private async Task AnalyzeSpreadsheetAsync()
    {
        await RunSpreadsheetOperation(async () =>
        {
            var bytes = _spreadsheetBytes ?? throw new InvalidOperationException("Choose an XLSX workbook first.");
            _spreadsheetAnalysis = await Api.AnalyzeManagementSpreadsheetAsync(
                ProjectId,
                SpreadsheetFileName(),
                bytes);
            _spreadsheetPreview = null;
            _spreadsheetImport = null;
            _notice = "Spreadsheet schema analysis completed without mutating project state.";
        });
    }

    private async Task PreviewSpreadsheetAsync()
    {
        await RunSpreadsheetOperation(async () =>
        {
            var bytes = _spreadsheetBytes ?? throw new InvalidOperationException("Choose an XLSX workbook first.");
            _spreadsheetPreview = await Api.ImportFlexibleManagementSpreadsheetAsync(
                ProjectId,
                SpreadsheetFileName(),
                bytes,
                dryRun: true,
                domainId: Null(_spreadsheetDomainOverride));
            _spreadsheetAnalysis = _spreadsheetPreview.Analysis;
            _spreadsheetImport = null;
            _notice = "Flexible spreadsheet dry run completed; no project state was changed.";
        });
    }

    private async Task ImportSpreadsheetAsync()
    {
        await RunSpreadsheetOperation(async () =>
        {
            var bytes = _spreadsheetBytes ?? throw new InvalidOperationException("Choose an XLSX workbook first.");
            _spreadsheetImport = await Api.ImportFlexibleManagementSpreadsheetAsync(
                ProjectId,
                SpreadsheetFileName(),
                bytes,
                dryRun: false,
                domainId: Null(_spreadsheetDomainOverride));
            _spreadsheetAnalysis = _spreadsheetImport.Analysis;
            _spreadsheetPreview = null;
            _notice = $"Flexible spreadsheet import applied: {_spreadsheetImport.Imported} row(s), {_spreadsheetImport.DependencyLinks} dependency link(s).";
            await LoadAsync();
        });
    }

    private async Task RunSpreadsheetOperation(Func<Task> operation)
    {
        if (!CanUseSpreadsheet)
        {
            _spreadsheetError = "Choose an XLSX workbook first.";
            return;
        }

        _spreadsheetBusy = true;
        _spreadsheetError = null;
        _notice = null;
        try { await operation(); }
        catch (Exception ex) { _spreadsheetError = ex.Message; }
        finally { _spreadsheetBusy = false; }
    }

    private string SpreadsheetFileName()
        => string.IsNullOrWhiteSpace(_spreadsheetFileName) ? "management.xlsx" : _spreadsheetFileName;

    private void EditRisk(RiskRegisterItemDto risk) { _riskKey = risk.Key; _riskTitle = risk.Title; _riskDescription = risk.Description; _riskStatus = risk.Status; _riskCategory = risk.Category; _riskProbability = risk.ProbabilityPercent; _riskImpact = risk.ImpactPercent; _riskOwner = risk.OwnerRole ?? ""; _riskTrigger = risk.Trigger ?? ""; _riskMitigation = risk.Mitigation ?? ""; _riskAffected = string.Join(", ", risk.AffectedItems); if (risk.ReviewDueUtc is not null) _riskReview = risk.ReviewDueUtc.Value.LocalDateTime; }
    private void EditDecision(ManagementDecisionDto decision) { _decisionKey = decision.Key; _decisionTitle = decision.Title; _decisionValue = decision.Decision; _decisionRationale = decision.Rationale; _decisionStatus = decision.Status; _decisionOwner = decision.DecisionOwnerRole ?? ""; _decisionRelated = string.Join(", ", decision.RelatedItems); _decisionDecided = decision.DecidedAtUtc?.LocalDateTime; if (decision.DecisionDueUtc is not null) _decisionDue = decision.DecisionDueUtc.Value.LocalDateTime; }
    private void EditAction(ManagementActionDto action) { _actionKey = action.Key; _actionTitle = action.Title; _actionPriority = action.Priority; _actionStatus = action.Status; _actionOwner = action.OwnerRole ?? ""; _actionCompletion = action.CompletionPercent; _actionDecisionKey = action.DecisionKey ?? ""; _actionTargetType = action.TargetType ?? ""; _actionTargetKey = action.TargetKey ?? ""; if (action.DueAtUtc is not null) _actionDue = action.DueAtUtc.Value.LocalDateTime; }
    private void EditChange(ChangeRequestDto change) { _changeKey = change.Key; _changeTitle = change.Title; _changeDescription = change.Description; _changePriority = change.Priority; _changeStatus = change.Status; _changeRequestedBy = change.RequestedByRole ?? ""; _changeRequestedAt = change.RequestedAtUtc.LocalDateTime; _changeEng = change.EngineeringHoursDelta; _changeArt = change.ArtHoursDelta; _changeQa = change.QaHoursDelta; _changeDays = change.ScheduleDaysDelta; _changeRisk = change.RiskDeltaPercent; _changeRequirementKeys = string.Join(", ", change.RequirementKeys); _changeWorkstreamKeys = string.Join(", ", change.WorkstreamKeys); _changeDecisionKey = change.DecisionKey ?? ""; }
    private void EditAllocation(ResourceAllocationDto allocation) { _allocationKey = allocation.Key; _allocationFrom = allocation.FromWorkstreamKey ?? ""; _allocationTo = allocation.ToWorkstreamKey; _allocationHours = allocation.CapacityHoursPerWeek; _allocationStart = allocation.StartUtc.LocalDateTime; _allocationEnd = allocation.EndUtc.LocalDateTime; _allocationStatus = allocation.Status; _allocationRationale = allocation.Rationale ?? ""; _allocationDecisionKey = allocation.DecisionKey ?? ""; }
    private void EditSharedPool(TeamCapacityDto team) { _poolTeamKey = team.Key; _sharedResourceKey = team.SharedResourceKey ?? ""; _sharedPoolCapacity = team.SharedPoolCapacityHoursPerWeek ?? 40m; }
    private ManagementReference? ActionTarget() => string.IsNullOrWhiteSpace(_actionTargetType) || string.IsNullOrWhiteSpace(_actionTargetKey) ? null : new(_actionTargetType.Trim(), _actionTargetKey.Trim());

    private static IReadOnlyList<ManagementReference> ParseReferences(string value)
    {
        var list = new List<ManagementReference>();
        foreach (var part in value.Split([',', ';', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var i = part.IndexOf(':');
            if (i <= 0 || i >= part.Length - 1) continue;
            var type = part[..i].Trim();
            var key = part[(i + 1)..].Trim();
            if (type.Length > 0 && key.Length > 0) list.Add(new(type, key));
        }
        return list.DistinctBy(x => $"{x.Type}:{x.Key}", StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private IReadOnlyList<string> CurrentActorRoles => _roles?.Bindings
        .Where(x => x.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) && x.ActorIdentity.Equals(_roles.CurrentActor, StringComparison.OrdinalIgnoreCase))
        .Select(x => x.Role)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? Array.Empty<string>();

    private static string[] SplitList(string value) => value.Split([',', ';', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTimeOffset Utc(DateTime value) => new DateTimeOffset(value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Local) : value).ToUniversalTime();
    private static DateTimeOffset? UtcNullable(DateTime? value) => value.HasValue ? Utc(value.Value) : null;
    private static string[] Split(string value) => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static IReadOnlyList<CapacityExceptionInput> ParseExceptions(string value)
    {
        var list = new List<CapacityExceptionInput>();
        foreach (var line in value.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 4 || !DateTime.TryParse(parts[0], out var start) || !DateTime.TryParse(parts[1], out var end) || !decimal.TryParse(parts[2], out var hours)) continue;
            list.Add(new(
                new DateTimeOffset(start.Date, TimeZoneInfo.Local.GetUtcOffset(start.Date)).ToUniversalTime(),
                new DateTimeOffset(end.Date.AddDays(1).AddTicks(-1), TimeZoneInfo.Local.GetUtcOffset(end.Date)).ToUniversalTime(),
                hours,
                parts[3],
                parts.Length > 4 ? parts[4] : null));
        }
        return list;
    }

    private Task SetActiveTab(string tab)
    {
        if (_tabs.Contains(tab, StringComparer.Ordinal)) _tab = tab;
        return Task.CompletedTask;
    }

    private static string FormatBytes(int bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / (1024d * 1024d):0.##} MiB";
        if (bytes >= 1024) return $"{bytes / 1024d:0.##} KiB";
        return $"{bytes} B";
    }
}
