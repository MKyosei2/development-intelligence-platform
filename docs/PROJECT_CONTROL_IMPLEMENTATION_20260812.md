# Project Control / Development Intelligence implementation — 2026-08-12

This document records the source implementation that turns DIP's existing Project Truth, QA, incident, benchmark and release paths into a project-control layer for PMs, directors, planners, lead engineers and lead artists.

## Design rule

The implementation does **not** score individual people. It models the flow of work and the evidence around it. The provider-neutral `DevelopmentGraphNode` / `DevelopmentGraphEdge` model is the shared context layer; existing `P2IntelligenceRecord`, `DecisionTrace`, Evidence, Incident, QA and Release entities remain the evidence and decision stores.

## Implemented scope

| Priority | Capability | Source implementation |
|---|---|---|
| S | Development Graph | Durable node/edge model, PostgreSQL migration, graph API and dashboard summary. Nodes include repository/branch/commit/PR/review/file/component/test/feature/criterion/asset/milestone/release/CI/deployment/evidence. |
| S | GitHub complete development-flow integration | Existing verified webhook path is extended into the graph for push, PR, PR review, workflow/check, release and deployment events. Active GitHub sync retrieves commit/PR file and review context. |
| S | Change Impact / Blast Radius | Weighted bounded graph traversal produces risk, blast radius, affected node types, critical paths and required actions. Results are durable P2 intelligence. |
| S | Release Readiness | Existing release gate now includes blocked milestones, feature readiness, acceptance evidence, review state, asset blockers and external CI state. |
| S | Milestone / Critical Path | Milestone nodes, dependency edges, deterministic CPM calculation, cycle detection, slack, critical path and overdue/blocker detection. |
| S | Test Impact Analysis | Existing Python/C++/C# test-impact selector is bridged into Development Graph `TestedBy` edges so selected tests become part of change/release context. |
| A | Review Intelligence | Approval/change-request state, unresolved threads, large diff, source-without-test, public API/schema/asset changes, QA state and Change Risk are combined into merge guidance. Active VCS sync invokes it for PRs. |
| A | Feature Readiness | Feature nodes plus weighted acceptance criteria produce evidence-backed readiness percentages and blockers. |
| A | Acceptance Criteria Evidence | Criteria persist evidence IDs, materialize Evidence nodes and `VerifiedBy` relationships, and derive Passed/Warning/Failed/Unverified state from fresh evidence. |
| A | Asset Intelligence | Asset observations detect failed import, missing references, platform incompatibility, very large assets and duplicate content hash; dependencies and change links enter the graph. |
| A | Cross-repository dependency | Existing repository dependency model remains for compatibility and is mirrored into the central Development Graph so blast-radius analysis can cross repositories. |
| A | Decision Log / Brief | Existing `DecisionTrace` is reused as the durable decision log; a PM/lead brief merges release state, change risk, blocked milestones, feature and asset signals into action-required output. |
| B | Perforce Connector | Provider-neutral VCS interface plus `p4 -ztag describe -s` adapter. Changelists enter the same normalized change and graph pipeline as GitHub. |
| B | What-if Simulation | Deterministic scope/schedule/QA-capacity scenario analysis reports modeled risk, schedule delta, QA-load change, assumptions and trade-offs. It is explicitly decision support, not a guarantee. |
| B | Jenkins / external CI | Jenkins REST adapter plus provider-neutral CI interface and direct normalized CI-ingest endpoint. CI runs link to commits and Release Readiness consumes failed states. |

## Central flow

```text
GitHub webhook / GitHub active sync / Perforce
                  |
                  v
        NormalizedVersionControlChange
                  |
                  v
          Development Graph
        /        |        \
       /         |         \
 Change Impact  Review    Test Impact
       |          |           |
       +----------+-----------+
                  |
      Feature / Acceptance / Asset
                  |
        Milestone / Critical Path
                  |
          Release Readiness
                  |
          Decision Log / Brief
                  |
       PM / Lead / Director dashboard

Jenkins / normalized CI ingest -> Development Graph -> Release Readiness
```

## API surface

All endpoints are under `/api/v1/project-control` and retain DIP's ProjectRead / ProjectWrite authorization policies.

- `GET /projects/{projectId}/graph`
- `POST /graph/nodes`
- `POST /graph/edges`
- `POST /change-impact`
- `POST /test-impact`
- `POST /milestones`
- `POST /milestone-dependencies`
- `GET /projects/{projectId}/critical-path`
- `POST /features`
- `POST /acceptance-criteria`
- `POST /acceptance-evidence`
- `GET /projects/{projectId}/features/{featureKey}/readiness`
- `POST /reviews/analyze`
- `POST /assets/observe`
- `POST /decisions`
- `GET /projects/{projectId}/decisions`
- `GET /projects/{projectId}/decision-brief`
- `POST /what-if`
- `POST /version-control/sync`
- `POST /ci/sync`
- `POST /ci/ingest`
- `GET /projects/{projectId}/dashboard`

## Persistence

`202608120004_AddProjectControlGraph` adds only two new tables:

- `development_graph_nodes`
- `development_graph_edges`

Derived intelligence reuses `p2_intelligence_records`; decision history reuses `decision_traces`. This avoids one table per analysis feature and keeps the graph provider-neutral.

## Integrations

### GitHub

The existing signed/idempotent webhook handling remains authoritative. Development Graph projection recognizes:

- `push`
- `pull_request`
- `pull_request_review`
- `workflow_run`
- `check_run`
- `release`
- `deployment`
- `deployment_status`

Active sync uses the existing `GitHubOptions` token and can retrieve a commit plus PR files/reviews when a PR number is supplied.

### Perforce

Configuration:

```text
Perforce__Executable=p4
Perforce__WorkingDirectory=<optional workspace>
Perforce__TimeoutSeconds=30
```

The connector never shells a user-created command string; it passes fixed `p4` arguments using `ProcessStartInfo.ArgumentList`.

### Jenkins

Configuration:

```text
Jenkins__BaseUrl=https://jenkins.example
Jenkins__User=<optional>
Jenkins__ApiToken=<optional>
Jenkins__TimeoutSeconds=30
```

The connector reads one specific build's JSON API and normalizes status, revision, branch and timing. Other CI systems can send the same `NormalizedCiRun` through `/ci/ingest` without adding a new database model.

## QA language scope

This phase intentionally keeps hardcoded QA depth focused on **Python, C++ and C#**. Universal/multi-language QA remains a later architectural phase. Test-impact integration reuses the existing selector instead of duplicating QA engines.

## Verification contract

Source presence is not runtime verification. The repository should only be described as runtime-verified after:

1. `.NET 8` restore/build/tests pass for the exact revision.
2. Migration `202608120004_AddProjectControlGraph` applies against PostgreSQL.
3. GitHub webhook and active sync smoke tests succeed.
4. Perforce is tested against a real workspace/server where available.
5. Jenkins is tested against a real authenticated/anonymous test job where available.
6. Project Control endpoints are exercised against PostgreSQL/Redis and the dashboard renders live data.

The generated package contains source implementation and static validation evidence; it must not be presented as runtime-verified solely because this file exists.
