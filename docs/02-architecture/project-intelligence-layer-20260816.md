# Project Intelligence Layer — 2026-08-16

## Purpose

DIP does not require a company to replace Excel, Redmine, Slack, Teams, GitHub, CI, or QA tooling. The Project Intelligence Layer reads those existing sources, preserves their provenance, links references that describe the same work item, and shows PMs/leads where the plan, reported state, and machine-observed evidence disagree.

The core comparison is intentionally split into four layers:

1. **Plan** — WBS/Gantt data imported from Excel/CSV.
2. **Operational report** — Redmine status/progress and collaboration progress claims.
3. **Engineering evidence** — GitHub/PR/CI/build/test/QA/runtime evidence already stored by DIP.
4. **Management interpretation** — stale signals, contradictions, evidence readiness, and digest actions.

A chat message that says “done” is not promoted to verified completion. A Redmine done ratio is not treated as a build/test result. `EvidenceReadinessPercent` is a work-item evidence heuristic; it is **not an employee skill score or performance rating**.

## Excel / WBS / Gantt understanding

Supported upload formats are `.xlsx`, `.xlsm`, and `.csv`. Legacy `.xls` must be saved as `.xlsx` or CSV before import.

The analyzer scans the first 20 rows of each sheet and detects Japanese/English aliases for:

- WBS key / task ID
- task or work-item name
- parent
- planned start / end / due date
- declared progress
- owner
- status
- predecessor/dependency
- milestone

The browser always exposes a mapping preview before import. Missing WBS hierarchy can be inferred from dotted identifiers such as `1.2.3` or from title indentation. The importer stores each workbook revision as `ProjectPlanImport` + `PlanItem` nodes in the Development Graph. It **does not overwrite canonical Task/Action/Requirement nodes**.

Safety limits:

- upload: 12 MB
- XLSX package entries: 2,000
- expanded workbook: 80 MB total / 32 MB per entry
- plan rows: 20,000 per import revision

## Cross-source entity resolution

DIP tries to map source entities such as an Excel WBS row, Redmine issue, Slack observation, GitHub PR, or commit to canonical project entities.

Signals include:

- exact logical key
- an explicit `DIP:` / `TASK:` reference
- an external URL/reference match
- target key in a source title
- normalized title-token similarity

Automatic persistence is deliberately conservative: exact/very strong matches, or a high score with a clear margin over the next candidate. Lower-confidence matches remain `Suggested` until a Maintainer confirms them. A manual confirmation produces an `EquivalentTo` edge and an auditable `EntityBinding` node.

## Redmine source

The Redmine adapter is **read-only** and uses the REST API. It imports:

- issue ID / subject / description
- tracker / status / priority
- assignee
- start / due date
- done ratio
- fixed version
- relations
- attachments
- custom fields

Custom fields are preserved in source metadata because real Redmine deployments frequently encode company-specific WBS or ownership conventions there. Fields whose names contain `DIP`, `Task`, or `WBS` are also considered potential entity references.

Configuration:

```json
{
  "ProjectIntelligenceSources": {
    "Redmine": {
      "BaseUrl": "https://redmine.example.com",
      "ApiKey": "server-secret",
      "ProjectIdentifier": "default-project",
      "ProjectIdentifiers": {
        "<dip-project-guid>": "redmine-project-key"
      }
    }
  }
}
```

Production Redmine URLs must use HTTPS. Project-specific mappings prevent data from different DIP projects from being mixed when one Redmine server hosts many projects.

## Slack source and signed event ingestion

Two paths are available:

- read recent relevant messages with `conversations.history`
- receive Slack Events API callbacks at `POST /api/v1/project-intelligence/hooks/slack/{projectId}`

The history adapter does not ingest every chat message. It keeps messages that contain a task marker, URL, progress percentage, completion/review/test/CI/PR/blocker wording, or file evidence. Project-specific channel allowlists scope collection.

Slack Events requests require `X-Slack-Request-Timestamp` and `X-Slack-Signature`. DIP verifies the official HMAC signature and rejects timestamps outside a five-minute replay window. The event channel must also be allowed for the route’s DIP project.

