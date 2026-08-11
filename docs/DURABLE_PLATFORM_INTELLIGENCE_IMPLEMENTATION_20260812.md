# Durable Platform Intelligence implementation — 2026-08-12

Base repository: `MKyosei2/development-intelligence-platform`  
Observed base revision: `2dd237468b61076bdaf25d4ac48dcf9ad459168d` (`Upgrade7`)

This change set implements the requested durable platform-intelligence foundation. Source implementation and static/package validation are distinct from runtime verification: the packaging environment does not contain the .NET 8 SDK, PowerShell, Docker, or Unity, so it does not claim a successful .NET build, live PostgreSQL migration, live Redis job execution, MinIO round trip, Integration/E2E run, or Unity run.

## 1. Formal EF Core migration baseline

- `202608120001_InitialBaseline` freezes the pre-migration DIP schema.
- `202608120002_AddDurablePlatformIntelligence` adds the durable artifact/job/incident/benchmark tables.
- `DipDbContextModelSnapshot` freezes the model for future EF diffs.
- `LegacyMigrationAdopter` detects an old `EnsureCreated` installation, validates the required legacy tables/critical columns/PK/FK/index contracts, and only then records the baseline migration as applied.
- A legacy mismatch fails closed with `LegacySchemaMismatchException`; it is never silently marked migrated.
- `DbInitializer` no longer uses `EnsureCreated` as the normal evolution path; it runs adoption if required, then `MigrateAsync`.
- `P0SchemaBootstrapper` remains only as a historical compatibility helper and is no longer the normal schema evolution mechanism.

## 2. Durable Artifact Store

Domain/persistence:
- `ArtifactBlob`: content-addressed binary identity (`SHA-256`, size, MIME type, provider, storage key).
- `ArtifactRecord`: project/job/QA/incident/benchmark metadata reference and retention policy.
- Blob insertion uses PostgreSQL `ON CONFLICT (storage_provider, sha256) DO NOTHING` to make concurrent metadata deduplication safe.
- Artifact references to Job, QaRun, Incident, and BenchmarkRun are database foreign keys with `ON DELETE SET NULL`.

Storage implementations:
- `LocalArtifactStore` for development/test.
- `MinioArtifactStore` using S3 Signature V4 for MinIO/S3-compatible storage.
- Content-addressed keys: `sha256/<prefix>/<digest>`.
- Local storage performs root-bound path validation.
- API and Worker share `dip-artifact-data` when Local storage is used under Docker.
- `ArtifactRetentionWorker` removes expired records and deletes a blob only after its last record disappears.

Main API:
- `GET /api/v1/artifacts/{artifactId}`
- `GET /api/v1/jobs/{jobId}/artifacts`

For MinIO under Docker, run the storage profile and set `DIP_ARTIFACT_PROVIDER=Minio`.

## 3. Durable Async Job System

`DurableJob` is stored in PostgreSQL and is the source of truth. Redis is transport only.

State model:
`Queued -> Running -> Succeeded | Failed | Cancelled | TimedOut`

Durability controls:
- transactional Outbox enqueue,
- dedicated `dip:jobs` Redis stream and consumer group,
- database row lock during claim,
- lease owner / lease expiry / heartbeat,
- explicit cancellation,
- retry bounded by `MaxAttempts`,
- `DurableJobRecoveryWorker` requeues expired leases or fails exhausted jobs,
- worker-stop cancellation intentionally leaves the lease for recovery instead of falsely marking the job failed.

Job execution success is deliberately separate from QA/business verdict. A benchmark job can execute successfully while its benchmark comparison produces a failed quality result.

Main API:
- `GET /api/v1/jobs/{jobId}`
- `GET /api/v1/jobs/projects/{projectId}`
- `POST /api/v1/jobs/{jobId}/cancel`
- `POST /api/v1/jobs/{jobId}/retry`

## 4. P2 operations moved onto durable jobs

New asynchronous routes return `202 Accepted + jobId`:
- `POST /api/v1/p2/jobs/fuzz`
- `POST /api/v1/p2/jobs/mutation`
- `POST /api/v1/p2/jobs/soak`
- `POST /api/v1/p2/jobs/bisect`
- `POST /api/v1/p2/jobs/media`

