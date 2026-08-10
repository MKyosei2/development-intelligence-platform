# P2 — Predictive Development Intelligence

P2 extends Project Truth; it does **not** replace it. QA, hardware, regression, reliability, visual/media comparison, and root-cause analysis are Evidence Providers. Project Truth remains the final status/risk/progress authority.

## P2 capabilities

- Opt-in, pseudonymized Community Hardware calibration samples and confidence aggregates.
- Estimated Minimum Hardware boundary search with explicit safety margin. The P2 search reuses the P1 constrained performance-simulation runner; simulation is never labeled real-device evidence.
- Adaptive QA planning from changed paths, historical failure evidence, and a time budget.
- Automated regression origin search over a Git commit range in a disposable detached Git worktree, leaving the original checkout untouched.
- Flaky/reliability/soak execution with target-process failure-rate, timeout, memory-growth, and emitted FPS/frame-time drift evidence.
- Visual/video/audio regression artifact comparison and evidence storage. Low-precision binary fallbacks are not promoted to high-confidence evidence.
- Evidence-grounded root-cause candidates. If the supplied evidence cannot support a conclusion, the result is `Insufficient Evidence`.
- Release-risk/completion-window forecasts with confidence and rationale. Completion velocity is derived from Project Truth verified-progress history; insufficient history produces no completion date rather than an invented estimate.
- PM Decision Brief: Situation → Why → Next Action, plus daily changes and Project Truth change drivers.

## Execution safety

P2 regression and mutation runners use an isolated detached Git worktree. The original checkout is not checked out to historical commits and mutation candidates are not written into the user's active working tree.

Verification commands are constrained to either:

- a workspace-relative executable/script that resolves inside the isolated worktree, or
- an allowlisted development tool (`dotnet`, Python/pytest, CMake/CTest/Ninja/MSBuild, PowerShell, Node package tools).

Absolute arbitrary host commands and workspace path traversal are rejected.

## Hardware evidence boundary

Minimum-hardware estimates are produced from bounded synthetic constraints and observed application results. CPU/GPU/memory/storage boundary probes fail closed when the requested constraint is not actually applied or when the target does not report an explicit pass/fail product result.

The result remains `simulated=true`, `realDevice=false`; real-device evidence is required before publishing a certified minimum specification.

## Reliability / soak semantics

A soak run is an observation window, not a timeout. If the target remains alive through the requested soak duration, DIP records a successful observation and stops the process intentionally. Early exit is treated as a failure. Memory growth is sampled from the **target process**, not from DIP's own managed heap. Performance drift is calculated only when the target emits parseable FPS or frame-time metrics; missing metrics are not replaced with unrelated host measurements.

## Safety / governance

P2 never computes a developer or artist trust score. It evaluates claims and evidence. Community hardware submission requires explicit opt-in and stores a one-way pseudonymous source fingerprint rather than the raw consent token. Root-cause suggestions do not automatically modify source code. Predictions are ranges/risks with confidence and supporting evidence.
