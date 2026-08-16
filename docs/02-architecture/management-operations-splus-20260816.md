# Management Operations S+/S — 2026-08-16

## Purpose

This expansion turns existing Development Graph / Management Analytics data into an operational control loop for project managers, producers and discipline leads. It does not replace Evidence/Project Truth. Management decisions, responsibilities, approvals, alerts, calendars and synchronization state are persisted beside the existing graph so technical evidence and management action stay connected.

The intended loop is:

```text
Detect -> Prioritize -> Decide -> Assign -> Approve -> Act -> Verify -> Close -> Review
```

## Implemented S+ capabilities

### Daily Cockpit / My work

`GET /api/v1/management/projects/{projectId}/cockpit`

The cockpit combines current alerts, open actions, pending decisions and approvals. `ownerRole` narrows **alerts, actions, decisions and approval roles** together; it is not only an Action filter. The Web page is `/dashboard/{projectId}/management`.

### Browser CRUD for management registers

The Management page can create/update and archive Risks, Decisions, Actions, Scope Changes and Resource Allocations. Update forms preserve the management fields needed for operations rather than replacing existing records with a reduced subset: status, category, triggers, mitigation, related targets, completion, decision links, scope keys and allocation rationale are represented.

Archive is non-destructive. `ManagementOperationsService.ArchiveAsync` retains the graph node and adds archive actor/time/reason metadata.

### Notification delivery + acknowledgement / snooze / escalation

`ManagementNotificationWorker` calculates operational alerts and routes them to configured Slack, Teams and SMTP channels. Delivery is fail-visible in Worker logs and successful delivery level is persisted as `AlertState`.

Alert state supports:

- `Acknowledged`
- `Snoozed` with `SnoozeUntilUtc`
- `Resolved`
- `Open` / reopen

Escalation is based on persisted `FirstSeenAtUtc`; acknowledgement does not reset the age of the incident. Acknowledgement records the currently reached escalation level, suppressing repeated delivery at the same level while still allowing later L2/L3 escalation.

Default ladder:

| Severity | L1 | L2 | L3 |
|---|---:|---:|---:|
| Critical | immediately | 30 min | 2 h |
| High | immediately | 2 h | 1 day |
| Other | immediately | 2 days | — |

`ManagementNotifications:RoleRoutes` maps RACI/owner roles to delivery channel types. `ManagementNotifications:RoleDestinations:<role>` can override the Slack webhook, Teams webhook and email recipients for that specific role (with `*` fallback). When a source item has RACI, Responsible is preferred, then Accountable, then explicit owner metadata, then category fallback.

### Formal approval workflow

Approval requests target existing management/release graph nodes and require one or more management roles. A Project Owner binds an authenticated audit identity (`login#subject`) to management roles. Approval response endpoints overwrite the actor with `RequestActor.AuditIdentity(User)`; request bodies cannot choose the authenticated identity.

By default `AllowSameActorMultipleRoles=false`, so one person cannot satisfy multiple required roles for the same approval. A rejection closes the approval as Rejected; all required roles approving closes it as Approved.

Sensitive API roles:

- role binding: Project **Owner**
- source authority: Project **Owner**
- approval create/respond: Project **Maintainer**
- issue tracker sync / conflict resolution: Project **Maintainer**
- alert disposition / RACI: Project **Member**

### Conflict-safe external synchronization

GitHub, Jira and Linear map external work items to DIP Management Actions. Google Sheets covers the broader editable management workbook.

Issue tracker synchronization is bidirectional and keeps provider links/revisions. The provider adapters paginate beyond the first 100 records with bounded safety limits. Managed external records are also explicitly project-scoped: GitHub/Linear descriptions carry a `dip-project:<ProjectId>` marker and Jira uses a `dip-project-<ProjectId>` label. Pull requires the same ProjectId, preventing a shared Jira/Linear/GitHub account from mixing Actions between DIP projects. Management metadata written into external descriptions is normalized to a bounded single line before marker/field serialization.

Issue tracker synchronization is conflict-safe. A mismatch becomes a durable conflict unless an explicit Source Authority with `ConflictPolicy=PreferAuthority` resolves it. `Manual` never silently overwrites both-sided changes. Conflict resolution transitions:

```text
Open -> Resolved(DIP|External) -> next successful synchronization -> Applied
```

