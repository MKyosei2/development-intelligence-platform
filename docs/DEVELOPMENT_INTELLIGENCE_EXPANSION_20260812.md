# Development Intelligence Expansion — 2026-08-12

Base repository: `MKyosei2/development-intelligence-platform`  
Observed base revision: `3e43dcce11dc5bf67a7918acd2d55d912e41c97a` (`Upgrade8`)

This expansion builds on the existing Project Truth P0, Technical Evidence P1, Decision Intelligence P2, formal EF migrations, durable artifacts/jobs, unified incidents, and benchmark evidence. The twelve requested product features are integrated into the existing Domain/Application/Infrastructure/API/Worker/Web/Collector structure rather than implemented as a separate prototype.

Source implementation is not equivalent to runtime verification. A feature is only described as runtime-verified after the exact repository revision has passed the relevant .NET/CI, PostgreSQL/Redis, engine, device, and observability checks.

## 1. Intelligence Dashboard

`IntelligenceDashboardService` and `IntelligenceDashboardController` aggregate the current project state into one read model:

- active/critical incidents;
- durable jobs and failures;
- deterministic replay sessions;
- performance-budget failures;
- latest change risk;
- latest release-readiness decision;
- real-device hardware calibration summary;
- external work-item links;
- repository/dependency counts.

`ProjectIntelligence.razor` consumes this read model and retains the existing forecast/decision-intelligence content. It does not replace Project Truth; it presents evidence-backed operational state above the detailed evidence paths.

## 2. Game Session Regression Replay

The replay path is intentionally deterministic and engine-aware rather than a generic command QA abstraction.

- `GameReplaySession`: scene, durable input-trace artifact, seed, duration, launch executable and baseline commit.
- `GameReplayRun` / `GameReplayMetric`: exact commit/build/hardware execution and aggregated metrics.
- `GameSessionReplayJobHandler`: runs replay through the existing durable-job system.
- `GameReplayExecutor`: launches without a shell and limits executable/working-directory resolution to `GameReplay:WorkspaceRoot` unless explicitly configured otherwise.
- Replay input must be a durable `ArtifactRecord`; the Worker materializes it into a temporary run directory.
- Unity `ReplayProtocolProbe` activates only under `DIP_REPLAY_MODE=1`, initializes the Unity random seed, replays semantic input events, emits frame/FPS/memory metrics, and exits after the configured duration.
- Replay results, stdout and stderr become durable artifacts and evidence. Crash/timeout/performance/budget failures become incidents.

Replay API includes an authenticated JSON/JSONL input-artifact upload with a 64 MiB limit, session creation/listing, and durable run enqueue.

## 3. Platform Performance Budget

`PerformanceBudgetProfile` and `PerformanceBudgetEvaluation` provide target-specific budgets. The migration seeds global defaults for:

- PC Low;
- PC Recommended;
- Console 60 FPS;
- Console 30 FPS;
- Mobile Low;
- Mobile High;
- VR 90 Hz.

These defaults are editable product presets, not claims of universal platform certification thresholds. Project-specific profiles can override them. Evaluation checks metric/aggregation pairs, hard boundaries and warning margins and is reusable from replay and benchmark paths. A project cannot evaluate another project's private profile.

## 4. Unreal Engine Collector

`collectors/unreal-collector` is a dependency-free Python collector that emits the existing DIP CollectorReport schema. It collects:

- `.uproject` identity;
- Content/Map/Source/Config/Plugin counts;
- Unreal log errors/warnings;
- Unreal Automation Test report pass/fail counts;
- Saved/Crashes crash-artifact detection;
- optional Unreal Insights CSV metrics.

A dedicated GitHub Actions workflow and collector tests are included. The initial implementation deliberately stays concrete to Unreal; it does not introduce the deferred unknown-language QA adapter.

## 5. Real-device Calibration / Backtesting

`HardwareBacktest` stores predicted and measured FPS for a concrete hardware profile/scenario/commit/environment fingerprint. `HardwareBacktestingService` reports:

- sample count;
- mean signed error;
- MAE;
- RMSE;
- calibration factor;
- confidence derived from observed absolute error.

Minimum Hardware keeps simulation and real-device evidence separate. After at least three real-device samples, the historical calibration factor can bound/correct the simulated safety recommendation and the evidence payload records both raw simulation and the correction. No measured result is invented when samples do not exist.

## 6. Change Risk Intelligence

`ChangeRiskAssessment` is deterministic and does **not** score people. Inputs are limited to change/evidence characteristics:

- changed files/lines;
- supplied direct-test coverage;
- related incidents/regressions;
- performance/security/schema-sensitive paths;
- cross-repository impact.

