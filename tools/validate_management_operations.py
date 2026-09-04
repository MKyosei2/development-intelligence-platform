#!/usr/bin/env python3
"""Static source-contract validation for DIP Management Operations S+/S implementation.

This is intentionally not a replacement for `dotnet build` or live connector tests.
It verifies that high-risk integration surfaces remain wired across Domain/Application/API/Web/Worker/config.
"""
from __future__ import annotations
import argparse, json, re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

CHECKS: list[tuple[str, bool, str]] = []

def text(rel: str) -> str:
    p = ROOT / rel
    if not p.exists():
        return ""
    return p.read_text(encoding="utf-8")

def check(name: str, condition: bool, detail: str) -> None:
    CHECKS.append((name, bool(condition), detail))

required = [
    "src/Dip.Application/Features/Management/ManagementOperationsService.cs",
    "src/Dip.Application/Features/Management/ManagementSyncService.cs",
    "src/Dip.Application/Features/Management/PortfolioManagementService.cs",
    "src/Dip.Application/Features/Management/WorkCalendarIntegrationService.cs",
    "src/Dip.Infrastructure/Calendars/GoogleCalendarRestConnector.cs",
    "src/Dip.Infrastructure/Management/GitHubManagementSyncAdapter.cs",
    "src/Dip.Infrastructure/Management/JiraManagementSyncAdapter.cs",
    "src/Dip.Infrastructure/Management/LinearManagementSyncAdapter.cs",
    "src/Dip.Worker/Workers/ManagementNotificationWorker.cs",
    "src/Dip.Web/Pages/Dashboard/ManagementOperations.razor",
]
check("required_files", all((ROOT / p).exists() for p in required), "All management operation implementation files exist")

enums = text("src/Dip.Domain/Enums/ProjectControlEnums.cs")
for token in ["ResponsibilityAssignment", "ManagementRoleBinding", "WorkCalendar", "ApprovalRequest", "AlertState", "SourceAuthority", "ExternalSyncState"]:
    check(f"domain_{token}", token in enums, f"Development Graph contains {token}")

ops = text("src/Dip.Application/Features/Management/ManagementOperationsService.cs")
check("approval_role_binding", "EnsureActorRoleBindingAsync" in ops and "Separation of duties" in ops, "Approval gate enforces authenticated role binding and SoD")
check("alert_first_seen", "FirstSeenAtUtc" in ops and "RequiredEscalation" in ops, "Escalation age is anchored to first observation")
check("raci_routing", "RouteRole" in ops and "ResponsibleRole" in ops and "AccountableRole" in ops, "Alerts route through RACI/owner metadata")
check("raci_type_safe_routing", "RaciTypeMatches" in ops and "DevelopmentGraphNodeType.Workstream" in ops, "RACI alert routing matches both target key and target type, avoiding same-key cross-category misrouting")
check("cockpit_role_filters_alert_payload", "activeAlerts,actions,myDecisions,myApprovals" in ops, "Daily Cockpit role filter is applied to the returned alert payload as well as actions/decisions/approvals")
check("authority_policy", "ConflictPolicy must be Manual or PreferAuthority" in ops, "Source Authority has explicit safe conflict policy")
check("conflict_applied", "MarkConflictAppliedAsync" in ops, "Resolved sync conflicts can transition to Applied")
check("shared_resource_membership", "UpsertSharedResourceMembershipAsync" in ops, "Shared resource pool membership updates existing Team metadata")
check("notification_read_durable", all(token in ops for token in ["ViewedAtUtc", "ViewedBy", '"read" or "view" or "viewed"', "AlertSeverity", "AlertTitle"]), "Notification read state and alert snapshot are persisted in AlertState metadata rather than browser-only storage")
check("notification_history_from_state", "liveKeys" in ops and "Historical alert state." in ops and "states.Values.Where" in ops, "Resolved/disappeared alert states remain available as durable notification history")

