# Management Intelligence

The Development Intelligence Platform treats QA, source control, CI, runtime telemetry and asset analysis as **supporting evidence**. The primary management surface is designed for PMs, directors, planners, lead engineers and lead artists who should not need to understand implementation details in order to understand project state.

## Management model

The durable Development Graph now includes `Team`, `Workstream`, `Task`, `Blocker`, `Requirement` and `ScopeBaseline` nodes in addition to the existing milestone, feature, asset, source-control and QA nodes.

Management flow:

`Team -> Workstream -> Task -> Dependency -> Milestone -> Release`

`Blocker -> Task/Workstream/Milestone`

`Requirement <- Task/Feature/Milestone`

Technical evidence remains connected to the same graph but is not the primary project health signal.

## Implemented S-priority capabilities

- Team Workload / Capacity: effective capacity, planning buffer, remaining work, utilization and weekly workload buckets for teams and workstreams.
- Task / Workstream Management: durable graph-backed teams, workstreams and tasks with status, dates, estimates and completion.
- Schedule / Gantt: planned dates, dependency-aware forecast dates, delay calculation and a generated Gantt sheet.
- Critical Path: zero-slack task/milestone path with cycle detection.
- Blocker Management: blocker aging, direct impact, downstream blast radius and management-action text.
- Dependency Management: task/workstream/milestone/requirement dependency view, cycle detection and cross-workstream links.
- Milestone Management: linked-task completion, blocked task count, overdue state and readiness.
- Requirement / Scope Management: requirement priority/status/scope state, scope baselines, growth calculation and high-priority incomplete scope warnings.
- Spreadsheet Integration: native `.xlsx` export/import plus Google Sheets bidirectional REST sync.

## Spreadsheet contract

Editable sheets:

- `Teams`
- `Workstreams`
- `Tasks`
- `Milestones`
- `Blockers`
- `Requirements`
- `Dependencies`
- `ScopeBaselines`

DIP-calculated sheets:

- `Executive`
- `Capacity`
- `Workload`
- `Schedule`
- `Gantt`
- `Scope`
- `RawGraph`

Google Sheets sync uses an OAuth access token supplied only for the request. The token is not written to the Development Graph. The connector creates missing tabs, synchronizes values, freezes header rows, applies header styling and auto-resizes columns.

## Key endpoints

- `POST /api/v1/project-control/teams`
- `POST /api/v1/project-control/workstreams`
- `POST /api/v1/project-control/tasks`
- `POST /api/v1/project-control/blockers`
- `POST /api/v1/project-control/requirements`
- `POST /api/v1/project-control/management-dependencies`
- `POST /api/v1/project-control/scope-baselines`
- `GET /api/v1/project-control/projects/{projectId}/management-dashboard`
- `GET /api/v1/project-control/projects/{projectId}/capacity`
- `GET /api/v1/project-control/projects/{projectId}/schedule`
- `GET /api/v1/project-control/projects/{projectId}/blockers`
- `GET /api/v1/project-control/projects/{projectId}/management-dependencies`
- `GET /api/v1/project-control/projects/{projectId}/management-milestones`
- `GET /api/v1/project-control/projects/{projectId}/scope`
- `GET /api/v1/project-control/projects/{projectId}/management.xlsx`
- `POST /api/v1/project-control/projects/{projectId}/management.xlsx/import`
- `POST /api/v1/project-control/google-sheets/sync`

## Interpretation policy

DIP does not score individual employee productivity. Capacity and bottleneck intelligence is calculated at team/workstream level. Individual owner labels may be displayed for coordination, but they are not used to produce a productivity score.
