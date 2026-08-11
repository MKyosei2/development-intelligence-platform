# P2 Decision Intelligence architecture

## Purpose

P2 evolves DIP from current-state visibility into evidence-backed decision support. It does not replace Project Truth and it does not turn QA into the product core.

```text
Community hardware / experiments / regression / reliability / media checks
                              |
                              v
                    P2 Intelligence Records
                              |
             +----------------+----------------+
             |                                 |
      Objective Evidence                Prediction / Context
             |                                 |
             v                                 v
        Project Truth                  PM Decision Intelligence
             |                                 |
             +----------------+----------------+
                              v
                         PM Dashboard
```

Measured failures can become Project Truth evidence. Forecasts, AI candidates, community aggregates and simulations are contextual evidence and are never promoted to measured real-device facts.

## Implemented P2 capability groups

### Hardware intelligence

- explicit opt-in Community Hardware samples
- SHA-256 pseudonymous hardware identity; raw key is not persisted
- sample count, OS/driver diversity, real-device ratio, P10/P50/P90 based confidence
- community aggregate Hardware Profiles
- portability analysis across CPU/GPU/memory/storage/thermal dimensions
- independent CPU/GPU/memory-bandwidth/storage/memory-budget minimum boundary search
- safety-margin recommended profile distinct from estimated technical minimum

### Adaptive verification planning

- changed-file/tag classification
- historical failure risk weighting
- fixed expected QA costs
- budget-aware plan selection
- docs-only short path
- rendering/mobile/audio/network specialisation

This planner chooses existing hardcoded evidence providers. It does not introduce a language-neutral QA adapter.

### Regression and reliability

- repeated flaky-test execution
- repeated runtime reliability execution
- soak tests with memory/CPU/FPS/frame-time telemetry
- performance drift detection
- isolated Git worktree regression bisect
- explicit regression-origin analysis over historical metric points
- deterministic fuzz input generation
- mutation testing in an isolated workspace

### Media regression

- visual image comparison and generated difference image
- sampled video comparison with first differing time
- audio RMS/silence/clipping/sample-difference analysis
- ffmpeg/ffprobe dependency failures are InfrastructureFailure, not product failure

### Evidence-grounded root cause

- deterministic evidence clustering is always available
- optional external OpenAI-compatible endpoint is disabled by default
- only the selected Evidence bundle is sent when explicitly enabled
- AI candidates referencing unknown Evidence IDs are discarded
- AI output cannot directly create a Project Truth blocker
- suggested actions are advice for human review; no auto-fix or auto-PR
- similar-incident lookup uses prior P2 evidence

### PM decision intelligence

- Evidence Freshness and confidence decay
- authority-weighted Evidence Conflict Resolution
- Historical Risk
- Schedule Risk estimate
- Completion Forecast range
- Work Stagnation detection
- Dependency Risk
- What Changed Today
- Why Health Changed
- ranked PM Decision Brief
- Decision Trace for Project Truth transitions

## Human-safety principle

P2 does not calculate employee trustworthiness, lying probability or personal performance scores. Stagnation means that objective Evidence is stale relative to a Claim. Contradiction means the Claim and current Evidence disagree. Neither is an accusation about a person.

## Prediction semantics

A forecast is not a measured fact. P2 stores prediction confidence separately and uses a date range instead of a promised completion date. Simulation and Community Hardware aggregation never receive real-device `Verified` status merely because they have many samples.

## Execution safety

Fuzzing, mutation, soak, repeated execution and bisect are restricted to the configured `Qa:WorkspaceRoot`. Executable and media paths must be relative and remain inside that root. Mutation and bisect use isolated Git worktrees where possible so the active developer checkout is not modified.

On a shared deployment, authentication must be enabled and ProjectWrite access restricted. P2 execution endpoints must not be exposed anonymously to untrusted networks.