sync = text("src/Dip.Application/Features/Management/ManagementSyncService.cs")
check("safe_sync_no_remote_delete", "The remote item is not deleted automatically" in sync, "Issue-tracker synchronization avoids destructive remote delete")
check("sync_resolved_to_applied", "MarkConflictAppliedAsync" in sync, "Resolved issue-tracker conflicts are consumed")
check("sync_manual_authority", "preferAuthority" in sync and "Manual" in sync, "Bidirectional sync distinguishes manual vs authority-driven resolution")
for provider, token in [("GitHub", "page={page}"), ("Jira", "startAt={startAt}"), ("Linear", "hasNextPage")]:
    adapter_text = text(f"src/Dip.Infrastructure/Management/{provider}ManagementSyncAdapter.cs")
    bounded = ("100-page safety limit" in adapter_text) or (provider == "GitHub" and "10,000 issue safety limit" in adapter_text)
    check(f"sync_{provider.lower()}_pagination", token in adapter_text and bounded, f"{provider} management sync paginates beyond the first 100 items with a bounded safety limit")
    scope_written = (
        '<!-- dip-project:{projectId:N} -->' in adapter_text
        if provider in {"GitHub", "Linear"}
        else 'ProjectLabel(projectId)' in adapter_text and 'labels' in adapter_text
    )
    scope_read = (
        'ProjectMarker(' in adapter_text and 'projectId.ToString("N")' in adapter_text
        if provider in {"GitHub", "Linear"}
        else r'labels = \"{projectLabel}\"' in adapter_text
    )
    check(f"sync_{provider.lower()}_project_scope", scope_written and scope_read, f"{provider} writes and reads a DIP ProjectId scope marker so shared external accounts cannot cross-contaminate projects")
    check(f"sync_{provider.lower()}_metadata_sanitized", "SafeField" in adapter_text, f"{provider} strips control characters from management metadata before sending it externally")

sheet = text("src/Dip.Application/Features/Management/ManagementSpreadsheetService.cs")
check("google_sheets_snapshot", "GoogleSheetsSnapshot" in sheet and "LocalHash" in sheet and "ExternalHash" in sheet, "Google Sheets uses two-sided snapshots")
check("google_sheets_conflict", "both DIP and Google Sheets changed" in sheet and "MarkConflictAppliedAsync" in sheet, "Google Sheets prevents silent overwrite on two-sided edits")
check("shared_pool_sheet_roundtrip", "SharedResourceKey" in sheet and "SharedPoolCapacityHoursPerWeek" in sheet, "Shared resource pool metadata round-trips through Teams sheet")

analytics = text("src/Dip.Application/Features/Management/ManagementAnalyticsService.cs")
check("calendar_capacity", "ApplyCalendarCapacity" in analytics and "RecurringMeetingHoursPerWeek" in analytics and "SupportDutyHoursPerWeek" in analytics, "Calendar/PTO/meeting/support deductions affect capacity")

portfolio = text("src/Dip.Application/Features/Management/PortfolioManagementService.cs")
check("portfolio_pool_explicit", "SharedResourceKey" in portfolio and "SharedPoolCapacityHoursPerWeek" in portfolio, "Cross-project planning uses explicit shared resource pools")
check("portfolio_pool_mismatch", "configured inconsistently" in portfolio, "Conflicting shared-pool capacity configuration is surfaced")

worker = text("src/Dip.Worker/Workers/ManagementNotificationWorker.cs")
check("notification_worker", "GetOperationalAlertsAsync" in worker and "RecordAlertDeliveryAsync" in worker, "Worker scans and records delivered alert levels")
check("notification_worker_persists_snapshot", "RecordAlertDeliveryAsync(project.Id,operational.Alert" in worker, "Worker records the full alert snapshot so notification history can survive later analytics changes")
check("notification_first_seen", "FirstSeenAtUtc" in worker, "Worker escalation uses stable first-seen age")
check("notification_public_application_boundary", "public async Task<DevelopmentGraphNodeDto> RecordAlertDeliveryAsync" in ops and "public static int RequiredEscalation" in ops, "Worker-facing notification operations are public across the Dip.Application/Dip.Worker assembly boundary")
for channel in ["Slack", "Teams", "Email"]:
    channel_path = f"src/Dip.Infrastructure/Management/{channel}ManagementNotificationChannel.cs"
    channel_text = text(channel_path)
    check(f"notification_{channel.lower()}", (ROOT / channel_path).exists(), f"{channel} delivery channel exists")
    check(f"notification_{channel.lower()}_role_destination", "DestinationFor" in channel_text, f"{channel} delivery resolves a RACI/owner role-specific destination before global fallback")