External-only items are not remotely deleted automatically. Choosing DIP for an external-only conflict means “do not import this unchanged remote item”; choosing External for a local-only conflict archives the local DIP Action instead of inventing a destructive remote delete.

Google Sheets stores per-sheet `(LocalHash, ExternalHash, SyncedAtUtc)` snapshots. If both sides change after the last snapshot, synchronization pauses as a conflict. Snapshot/resolution state is advanced only after the selected pull/push applies successfully.

## Implemented S capabilities

### Portfolio / Program Command Center

`GET /api/v1/management/portfolio`

The Home page aggregates accessible projects with project health, schedule delay, peak capacity, critical risk, blockers, pending decisions, overdue actions, approvals and synchronization conflicts. Only projects visible to the authenticated user are returned unless the caller is an Admin/development mode.

### Explicit cross-project resource planning

Cross-project resource planning does **not** infer a shared workforce from equal team names. Each Team can explicitly carry:

- `SharedResourceKey`
- `SharedPoolCapacityHoursPerWeek`

The Management page can add/remove a Team's membership without rewriting its existing team capacity/name/status. The Teams spreadsheet round-trips both values.

Portfolio planning groups only explicitly matching resource keys, sums weekly demand across projects and compares it with the shared pool limit. If projects configure different limits for one key, DIP surfaces a configuration conflict and conservatively uses the lowest limit until the values are aligned.

### RACI / responsibility

RACI assignments target existing graph items and persist Responsible, Accountable, Consulted and Informed roles. RACI also drives operational alert routing, so it affects behavior rather than acting only as documentation.

### Daily / Weekly / Release review modes

`GET /api/v1/management/projects/{projectId}/cadence/{daily|weekly|release}`

Meeting mode builds a bounded agenda from the same Management Dashboard/Cockpit data. Release review explicitly surfaces schedule gate, blockers, critical risk and pending approvals rather than creating a separate untraceable meeting state.

### Work Calendar / PTO / Holiday / Meeting capacity

Work Calendars are Project-wide or Team-specific and contain:

- Hours/day and working days
- recurring meeting hours/week
- support/on-call hours/week
- dated capacity exceptions (PTO, holiday, meeting, support or other)

Calendar deductions are applied directly to weekly capacity forecasts. Multi-week exceptions are prorated by actual overlap with each week, so a long leave is not deducted in full from every overlapping week.

Google Calendar synchronization classifies PTO/leave/holiday/meeting/support events into calendar exceptions. OAuth access tokens are request-only and are not serialized into graph metadata. All-day events use the calendar/event timezone instead of assuming UTC. Multi-day all-day PTO/holiday events are split into configured working-day exceptions, so weekends/non-working days are not accidentally deducted from capacity.

### Source of Truth / conflict policy

Source Authority rules are project-scoped and identify `DataType`, `Authority`, `ConflictPolicy` and priority. Supported conflict policies are deliberately limited to:

- `Manual`: do not auto-overwrite a real mismatch
- `PreferAuthority`: apply the configured authoritative side

This is used by issue tracker and Google Sheets synchronization. Manual merge is performed by editing the chosen side, then resolving to DIP or External; DIP does not store an ambiguous third “Merge” state that cannot be executed deterministically.

## Security and operational boundaries

- Project-scoped endpoints are protected by existing `ProjectScopeAuthorizationFilter`; route and body ProjectIds are cross-checked.
- Formal approval identities are derived server-side from authenticated claims.
- Slack/Teams webhook URLs require HTTPS.
- Google Calendar and Google Sheets access tokens are not persisted by this layer.
- Source synchronization does not perform automatic destructive remote deletes.
- Notification channels are inactive when not configured; no fake delivery success is recorded.

## Verification

`tools/validate_management_operations.py` statically verifies the integration surfaces across Domain, Application, Infrastructure, API, Worker, Web and configuration. Its PASS is **not** a .NET build or live connector PASS.

Exact-revision runtime completion still requires:

1. `dotnet build` and .NET unit/integration tests.
2. Browser/API smoke test with authentication enabled.
3. Slack/Teams/SMTP delivery against test destinations.
4. Jira/Linear/GitHub and Google Sheets/Calendar tests against sandbox accounts.
5. Worker + PostgreSQL persistence/restart verification.

Generated validation evidence is stored under `artifacts/management-operations-validation-20260816/`.
