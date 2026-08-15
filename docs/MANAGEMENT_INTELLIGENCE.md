# Management Intelligence

The Development Intelligence Platform treats QA, source control, CI, runtime telemetry and asset analysis as **supporting evidence**. The primary management surface is designed for PMs, directors, planners, lead engineers and lead artists who should not need implementation-level expertise to understand project state or decide what to do next.

## Management model

The durable Development Graph includes `Team`, `Workstream`, `Task`, `Blocker`, `Requirement`, `ScopeBaseline`, `Risk`, `ManagementDecision`, `ActionItem`, `ChangeRequest` and `ResourceAllocation` nodes in addition to the existing milestone, feature, asset, source-control and QA nodes.

Core management flow:

`Team -> Workstream -> Task -> Dependency -> Milestone -> Release`

`Blocker -> Task/Workstream/Milestone`

`Requirement <- Task/Feature/Milestone`

`Risk -> affected work -> mitigation Action`

`ChangeRequest -> affected Requirement/Workstream -> Decision -> Action/ResourceAllocation`

Technical evidence remains connected to the same graph but is not the primary project-health signal.

## Implemented management capabilities

### Delivery control

- Team Workload / Capacity: effective capacity, planning buffer, remaining work, utilization and weekly workload buckets for teams/workstreams.
- Task / Workstream Management: durable graph-backed teams, workstreams and tasks with status, dates, estimates and completion.
- Schedule / Gantt: planned dates, dependency-aware forecast dates, delay calculation and generated Gantt data.
- Critical Path: zero-slack task/milestone path with cycle detection.
- Blocker Management: blocker aging, direct impact, downstream blast radius and management-action text.
- Dependency Management: task/workstream/milestone/requirement dependency view, cycle detection and cross-workstream links.
- Milestone Management: linked-task completion, blocked task count, overdue state and readiness.
- Requirement / Scope Management: requirement priority/status/scope state, scope baselines, growth calculation and high-priority incomplete-scope warnings.
- Spreadsheet Integration: native `.xlsx` export/import and Google Sheets bidirectional synchronization for editable management data and calculated management intelligence.

### Decision cycle

- Risk Register / Risk Management: probability, impact, exposure, review date, trigger, mitigation, affected items and escalation guidance.
- Decision / Action Management: proposed/decided management decisions, rationale, decision due date, role ownership, linked actions, action completion and overdue tracking.
- Resource Allocation: proposed or approved workstream-level capacity movements plus overload-driven recommendations. DIP intentionally does not produce individual productivity scores.
- Change Request / Scope Change: engineering/art/QA hour deltas, schedule-day impact, risk delta, affected requirements/workstreams and decision linkage.
- Notification / Escalation: calculated critical/high management alerts for aged blockers, overloaded workstreams, high-exposure risks, overdue actions, schedule slips and material scope changes.
- Project Health / Management Summary: explainable project-health dimensions, a primary concern, key drivers, decisions required, recommended actions and a plain-language summary.

## Spreadsheet contract

Excel and Google Sheets are first-class management surfaces rather than one-way export targets.

Editable sheets:

- `Teams`
- `Workstreams`
- `Tasks`
- `Milestones`
- `Blockers`
- `Requirements`
- `Dependencies`
- `ScopeBaselines`
- `Risks`
- `Decisions`
- `Actions`
- `ChangeRequests`
- `ResourceAllocations`

DIP-calculated sheets:

- `Executive`
- `Capacity`
- `Workload`
- `Schedule`
- `Gantt`
- `Scope`
- `ResourceRecommendations`
- `Escalations`
- `ManagementSummary`
- `RawGraph`

Google Sheets sync uses an OAuth access token supplied only for the request. The token is not written to the Development Graph. The connector creates missing tabs, synchronizes values, freezes header rows, applies header styling and auto-resizes columns.

## Key endpoints

Delivery management:

- `POST /api/v1/project-control/teams`
- `POST /api/v1/project-control/workstreams`
- `POST /api/v1/project-control/tasks`
- `POST /api/v1/project-control/blockers`
- `POST /api/v1/project-control/requirements`
- `POST /api/v1/project-control/management-dependencies`
- `POST /api/v1/project-control/scope-baselines`

Decision cycle:

- `POST /api/v1/project-control/risks`
- `GET /api/v1/project-control/projects/{projectId}/risks`
- `POST /api/v1/project-control/management-decisions`
- `POST /api/v1/project-control/management-actions`
- `GET /api/v1/project-control/projects/{projectId}/decision-actions`
- `POST /api/v1/project-control/resource-allocations`
- `GET /api/v1/project-control/projects/{projectId}/resource-allocations`
- `POST /api/v1/project-control/change-requests`
- `GET /api/v1/project-control/projects/{projectId}/scope-changes`
- `GET /api/v1/project-control/projects/{projectId}/management-alerts`
- `GET /api/v1/notifications/projects/{projectId}/management`

Management views and spreadsheet sync:

- `GET /api/v1/project-control/projects/{projectId}/management-dashboard`
- `GET /api/v1/project-control/projects/{projectId}/capacity`
- `GET /api/v1/project-control/projects/{projectId}/schedule`
- `GET /api/v1/project-control/projects/{projectId}/blockers`
- `GET /api/v1/project-control/projects/{projectId}/management-dependencies`
- `GET /api/v1/project-control/projects/{projectId}/management-milestones`
- `GET /api/v1/project-control/projects/{projectId}/scope`
- `GET /api/v1/project-control/projects/{projectId}/management.xlsx`
- `POST /api/v1/project-control/projects/{projectId}/management.xlsx/import`

## Project-health policy

Project Health is explainable. DIP exposes dimensions such as Schedule, Capacity, Blockers, Dependencies, Scope, Milestones, Risks, Decisions & Actions, Scope Changes and Escalation. The aggregate score is accompanied by the weakest dimension, key drivers and actions. It must not be presented as a guarantee.

## Interpretation policy

DIP does not score individual employee productivity. Capacity and bottleneck intelligence is calculated at team/workstream level. Role labels may be displayed for coordination, but they are not used to produce individual performance scores.