options = text("src/Dip.Infrastructure/Management/ManagementNotificationOptions.cs")
check("notification_role_destinations", "RoleDestinations" in options and "DestinationFor" in options, "Notification configuration supports per-role Slack/Teams/email destinations")

controller = text("src/Dip.Api/Controllers/ManagementOperationsController.cs")
for signature, role in [
    ("UpsertManagementRole", "Owner"), ("UpsertSharedResourceMembership", "Maintainer"),
    ("RespondApproval", "Maintainer"), ("UpsertAuthority", "Owner"), ("Synchronize", "Maintainer")]:
    pos = controller.find(signature)
    prefix = controller[max(0, pos-500):pos] if pos >= 0 else ""
    check(f"auth_{signature}", pos >= 0 and f"ProjectMembershipRole.{role}" in prefix, f"{signature} requires {role}")

project_control = text("src/Dip.Api/Controllers/ProjectControlController.cs")
sheets_pos = project_control.find("SyncGoogleSheets")
sheets_prefix = project_control[max(0, sheets_pos-500):sheets_pos] if sheets_pos >= 0 else ""
check("auth_GoogleSheetsSync", sheets_pos >= 0 and "RequireProjectRole(ProjectMembershipRole.Maintainer)" in sheets_prefix, "Google Sheets management synchronization requires Maintainer")

app_di = text("src/Dip.Application/DependencyInjection.cs")
infra_di = text("src/Dip.Infrastructure/DependencyInjection.cs")
worker_program = text("src/Dip.Worker/Program.cs")
for service in ["ManagementOperationsService", "ManagementSyncService", "PortfolioManagementService", "WorkCalendarIntegrationService"]:
    check(f"di_{service}", service in app_di, f"Application DI registers {service}")
for service in ["GoogleCalendarRestConnector", "SlackManagementNotificationChannel", "TeamsManagementNotificationChannel", "EmailManagementNotificationChannel", "GitHubManagementSyncAdapter", "JiraManagementSyncAdapter", "LinearManagementSyncAdapter"]:
    check(f"di_{service}", service in infra_di, f"Infrastructure DI registers {service}")
check("worker_registered", "AddHostedService<ManagementNotificationWorker>" in worker_program, "Notification worker is hosted")

page = text("src/Dip.Web/Pages/Dashboard/ManagementOperations.razor")
for token in ["Daily Cockpit", "Operational alerts", "Pending approvals", "Risk register", "Scope changes", "Cross-project shared resource pools", "RACI responsibilities", "Capacity calendars", "Sync & source of truth", "Google Calendar", "Google Sheets"]:
    check("web_" + token.lower().replace(" ", "_").replace("&", "and"), token in page, f"Management Web exposes {token}")
check("web_full_update_fields", all(x in page for x in ["_riskMitigation", "_decisionStatus", "_actionCompletion", "_changeRequirementKeys", "_allocationRationale"]), "GUI update paths preserve extended management fields")
check("web_approval_bound_role_select", "CurrentActorRoles" in page and "Select bound role" in page, "Approval response UI only offers management roles bound to the signed-in actor")
check(
    "web_cockpit_bound_role_filter",
    "All accessible work" in page
    and re.search(r"@foreach\s*\(\s*var\s+role\s+in\s+CurrentActorRoles\s*\)", page) is not None,
    "Daily Cockpit My-role filter is selected from roles bound to the signed-in actor",
)

