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
GET  /api/v1/qa/runs/{runId}/artifacts
GET  /api/v1/qa/runs/{runId}/artifacts/{path}

POST /api/v1/hardware/projects/{projectId}/calibrate
POST /api/v1/hardware/profiles/relative
GET  /api/v1/hardware/profiles
POST /api/v1/hardware/simulate
POST /api/v1/hardware/estimate-minimum
```

QA and simulation write technical results as `EvidenceRecord` entries. The Project Truth evaluator remains the final project-level decision layer. Runtime and simulation executable paths are restricted to the configured QA workspace.
