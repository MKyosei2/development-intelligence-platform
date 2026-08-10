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

The repository currently has no EF Core migration baseline. Existing
installations created through `EnsureCreated` are therefore extended by the
idempotent `P0SchemaBootstrapper`, which creates only the new Project Truth
P0 tables when the application is still in no-migrations mode. A formal
migration baseline should replace this bootstrapper in a later database
migration phase.


## P1 technical evidence expansion

P1 source paths for hardcoded Python/C#/C++ QA, fingerprint-based automatic reproduction, Windows capture/audio/dump helpers, hardware calibration, relative performance simulation, PM explanation, and Project Truth integration are included. P1 is **not verified** until `docs/P1_IMPLEMENTATION_STATUS.md` requirements have been exercised, including Windows-specific native evidence.

## P2 Predictive Development Intelligence

Source implementation now includes Community Hardware aggregation, Minimum Hardware Finder, Adaptive QA Planner, Git regression-origin search, flaky/soak reliability, fuzz/mutation testing, media regression, evidence governance, portability analysis, evidence-grounded root-cause candidates, forecasts, and PM decision briefs. P2 must not be declared runtime-verified until `scripts/Verify-P2.ps1` passes in the target Windows/.NET environment. Simulation-derived hardware results remain explicitly distinct from real-device evidence.
