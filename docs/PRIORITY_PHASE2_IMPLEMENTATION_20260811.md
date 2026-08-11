# Priority implementation Phase 2 — 2026-08-11

Upstream baseline checked before this phase: `main` at `6c38694cd56d3b4b34b3aafebe7419e17b780389` (`Upgrade5`).

This phase continues from the first priority package and deliberately leaves Universal/unknown-language QA for the final architecture stage.

## Implemented

### 1. Test Impact Analysis
- New `ITestImpactAnalyzer` / `TestImpactAnalyzer`.
- Resolves changed files inside the configured QA workspace.
- Uses `.csproj` `ProjectReference` edges as the strongest signal.
- Ranks tests using project dependency, changed file/name overlap, directory proximity, and changed source tokens.
- Returns selected tests, unresolved changed paths, and estimated test-count reduction.
- API: `POST /api/v1/p2/test-impact`.
- Recorded as `P2IntelligenceKind.TestImpact` without treating test selection itself as product-quality evidence.

### 2. Game-specific regression gate
- Deterministic baseline/current metric comparison.
- Understands higher-is-better FPS/throughput and lower-is-better frame time, CPU/GPU time, memory, GC, draw calls, triangles, latency and related metrics.
- Enforces target FPS absolute frame budget in addition to relative regression.
- Adds online-play envelopes for packet loss, jitter, RTT/ping latency.
- API: `POST /api/v1/p2/game/regression`.
- Produces measured performance Evidence and recalculates Project Truth.

### 3. Fuzzing hardening
- Deterministic boundary-value and patterned seed inputs.
- Separates seed / interesting / crash / timeout corpora.
- Persists failure inputs with stable failure fingerprints.
- Deduplicates failures by fingerprint.
- Performs bounded failure-preserving input minimization.
- Writes `fuzz-manifest.json` with corpus and failure references.

### 4. Mutation testing hardening
- Requires a passing unmodified baseline before scoring mutants.
- Keeps isolated workspace behavior.
- Rejects ambiguous explicit mutations instead of mutating an arbitrary occurrence.
- Records file/line/column/operator per mutant.
- Can generate common boundary/equality/boolean/arithmetic mutations from explicitly supplied source files.
- Persists per-mutant patch text and execution logs.

### 5. Crash clustering
- Adds project-level QA finding grouping by the existing stable fingerprint.
- Returns occurrences, first/last seen time, reproduced occurrence count, severity and involved subjects.
- API: `GET /api/v1/qa/projects/{projectId}/crash-clusters`.

### 6. Native symbolication
- Resolves native addresses through `llvm-symbolizer` or `addr2line` when available.
- Binary paths remain constrained to the configured QA workspace.
- Emits function/file/line frames and `symbolication.json`.
- Tool absence/start failure is kept as `InfrastructureFailure`, not product failure.
- API: `POST /api/v1/p2/debug/symbolicate`.

### 7. Verification integration
`Verify-P2.ps1` now requires the Test Impact implementation and new unit-test sources, and statically verifies the hardened Fuzz/Mutation/Test Impact/Crash Cluster markers before runtime verification.

## Verification policy

The generated package is not allowed to claim runtime verification merely because source files exist. The generation environment has no .NET SDK or Unity Editor. Run `scripts/Verify-P2.ps1` and then `scripts/Verify-PortfolioReadiness.ps1` on the target Windows machine before declaring this phase VERIFIED.