Required Slack scopes depend on the channel types used. At minimum the bot needs permission to read the configured conversation history. Tokens and signing secrets belong in server-side secret storage, never in the browser bundle.

## Teams evidence bridge

A signed normalized bridge is available at:

`POST /api/v1/project-intelligence/hooks/teams/{projectId}`

Header:

`X-DIP-Ingest-Key: <server-side-key>`

Payload:

```json
{
  "externalId": "message-123",
  "channel": "rendering",
  "author": "lead-a",
  "text": "TASK-42 implementation complete; PR https://github.com/org/repo/pull/42",
  "observedAtUtc": "2026-08-16T06:00:00Z",
  "url": "https://teams.example/message/123",
  "references": ["TASK-42", "https://github.com/org/repo/pull/42"],
  "revision": "1"
}
```

Use `ProjectIngestKeys` when one DIP deployment serves multiple projects. The shared `IngestKey` is only a fallback.

## Source provenance and deduplication

Each source observation keeps:

- provider
- external source ID
- revision
- observed/updated time
- source URL
- raw normalized SHA-256
- channel/author where applicable

The source node key and revision are used to prevent unchanged polling data from generating duplicate Evidence records. Collaboration messages without a revision use a deterministic SHA over the text and references.

## Plan vs Actual / Evidence-backed Progress

For each current WBS item DIP exposes independent fields:

- `PlannedPercent` — declared in the imported plan
- `OperationalPercent` — most recent canonical task / Redmine / collaboration report
- `EvidenceReadinessPercent` — heuristic aggregation of independent machine evidence
- `ReadinessConfidence` — `Unverified`, `Low`, `Medium`, `High`, or `Verified`

Typical contradictions include:

- WBS says 80%, Redmine says 40%
- WBS says 100%, but evidence readiness remains low
- Slack says “complete”, but no PR/CI/QA evidence is linked
- a high reported percentage conflicts with failing CI
- a due date has passed without independently verified completion
- no fresh progress/evidence signal for seven days

The system explains these as source/evidence gaps; it does not accuse an employee of inaccurate reporting.

## Management digest and WBS/Gantt viewer

`/dashboard/{projectId}/sources` provides:

- source adapter status and dry-run/sync actions
- Excel mapping preview/import
- plan-vs-actual table
- evidence details
- contradiction/stale reasons
- WBS/Gantt schedule bars colored by cross-source status
- unresolved entity suggestions and manual confirmation
- an executive management digest

The Gantt view is a read/understand surface for an imported company plan. The original Excel remains usable as the planning tool.

## API surface

Authenticated project routes:

- `GET /api/v1/project-intelligence/projects/{projectId}/overview`
- `GET /api/v1/project-intelligence/projects/{projectId}/gantt`
- `GET /api/v1/project-intelligence/projects/{projectId}/digest`
- `GET /api/v1/project-intelligence/projects/{projectId}/entity-resolution`
- `GET /api/v1/project-intelligence/projects/{projectId}/sources`
- `POST /api/v1/project-intelligence/projects/{projectId}/sources/{provider}/sync`
- `POST /api/v1/project-intelligence/projects/{projectId}/plans/analyze`
- `POST /api/v1/project-intelligence/projects/{projectId}/plans/import`
- `POST /api/v1/project-intelligence/projects/{projectId}/collaboration/ingest`
- `POST /api/v1/project-intelligence/projects/{projectId}/entity-bindings`

Signed public webhook routes:

- `POST /api/v1/project-intelligence/hooks/slack/{projectId}`
- `POST /api/v1/project-intelligence/hooks/teams/{projectId}`

Read routes require Viewer membership. Pull/import/binding routes require Maintainer. Authenticated normalized collaboration ingestion requires Member. Public webhook routes do not use a browser/JWT identity because they are independently authenticated by Slack HMAC or a project-specific bridge key.

## Non-goals

The Project Intelligence Layer deliberately does not:

- replace Excel, Redmine, Slack, Teams, or GitHub
- infer an individual developer’s skill score
- treat chat messages as machine-verifiable truth
- overwrite canonical task data during WBS import
- silently force low-confidence entity matches
- scan unrelated Slack conversations outside configured project channels
