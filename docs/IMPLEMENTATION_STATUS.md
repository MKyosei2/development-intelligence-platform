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

Universal / unknown-language QA adapters were deferred in this historical phase; they are now implemented by the 2026-08-14 Universal QA layer documented later in this file.

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

## Development Intelligence expansion — 2026-08-12

The Upgrade8-based source now also implements the requested concrete expansion: Intelligence Dashboard aggregation, deterministic Game Session Regression Replay, platform Performance Budgets, an Unreal collector, real-device hardware backtesting/calibration, deterministic Change Risk, automatic evidence-based Regression Triage, Release Readiness gating, explicit GitHub Issue work-item creation, Release Provenance/CycloneDX SBOM bundles, OpenTelemetry/Prometheus/Grafana observability, and a Multi-repository Dependency Graph. The persistence model is extended by `202608120003_AddDevelopmentIntelligenceExpansion`.

These items are **implemented in source, not automatically runtime-verified**. Completion evidence still requires a .NET 8 build/test for the exact revision, clean and legacy PostgreSQL migration runs, live Redis/Worker durable-job tests, selected Local/MinIO artifact round trips, OTLP export verification, and real Unity/Unreal/device runs for engine/hardware-specific paths. Real-device calibration remains empty until measured samples are actually recorded, and portfolio benchmark `TBD` values remain untouched until fixed-environment measurements exist.

The GitHub Issues connector is the implemented external work-item path in this phase; Jira/Linear are not represented as implemented. The Universal/unknown-language QA abstraction that was deferred here is now implemented by the 2026-08-14 Universal QA layer documented later in this file.

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

## Project Control / Development Graph — 2026-08-12

Source implementation includes the central Development Graph, GitHub/Perforce version-control normalization, Change Impact/Blast Radius, Release Readiness integration, Milestone/Critical Path, Test Impact graph links, Review Intelligence, Feature Readiness, Acceptance Criteria Evidence, Asset Intelligence, cross-repository graph bridging, Decision Log/Brief, What-if simulation and Jenkins/normalized external CI ingestion.

This phase intentionally keeps hardcoded QA language scope to Python/C++/C#. It is **implemented in source, not runtime-verified** until the exact resulting revision passes .NET build/tests, PostgreSQL migration/application, connector smoke tests and live dashboard/API verification. See `docs/PROJECT_CONTROL_IMPLEMENTATION_20260812.md`.


## 2026-08-14 Universal QA S-priority implementation

| Area | Code state | Exact-revision completion evidence |
|---|---|---|
| Extensible `LanguageId` | Implemented | Domain/build tests on target .NET |
| Universal Adapter Registry | Implemented | Universal QA Bug Zoo |
| Build/Framework/Game Engine detection | Implemented | detector tests + Bug Zoo/profile evidence |
| SARIF / JSON / JUnit normalization | Implemented | normalizer tests + structured-report cases |
| Multi-language Bug Zoo | Implemented | `artifacts/qa-bug-zoo/qa-bug-zoo-results.json` |
| Unknown/proprietary language manifest | Implemented | `customlang` Bug Zoo cases |
| Formal Windows/.NET/Unity verification | Runner implemented | `artifacts/formal-portfolio-verification/FORMAL_PORTFOLIO_VERIFICATION_PASS.txt` for exact commit |
| Portfolio benchmark measurement | Runner implemented | `artifacts/portfolio-benchmark/portfolio-benchmark-results.json` for exact commit |

The former closed `QaLanguage` enum has been removed. Existing persisted values (`Python`, `CSharp`, `Cpp`) remain readable through `LanguageId` aliases, so the QA language column does not require a destructive schema migration. Python/C#/C++ remain deep specialized adapters; additional languages use the built-in generic adapter or `.dip/qa.json`. Structured diagnostics are normalized before textual fallbacks.

This generated source tree **does not claim Windows/.NET/Unity runtime PASS or benchmark numbers until those runners are executed on the target machine for the exact revision**. This prevents previous-revision evidence from being mislabeled.

## 2026-08-14 QA Governance / Reliability expansion

| Area | Source state | Exact-revision evidence |
|---|---|---|
| Flaky/Test Reliability | Implemented | Governance domain tests + repeated-run evidence |
| Finding lifecycle / Waiver | Implemented | Governance domain/API verification |
| Regression baseline | Implemented | baseline records + regression/bisect evidence |
| Ownership / CODEOWNERS fallback | Implemented | repository-rule verification |
| QA Policy / Release Gate | Implemented | `qa_policy_evaluations` + release-readiness integration |
| Execution sandbox boundary | Implemented | tool sandbox metadata + target OS verification |
| Provenance / reproducibility | Implemented | exact-commit `qa_evidence_provenance` |
| QA self-health | Implemented | health snapshots separated from product findings |
| Root-cause/change correlation | Implemented | finding/baseline correlation; existing evidence-root-cause remains authoritative evidence-ranking path |
| Quality trend / technical debt | Implemented | trend snapshots + Project Intelligence UI |
| Smart test selection | Implemented | path/test impact rules and API |
| Diagnostic schema versioning | Implemented | `dip.finding.v2` persisted on findings |
| Artifact retention | Implemented | retention rules applied by ArtifactService / retention worker |
| Freshness / confidence | Implemented | provenance age + QA self-health confidence |
| QA cost/runtime budget | Implemented | wall/tool/CPU/artifact cost records + policy checks |
| Test recommendation | Implemented | changed-path/flaky/coverage-gap recommendations |
| Automatic bug minimization | Implemented (safe argument reduction) | sandboxed reproduction evidence required |
| Cross-project benchmark comparison | Implemented | same-definition successful run comparison; environment comparability remains caller responsibility |

