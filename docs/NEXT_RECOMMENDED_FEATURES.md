# Recommended next features after Upgrade6 repair

This list intentionally separates **future product expansion** from the current
Upgrade6 build/CI repair. The current priority is to keep the latest revision
buildable and evidence-backed before adding more architectural breadth.

## Priority A — add after the current CI revision is green

1. **Unified incident timeline on top of the existing Crash Cluster**
   - Keep the current stable-fingerprint crash clustering as the base.
   - Join cluster history with regression origin, symbolicated frames, build/version and resolved/reopened state.
   - Rank recurring regressions by severity × recurrence × recency without scoring people.

2. **Formal EF Core migration baseline**
   - Replace bootstrap-only schema evolution with an `InitialCreate` baseline.
   - Add upgrade/downgrade compatibility tests and migration validation in CI.
   - Keep idempotent upgrade paths for existing local installations.

3. **Durable asynchronous P2 jobs**
   - `POST /jobs` -> `202 Accepted + jobId`.
   - Worker execution for soak, fuzz, mutation, bisect, media and hardware searches.
   - Status/progress/cancel/retry endpoints with idempotency and timeout policies.

4. **Durable artifact storage and retention**
   - Introduce `IArtifactStore` with local + S3/Azure Blob/MinIO implementations.
   - Content hashing, deduplication, retention, metadata, signed download URLs and cleanup policies.

5. **Automated portfolio/benchmark evidence**
   - Populate the current `TBD` benchmark table automatically.
   - Measure API p50/p95/p99, collector throughput, 100 req/s error rate and 1M-metric query latency.
   - Store benchmark provenance (commit, hardware, runtime, dataset version) and trend charts.

## Priority B — strong game-development expansion

6. **Unreal Engine collector**
   - Asset/reference/build configuration checks.
   - Unreal Insights/performance ingestion.
   - Crash dump + symbolication + regression-origin integration.
   - Automation Test ingestion and project/package validation.

7. **Real-device hardware calibration / backtesting**
   - Compare simulated minimum-hardware estimates with measured devices.
   - Calibrate CPU/GPU/memory/storage/thermal coefficients from real runs.
   - Track forecast error and confidence calibration over time.

8. **Game-session regression replay**
   - Capture deterministic/replayable scenarios, seed, build, map/scene and input trace.
   - Re-run the same scenario against candidate revisions and compare frame-time/network/visual evidence.

9. **Performance budget profiles per platform**
   - PC/console/mobile/VR presets with FPS, frame-time, memory, GPU/CPU, network and thermal envelopes.
   - Per-scene/per-level budgets and escalation rules.

## Priority C — production and team integration

10. **External work-item connectors**
    - GitHub Issues/Projects first, then Jira/Linear-compatible adapter boundary.
    - Map claims, blockers and evidence to canonical work items without turning people into scores.

11. **Release evidence / provenance**
    - SBOM, dependency vulnerability summary, artifact hash manifest and build provenance.
    - Signed release evidence bundle tied to the exact commit and CI run.

12. **Operational observability**
    - OpenTelemetry traces/metrics/log correlation.
    - Queue depth, job latency, artifact-store errors, database latency and webhook failure dashboards.

13. **Multi-repository dependency graph**
    - Cross-repo service/tool/asset dependencies.
    - Change impact and release risk propagation across repositories.

## Final architectural phase

14. **Universal / unknown-language QA abstraction — IMPLEMENTED 2026-08-14**
    - Replaced the closed language enum with extensible `LanguageId`.
    - Added capability-based adapters and `.dip/qa.json` for new/proprietary languages.
    - Kept language-specific evidence and reproducibility contracts explicit.
    - Exact-revision runtime evidence is still required for every toolchain that is claimed as verified.

This item was originally deferred so the abstraction could be derived from concrete QA paths rather than hypothetical languages.