`GitHubChangeRiskWorker` consumes push events in its own Redis consumer group and creates assessments automatically. High/Critical assessments feed the existing Adaptive QA Planner so game replay, performance budgets, fuzzing and soak can be requested for higher-risk changes.

## 7. Automatic Regression Triage

`RegressionTriageService` combines an incident's occurrences/events, regression commit, symbolication/root-cause events, evidence and latest change risk into a deterministic triage packet with:

- suspected commit;
- suspected subsystem/files;
- evidence references;
- confidence;
- optional suggested owner label without person scoring.

`AutomaticRegressionTriageWorker` periodically triages unresolved non-informational incidents that do not already have a current triage record.

## 8. Release Readiness Gate

`ReleaseReadinessService` evaluates an exact project/commit using:

- unresolved critical incidents;
- durable job failure/running state;
- recent performance-budget results;
- evidence freshness / Project Truth blockers;
- exact-commit release-evidence bundle;
- latest change risk.

It persists `GO`, `CONDITIONAL GO`, or `NO-GO`, blockers, warnings, required actions and confidence. `POST /api/v1/release-readiness/gate` is CI-friendly: `NO-GO` returns HTTP 409 and conditional releases return 412 unless explicitly allowed.

## 9. External Work-item Connector

The first concrete connector is GitHub Issues through the existing GitHub token configuration.

- Incident -> issue preview is read-only.
- Issue creation requires an explicit ProjectWrite call; DIP never auto-spams trackers.
- Existing links are reused for idempotency.
- The resulting URL/external ID is stored as `ExternalWorkItemLink` and added to the incident timeline.

The application boundary can support additional trackers later, but Jira/Linear are intentionally not faked as implemented.

## 10. Release Provenance / SBOM

`ReleaseEvidenceJobHandler` creates a durable release-evidence bundle for an exact commit/build:

- CycloneDX 1.5 SBOM JSON;
- provenance manifest;
- selected release file hashes;
- ZIP bundle;
- bundle SHA-256;
- durable `ReleaseEvidenceBundle` record and EvidenceRecord.

`ReleaseProvenanceCollector` limits repository scanning to `ReleaseProvenance:AllowedRoot` and skips transient/dependency directories. Package references from `.csproj`, `package.json`, and `requirements*.txt` are collected without claiming unverifiable dependency provenance.

## 11. OpenTelemetry Observability

API and Worker register the OpenTelemetry SDK with OTLP export for:

- ASP.NET Core / HTTP traces;
- runtime metrics;
- `DIP.DevelopmentIntelligence` custom ActivitySource/Meter.

Custom metrics include durable job lifecycle/duration, queue depth, lease recovery, artifact count/bytes and existing API metrics. Docker Compose exposes OTLP to the OpenTelemetry Collector, which also scrapes API/Worker/Python Prometheus endpoints. Prometheus/Grafana provisioning includes a DIP operational dashboard.

## 12. Multi-repository Dependency Graph

`RepositoryNode`, `RepositoryDependency`, and `CrossRepositoryImpact` represent project-local repository topology and transitive change impact. Dependencies have explicit contract keys/types and optional criticality. Cross-repository impact feeds Change Risk but does not infer undocumented dependencies automatically.

## EF migration

`202608120003_AddDevelopmentIntelligenceExpansion` adds the new persistence tables, the replay artifact foreign-key connection, indexes, and global performance-budget presets. `DipDbContextModelSnapshot` and `DipDbContext` include the same new entity set. Existing `001` baseline and `002` durable-platform migrations remain unchanged in role.

## Security boundaries added in this phase

- no shell execution for replay;
- replay executable/working directory root confinement;
- durable replay input artifacts instead of arbitrary file paths;
- provenance repository-root confinement;
- explicit ProjectWrite requirement for GitHub issue creation;
- performance-budget project-scope validation;
- Change Risk uses technical/evidence properties rather than employee scoring.

## Verification

Run on Windows/.NET 8:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-DevelopmentIntelligenceExpansion.ps1
```

Use `-RunIntegration` and `-RunEndToEnd` only when the required PostgreSQL/Redis/API/Worker stack is running. Engine/device validation remains separate: Unity replay needs a real Unity Player/Editor path, Unreal collection should be exercised against a real Unreal project/Insights export, and hardware backtesting only becomes measured evidence after real-device samples are submitted.

The packaging environment used to assemble this source does not have .NET 8, PowerShell, Docker, Unity, or Unreal Engine. Therefore source/static validation, Python collector tests, and native tests must not be interpreted as proof that those unavailable runtime paths passed.

## Intentionally deferred

Universal/unknown-language QA remains the **final** architectural phase. The current expansion strengthens concrete Python/C#/C++/Unity/Unreal/game-development paths first so any later abstraction is derived from verified behavior rather than hypothetical adapters.
