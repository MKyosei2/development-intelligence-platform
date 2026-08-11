# API Overview

## Core project APIs

```text
GET  /api/projects
POST /api/projects
```

## Collector / evidence ingestion

```text
POST /api/v1/collector-reports
POST /api/v1/webhooks/github
```

Collector and GitHub results are persisted through their existing paths and are
also converted into `EvidenceRecord` entries for Project Truth evaluation.

## Project Truth / PM dashboard

```text
GET  /api/v1/dashboard/projects
GET  /api/v1/dashboard/projects/{projectId}

GET  /api/v1/projects/{projectId}/claims
POST /api/v1/projects/{projectId}/claims
```

The dashboard API returns PM-first state such as Truth Status, reported and
evidence-backed progress, Evidence Confidence, Risk, Blockers, Report vs
Reality, and the Evidence Timeline. Existing Pipeline, Quality Gate, Metric and
Quality Issue fields remain available as technical evidence.

A Claim is a project member's report. It is stored separately from machine
Evidence and is evaluated as `Confirmed`, `PartiallyConfirmed`, `Unverified`,
`Contradicted`, or `Stale`. These states evaluate the statement only; they are
not a trust score for the reporter.

## Python analysis service

The independent FastAPI analysis service continues to expose its analysis
endpoints. It is an analysis/evidence provider and does not replace the Project
Truth evaluator.


## Project Truth P1

```text
POST /api/v1/qa/runs
GET  /api/v1/qa/runs/{runId}
GET  /api/v1/qa/projects/{projectId}/crash-clusters
GET  /api/v1/qa/runs/{runId}/artifacts
GET  /api/v1/qa/runs/{runId}/artifacts/{path}

POST /api/v1/hardware/projects/{projectId}/calibrate
POST /api/v1/hardware/profiles/relative
GET  /api/v1/hardware/profiles
POST /api/v1/hardware/simulate
POST /api/v1/hardware/estimate-minimum
```

QA and simulation write technical results as `EvidenceRecord` entries. The Project Truth evaluator remains the final project-level decision layer. Runtime and simulation executable paths are restricted to the configured QA workspace.

## P2 Decision Intelligence

```text
POST /api/v1/p2/community-hardware
GET  /api/v1/p2/community-hardware/confidence
POST /api/v1/p2/community-hardware/profile
POST /api/v1/p2/portability
POST /api/v1/p2/adaptive-qa-plan
POST /api/v1/p2/reliability/flaky-test
POST /api/v1/p2/reliability/runtime
POST /api/v1/p2/reliability/soak
POST /api/v1/p2/regression/bisect
POST /api/v1/p2/regression/origin
POST /api/v1/p2/mutation
POST /api/v1/p2/fuzz
POST /api/v1/p2/test-impact
POST /api/v1/p2/debug/symbolicate
POST /api/v1/p2/game/regression
POST /api/v1/p2/media/regression
POST /api/v1/p2/root-cause
GET  /api/v1/p2/projects/{projectId}/similar-incidents
GET  /api/v1/p2/projects/{projectId}/freshness
GET  /api/v1/p2/projects/{projectId}/conflicts
GET  /api/v1/p2/projects/{projectId}/schedule-risk
GET  /api/v1/p2/projects/{projectId}/completion-forecast
GET  /api/v1/p2/projects/{projectId}/stagnation
POST /api/v1/p2/dependency-risk
GET  /api/v1/p2/projects/{projectId}/daily-change
GET  /api/v1/p2/projects/{projectId}/health-change
GET  /api/v1/p2/projects/{projectId}/historical-risk
POST /api/v1/p2/decision-brief
GET  /api/v1/p2/projects/{projectId}/decision-traces
GET  /api/v1/p2/projects/{projectId}/intelligence
```

Execution endpoints accept only workspace-relative targets inside `Qa:WorkspaceRoot`. Community Hardware upload requires explicit consent. Prediction/AI endpoints produce decision-support context and cannot by themselves create a measured product failure.

