# Implementation status

This document distinguishes implemented code from verified completion.

## Current code state

| Area | Code state | Completion evidence |
|---|---|---|
| Project Truth / Evidence model | Implemented for P0 | Domain/Application tests + live E2E |
| Claim vs Evidence | Implemented for P0 | Claim verification tests + Project Truth E2E |
| Evidence Confidence | Implemented for P0 | Evaluator tests + dashboard observation |
| Evidence-backed / Reported Progress | Implemented for P0 | Progress evaluator tests + Project Truth E2E |
| Risk / Blocker evaluation | Implemented for P0 | Project Truth evaluator tests + live E2E |
| Evidence Timeline | Implemented for P0 dashboard | Dashboard query + live data inspection |
| PM-first Blazor dashboard | Implemented for P0 | Dashboard query tests + live API/E2E |
| Existing C# backend / Collector path | Preserved and extended as Evidence Provider | Unit + integration tests |
| Worker / Redis | Preserved | Live E2E workflow |
| Quality Gate | Preserved as one Evidence Provider | Quality unit tests + live E2E |
| Python analysis | Independent FastAPI service implemented | Python test workflow |
| C++ native collector | Implemented under `collectors/native-collector` | Native collector CI |
| GitHub Actions | Workflows included | Complete only when the latest commit is green |

## P0 completion rule

Project Truth P0 must not be described as verified merely because source files
exist. It is verified only when the resulting repository revision has green
`.NET CI`, `Integration and E2E Tests`, Python tests, and Native Collector CI,
or when Windows local verification succeeds and
`scripts/Verify-Solution.ps1` creates the completion artifact.

The P0 implementation deliberately keeps two concepts separate:

- **Claim**: what a person reports.
- **Evidence**: what GitHub, CI, collectors, runtime/quality/performance checks,
  or other observable systems actually report.

The platform never assigns a person a truthfulness score. It only evaluates the
current relationship between a Claim and the available Evidence.

## Database compatibility

A formal EF Core migration baseline is now implemented in source.
`202608120001_InitialBaseline` represents the pre-migration DIP schema and
`202608120002_AddDurablePlatformIntelligence` adds the durable artifact/job/
incident/benchmark schema. `LegacyMigrationAdopter` validates a historical
`EnsureCreated` database before recording the baseline as applied; incompatible
legacy schemas fail closed instead of being silently adopted. `DbInitializer`
then uses `MigrateAsync` as the normal schema-evolution path.

This source state is **not live-migration verified** until both a clean PostgreSQL
database and a representative historical DIP database have successfully run the
migration path in the target .NET/PostgreSQL environment.

## P1 technical evidence expansion

P1 source paths for hardcoded Python/C#/C++ QA, fingerprint-based automatic reproduction, Windows capture/audio/dump helpers, hardware calibration, relative performance simulation, PM explanation, and Project Truth integration are included. P1 is **not verified** until `docs/P1_IMPLEMENTATION_STATUS.md` requirements have been exercised, including Windows-specific native evidence.

## P2 Predictive Development Intelligence

Source implementation includes Community Hardware aggregation, Minimum Hardware Finder, Adaptive QA Planner, Git regression-origin search, flaky/soak reliability, fuzz/mutation testing, media regression, evidence governance, portability analysis, evidence-grounded root-cause candidates, forecasts, and PM decision briefs.

The completion-hardening changes additionally:

- fix the current `.NET CI` compile blockers in `Home.razor` and `PerformanceSimulationService`;
- route Minimum Hardware boundary probes through the existing constrained performance simulation instead of relying on target-defined environment variables;
- make Soak an observation window and sample target-process memory rather than DIP's managed heap;
- isolate regression and mutation execution in disposable Git worktrees;
- prevent low-precision media fallback from being stored as High-confidence evidence;
- derive completion velocity from Project Truth progress history instead of raw Evidence volume;
- deduplicate recent Community Hardware retries with a one-way consent-scoped pseudonym;
- add P2 live API E2E coverage and execution-safety unit coverage;
- default non-Development API configuration to authenticated access, while Development explicitly keeps local unauthenticated verification behavior.

P2 must **not** be declared runtime-verified until `scripts/Verify-P2.ps1` completes without skip switches in the target Windows/.NET environment and the resulting repository revision is green in CI. Simulation-derived hardware results remain explicitly distinct from real-device evidence.

## 2026-08-11 priority hardening

| Area | Code state | Required completion evidence |
|---|---|---|
| QA Bug Zoo evaluator | Implemented | `artifacts/qa-bug-zoo/qa-bug-zoo-results.json` generated by the real Python/C#/C++ QA engines |
| QA clean controls / false-positive measurement | Implemented | Bug Zoo report |
| Stable-fingerprint re-run measurement | Implemented | Bug Zoo reproduction metric |
| Unity Build Settings QA | Implemented | Unity BatchMode + EditMode evidence |
| Unity Animation QA | Implemented | Unity BatchMode + EditMode evidence |
| Unity Scene missing-script/reference QA | Implemented | Unity BatchMode report |
| Unity Runtime PerformanceProbe | Implemented | Real Unity Player/Editor runtime metrics |
| Portfolio readiness orchestration | Implemented | `PORTFOLIO_READINESS_PASS.txt` only after all non-skipped verification phases pass |

Universal / unknown-language QA adapters are intentionally **not** part of this phase. They remain a final-stage architectural generalization after the current QA and game-development evidence paths are validated.

## Durable platform-intelligence implementation — 2026-08-12

Source implementation now includes:

- formal EF Core baseline + fail-closed legacy adoption;
- durable artifact metadata/blob storage with Local and MinIO providers plus retention;
- PostgreSQL-source-of-truth asynchronous jobs with Redis delivery, leases, heartbeat, cancellation, retry and recovery;
- durable Fuzz/Mutation/Soak/Git Bisect/Media execution paths;
- Incident/Occurrence/Event lifecycle and timeline aggregation above existing QA crash fingerprints;
- regression-origin, native symbolication and evidence-grounded root-cause incident connections;
- benchmark definitions/runs/metrics/comparisons, durable benchmark jobs and Benchmark -> Artifact -> Evidence -> Project Truth -> Incident integration.

These items are marked **implemented in source**, not runtime-verified. Runtime completion requires a .NET 8 build/test pass plus live PostgreSQL/Redis and selected Artifact Store verification for the exact resulting revision.

## Remaining production-hardening work

The following remain outside this changeset:

- external Claim/work-item connectors and canonical work-item resolution across issue trackers/chat tools;
- real-device calibration/backtesting needed to validate minimum-hardware and forecast accuracy at production scale;
- fixed-environment benchmark execution that replaces portfolio `TBD` values with measured evidence;
- broader operational observability and multi-repository dependency intelligence.

## 2026-08-11 Upgrade6 CI repair package

The Upgrade6 repair package addresses the seven compiler errors observed in the
latest `.NET CI` run (three missing P2 E2E client methods, two missing Web API
client methods and two `MediaRegressionRunner` declaration-space collisions).
It also restores current-main compatibility files missing from the prior
reconstructed package and registers the restored legacy intelligence services.

Static package validation is recorded in
`artifacts/package-validation/UPGRADE6_FIX_STATIC_VALIDATION.txt`.
The revision is still **not runtime-verified** until a .NET 8 build/test run and
live Integration/E2E workflow succeed for the exact resulting revision.