# Cross-feature completion checks added after the browser-first lifecycle/evidence pass.
access = text("src/Dip.Application/Features/Authorization/ProjectAccessService.cs")
check("last_owner_demotion_blocked", "The last project owner cannot be demoted" in access and "ProjectMembershipRole.Owner" in access, "Role updates cannot silently demote the final project owner")

pipeline_controller = text("src/Dip.Api/Controllers/PipelineRunsController.cs")
check("pipeline_specific_qa_evidence", all(token in pipeline_controller for token in ["IQualityIssueRepository", "IEvidenceRepository", "QualityIssues", "Evidence"]), "Pipeline detail returns concrete related QA issues and EvidenceRecord entries")

github_controller = text("src/Dip.Api/Controllers/GitHubRepositoriesController.cs")
github_catalog = text("src/Dip.Infrastructure/GitHub/GitHubRepositoryCatalog.cs")
github_contract = text("src/Dip.Application/Abstractions/GitHub/IGitHubRepositoryCatalog.cs")
github_options = text("src/Dip.Infrastructure/GitHub/GitHubOptions.cs")
repositories_contract = text("src/Dip.Application/Abstractions/Persistence/Repositories.cs")
repository_page = text("src/Dip.Web/Pages/Dashboard/Repository.razor")
check("github_capability_probe", all(token in github_catalog for token in ["GitHubCapabilityProbeDto", "Contents", "PullRequests", "Actions"]), "GitHub connection test can distinguish repository/content/PR/Actions capabilities")
check("github_project_specific_probe", "projects/{projectId:guid}/test" in github_controller and "ProbeRepositoryAsync" in github_controller and "TestGitHubConnectionAsync(_project?.Id)" in text("src/Dip.Web/Pages/Settings.razor"), "Settings verifies capabilities against the selected DIP project's repository instead of an arbitrary repository")
check("github_pr_backfill_explicit_sync", 'ListPullRequestsAsync(owner, repository, "all"' in github_controller and "BackfillPullRequestsAsync" in github_controller and "TryBackfillPullRequestsAsync" not in github_controller, "Import/sync perform bounded historical PR backfill while PR GET endpoints stay read-only")
check("github_pr_backfill_pagination", all(token in github_catalog for token in ["pulls?state={normalizedState}", "per_page=100&page={page}", "/commits?per_page=100&page={page}", "/files?per_page=100&page={page}", "/reviews?per_page=100&page={page}"]) and all(token in github_options for token in ["MaxPullRequestPages", "MaxPullRequestCommitPages", "MaxPullRequestFilePages", "MaxPullRequestReviewPages"]) and "ListPullRequestsAsync" in github_contract, "Historical PR, commit, file and review collection is paginated with explicit safety limits")
check("github_pr_merged_from_timestamp", 'GetDate(item, "merged_at").HasValue' in github_catalog, "Closed and merged pull requests are distinguished using merged_at")
check("pr_pipeline_qa_evidence_traceability", all(token in github_controller for token in ["ListByCommitShasAsync", "ListByPipelineRunsAsync", "PipelineRuns", "QualityIssues", "Evidence"]) and all(token in repository_page for token in ["Persisted pipeline runs", "Related QA issues", "Evidence records"]), "PR detail traces revisions through persisted pipelines to concrete QA issues and EvidenceRecord entries")
check("pipeline_commit_query_contract", "ListByCommitShasAsync" in repositories_contract, "Pipeline repository exposes full-SHA lookup for PR traceability")
evidence_repo = text("src/Dip.Infrastructure/Persistence/Repositories/EvidenceRepository.cs")
quality_repo = text("src/Dip.Infrastructure/Persistence/Repositories/QualityIssueRepository.cs")
check("pr_traceability_direct_repository_queries", "ListByPipelineRunsAsync" in evidence_repo and ".Take(2000)" not in evidence_repo and "ListByPipelineRunsAsync" in quality_repo, "PR evidence/QA traceability queries target pipeline IDs directly instead of truncating a latest-N project window")
check("github_backfill_reports_safety_limit", "pagination safety limit" in github_catalog and "EnsureSuccessStatusCode" in github_catalog, "Historical GitHub enrichment fails explicitly instead of silently returning a partial page when permissions or configured bounds are exhausted")