Formal completion requires `scripts/Verify-QaGovernance.ps1` and the existing exact-revision portfolio runner. The generation environment is not a substitute for Windows/.NET/Unity runtime evidence.



## 2026-08-16 Defect Intelligence / Universal QA expansion

| Area | Source state | Completion evidence |
|---|---|---|
| Deep Language Pack / capability planner | Implemented | exact-revision adapter tests + Bug Zoo |
| Universal structural AST + bounded data-flow | Implemented | unit tests + formal .NET run |
| Runtime Defect Intelligence | Implemented | runtime event -> Incident E2E |
| Verified Fix + Regression Test isolation | Implemented | disposable worktree QA E2E |
| Unity/Unreal Game QA expansion | Implemented | real Unity/Unreal runs |
| Sentry / OpenTelemetry ingest | Implemented | authenticated sample event E2E |
| Jira / Linear connectors | Implemented | configured sandbox integration test |
| Fuzz/property/race extensibility | Implemented | native tool availability or `.dip/qa.json` evidence |
| API/DB/distributed QA pack | Implemented, opt-in | `.dip/system-qa.json` E2E |
| Evidence-grounded Fix Agent | Implemented, default OFF | evidence-ID contract + VerifiedFix E2E |
| IDE/LSP diagnostics bridge | Implemented | API + bridge round trip |
| Native-toolchain benchmark | Measured where tools are installed | `artifacts/universal-qa-benchmark` |

The bootstrap benchmark is intentionally labeled `formalAdapterBenchmark=false`; it does not substitute for `Dip.QaBugZooVerifier`. This source package must not be described as fully CI-green until GitHub can actually start hosted runners and the exact packaged revision passes the .NET/integration/GUI/security workflows.

## 2026-08-16 Management Operations S+/S expansion

| Area | Source state | Runtime completion evidence |
|---|---|---|
| Daily Cockpit / My Actions / My Decisions / Approvals | Implemented | authenticated browser/API smoke |
| Risk / Decision / Action / Scope / Resource GUI CRUD | Implemented | Blazor runtime + persistence round trip |
| Slack / Teams / SMTP notifications | Implemented | configured test-destination delivery |
| Ack / Snooze / L1-L3 escalation | Implemented | Worker time-based escalation test |
| Formal approval + role binding + SoD | Implemented | authenticated multi-role approval E2E |
| GitHub / Jira / Linear bidirectional Action sync | Implemented; paginated + ProjectId-scoped | sandbox provider round trip + conflict replay |
| Google Sheets two-sided sync | Implemented | sheet snapshot/conflict/resolution E2E |
| Portfolio / Program Command Center | Implemented | multi-project authenticated browser test |
| Explicit cross-project Shared Resource Pool | Implemented | two-project capacity scenario |
| RACI responsibility + alert routing | Implemented | RACI -> notification route E2E |
| Daily / Weekly / Release meeting mode | Implemented | browser/API smoke |
| Work Calendar / PTO / holiday / meeting capacity | Implemented; all-day events respect configured working days | capacity fixture + Google Calendar sandbox sync |
| Source Authority / conflict resolution | Implemented | Manual/PreferAuthority provider conflict E2E |

Static integration contract evidence is generated by `tools/validate_management_operations.py` into `artifacts/management-operations-validation-20260816/static-contract.json`. It validates wiring, authorization declarations, Web/API surfaces, conflict-safety contracts and configuration presence, but it **does not claim .NET compilation or live third-party delivery/synchronization**. Those remain exact-revision runtime completion requirements.

\n\n## 2026-08-16 Portfolio publication hardening\n\nThe source package now includes reviewer-facing portfolio hardening without weakening the existing exact-revision verification policy:\n\n- concise README with deterministic sample-data screenshots/GIF and architecture overview;\n- fail-closed `tools/verify_publication_safety.py` plus GitHub Security Scan as the publication gate;\n- exact-SHA required-workflow gate before repository visibility can be changed to public;\n- commit-bound historical benchmark / QA Bug Zoo / Windows .NET evidence snapshots;\n- no zero-byte `.razor` pages in the package;\n- `ManagementOperations` split into Razor view, code-behind and reusable cockpit/tab components;\n- Conventional Commit validation for future pull requests;\n- guarded `v1.0.0-portfolio` tag/release path with build, unit tests, safety checks and SHA-256 release manifests.\n\n**Important:** these additions do not claim the packaged revision is CI-green before a runner executes it. A workflow that fails before any job step starts is treated as infrastructure-unavailable, never as PASS. Public visibility and the GitHub Release are external state changes and are intentionally gated until the modified revision itself has successful required workflows.\n