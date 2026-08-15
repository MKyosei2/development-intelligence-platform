# P2 changeset — Decision Intelligence

Base: `29d9b15dd02ad65f173f40230f995c9a7fcf3185` (`Upgrade2`).

P2 extends the P0 Project Truth / P1 Technical Evidence system. It does not
replace the core with a QA or AI platform.

## Domain / persistence

Added durable P2 records for:

- `P2IntelligenceRecord`
- privacy-minimised `HardwareCommunitySample`
- `AdaptiveQaPlanSnapshot`
- `DecisionTrace`

Added EF mappings, repositories, DbSets and no-migration bootstrap SQL for
existing `EnsureCreated` installations.

## Hardware intelligence

- explicit opt-in Community Hardware Database
- pseudonymous SHA-256 hardware key
- sample/real-device/OS/driver diversity confidence
- conservative percentile metadata
- Community Aggregate Hardware Profile
- Portability Analyzer
- full P2 Minimum Hardware boundary search for CPU, GPU, memory bandwidth,
  storage and memory budget
- separate safety-margin recommended profile

## Adaptive verification

- changed-file/tag risk classification
- historical-failure weighting
- QA cost estimator
- budget-aware plan selection
- no universal language adapter; Python/C#/C++ remain explicit P1 hardcoded
  evidence providers

## Regression / reliability

- flaky-test repeated execution
- runtime reliability repeated execution
- soak telemetry
- memory growth and performance drift
- isolated-worktree Git regression bisect
- regression-origin analysis
- bounded deterministic fuzzing
- isolated mutation testing
- InfrastructureFailure separation so runner/tool failures are not product
  failures

## Media regression

- image regression + difference artifact
- sampled video regression + first-difference time
- audio RMS/silence/clipping/sample comparison
- ffmpeg/ffprobe dependency failures classified as InfrastructureFailure

## Root-cause intelligence

- deterministic evidence clustering
- optional OpenAI-compatible external endpoint, disabled by default
- evidence-ID grounding validation
- ungrounded AI candidates discarded
- Similar Incident search
- human-review Suggested Fix only; no auto-fix/auto-PR

## Project / PM intelligence

- Evidence Freshness and confidence decay
- authority-weighted Evidence Conflict Resolution
- Historical Risk
- Schedule Risk estimate
- Completion Forecast range
- Work Stagnation detection without person accusation
- Dependency Risk
- What Changed Today
- Why Health Changed
- ranked PM Decision Brief
- Project Truth Decision Trace
- P2 signals and Decision Trace added to the PM dashboard

## Verification additions

- P2 Domain tests
- Evidence Freshness/Conflict tests
- Adaptive QA Planner test
- P2 Decision Intelligence live E2E scenario
- Python P2 experiment fixtures
- P2 architecture/testing/status documentation
- fail-closed `scripts/Verify-P2.ps1`
## Delta versus previous P1 overlay

- New files: **37**
- Changed files: **26**
- Deleted files: **0**
- Cumulative overlay files before `P2_FILE_MANIFEST.txt`: **196**

See `P2_FILE_MANIFEST.txt` for exact paths and SHA-256 hashes.