audit_repo = text("src/Dip.Infrastructure/Persistence/Repositories/AuditLogRepository.cs")
projects_controller = text("src/Dip.Api/Controllers/ProjectsController.cs")
project_service = text("src/Dip.Application/Features/Projects/ProjectService.cs")
settings_page = text("src/Dip.Web/Pages/Settings.razor")
check("audit_server_side_search", all(token in audit_repo for token in ["SearchAsync", "fromUtc", "toUtc", "correlationId"]) and all(token in projects_controller for token in ["fromUtc", "toUtc", "correlationId"]), "Audit search and date filters execute server-side before the result limit")
check("audit_cursor_pagination", all(token in audit_repo for token in ["SearchPageAsync", "beforeUtc", "beforeId", "ThenByDescending(x => x.Id)"]) and "ProjectAuditPageDto" in project_service and "audit-page" in projects_controller and "LoadMoreAuditAsync" in settings_page, "Audit history uses stable CreatedAtUtc+Id keyset pagination and a browser Load more flow")

main_layout = text("src/Dip.Web/Components/Layout/MainLayout.razor")
check("notifications_not_localstorage", "localStorage" not in main_layout and "ViewedAtUtc" in main_layout, "Notification unread state comes from durable server state rather than browser localStorage")
check("notification_event_history", all(token in ops for token in ["notification.viewed", "notification.acknowledged", "notification.snoozed", "notification.reopened", "notification.resolved", "notification.delivered", "GetAlertHistoryAsync"]) and "AlertHistory" in controller and "notification-history" in main_layout, "Notification interactions and deliveries append immutable audit events that are exposed as a timeline")

api_client = text("src/Dip.Web/Services/ApiClient.cs")
for call in ["GetDailyCockpitAsync", "UpsertSharedResourceMembershipAsync", "RespondApprovalAsync", "RunManagementSyncAsync", "SyncManagementGoogleCalendarAsync", "SyncManagementGoogleSheetsAsync"]:
    check(f"web_api_{call}", call in api_client, f"Web ApiClient reaches {call}")

for cfg_rel in ["src/Dip.Api/appsettings.json", "src/Dip.Worker/appsettings.json"]:
    try:
        cfg = json.loads(text(cfg_rel))
        check(f"config_{Path(cfg_rel).parent.name}_notifications", "ManagementNotifications" in cfg, f"{cfg_rel} contains ManagementNotifications")
    except Exception as exc:
        check(f"config_{cfg_rel}", False, f"Could not parse {cfg_rel}: {exc}")

calendar = text("src/Dip.Application/Features/Management/WorkCalendarIntegrationService.cs")
check("calendar_token_not_persisted", "AccessToken" not in "\n".join(line for line in calendar.splitlines() if "JsonSerializer.Serialize" in line or "UpsertWorkCalendar" in line), "Google Calendar OAuth token is not serialized into graph/calendar metadata")
check("calendar_all_day_working_days", "workingDays" in calendar and "if(!workingDays.Contains(day.DayOfWeek))continue" in calendar and "day:yyyyMMdd" in calendar, "Google Calendar all-day PTO/holidays are split by configured working day so weekends are not over-deducted")

failed = [c for c in CHECKS if not c[1]]
result = {
    "validator": "management-operations-static-contract-v1",
    "formalRuntimeVerification": False,
    "passed": not failed,
    "checks": [{"name": n, "passed": ok, "detail": detail} for n, ok, detail in CHECKS],
    "summary": {"passed": sum(1 for _, ok, _ in CHECKS if ok), "failed": len(failed), "total": len(CHECKS)},
}
parser = argparse.ArgumentParser()
parser.add_argument("--output")
args = parser.parse_args()
if args.output:
    out = Path(args.output)
    if not out.is_absolute(): out = ROOT / out
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, ensure_ascii=False, indent=2))
sys.exit(1 if failed else 0)