Handlers reuse the existing P2 services rather than duplicating test logic:
- `FuzzJobHandler`
- `MutationJobHandler`
- `SoakJobHandler`
- `GitBisectJobHandler`
- `MediaRegressionJobHandler`

The existing synchronous P2 routes remain for compatibility during migration to the durable path.

## 5. Unified Incident Timeline / Incident Intelligence

New model:
- `Incident`: unique logical incident per `(ProjectId, Fingerprint)`.
- `IncidentOccurrence`: each observed QA/job occurrence.
- `IncidentEvent`: chronological timeline.

Event types include Observed, Reproduced, Symbolicated, RegressionLocated, RootCauseUpdated, ArtifactAttached, BenchmarkRegression, Resolved, and Reopened.

Behavior:
- QA Warning/Critical findings aggregate by stable QA fingerprint.
- Synthetic Fuzz/Soak/Media/Benchmark fingerprints longer than 128 characters are normalized to stable SHA-256 fingerprints.
- A matching occurrence after resolution reopens the same incident.
- Artifact records are linked back to the incident as well as represented in timeline events.

API:
- `GET /api/v1/incidents/projects/{projectId}`
- `GET /api/v1/incidents/{incidentId}`
- `POST /api/v1/incidents/{incidentId}/resolve`

The existing Crash Cluster remains intact; Incident Intelligence is a durable lifecycle/timeline layer above the existing fingerprint-based QA evidence.

## 6. Symbolication / Regression Origin / Root Cause integration

Existing P2 services now update the latest matching open Incident:
- Git bisect and analytical regression detection -> `RegressionLocated` + suspected commit.
- Native symbolication -> `Symbolicated` timeline event.
- Evidence-grounded root-cause analysis -> `RootCauseUpdated` + root-cause summary.

No AI result directly changes code or independently proves a root cause; it remains evidence-grounded contextual intelligence.

## 7–9. Automated Benchmark Evidence

Model:
- `BenchmarkDefinition`
- `BenchmarkRun`
- `BenchmarkMetric`
- `BenchmarkComparison`

Execution:
- `BenchmarkJobHandler` runs through the Durable Job system.
- HTTP and process benchmarks are supported.
- Warmups are excluded from measured samples.
- Results include p50/p95/p99 latency, error rate, and observed throughput.
- Baselines require the same project, benchmark definition, environment fingerprint, and configuration hash, and exclude the current commit.
- Throughput is direction-aware (decrease is regression); latency/error-rate increases are regressions.
- User-provided/fixed thresholds classify Passed/Warning/Failed comparisons.

Evidence chain:
`Benchmark Job -> BenchmarkRun/Metrics/Comparison -> Artifact Store -> EvidenceRecord -> Project Truth -> Incident (for warning/failure regression)`

Each benchmark run stores four durable JSON artifacts:
- `benchmark-summary.json`
- `raw-samples.json`
- `execution-context.json`
- `comparison.json`

No benchmark values are invented. Existing portfolio benchmark `TBD` values remain `TBD` until a real fixed environment run produces measurements.

API:
- `POST /api/v1/benchmarks/definitions`
- `GET /api/v1/benchmarks/definitions`
- `POST /api/v1/benchmarks/runs`
- `GET /api/v1/benchmarks/runs/{runId}`

## Verification contract

Package/static validation checks source structure, parseable JSON/XML/YAML, solution references, migration/model coverage, new routes, handler registration, integration hooks, and absence of merge-conflict markers. It also runs executable Python/native checks when those toolchains are present.

Final Windows/.NET proof should run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-DurablePlatformIntelligence.ps1
```

For live infrastructure verification, also run the Integration/E2E workflows against PostgreSQL/Redis and, when selected, MinIO. A revision is not described as runtime-verified until those checks are green for that exact revision.

## Deferred by design

Universal/unknown-language QA abstraction is intentionally unchanged and remains the final architectural phase, after the concrete Python/C#/C++/Unity/game QA paths are runtime-verified.
