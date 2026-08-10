# P2 — Predictive Development Intelligence

P2 extends Project Truth; it does **not** replace it. QA, hardware, regression, reliability, visual/media comparison, and root-cause analysis are Evidence Providers. Project Truth remains the final status/risk/progress authority.

## P2 capabilities

- Opt-in, anonymized Community Hardware calibration samples and confidence aggregates.
- Estimated Minimum Hardware boundary search with explicit safety margin. Simulation is never labeled real-device evidence.
- Adaptive QA planning from changed paths, historical failure evidence, and a time budget.
- Automated regression origin search over a Git commit range while restoring the original checkout.
- Flaky/reliability/soak execution with failure-rate, timeout, memory-growth, and performance-drift evidence.
- Visual/video/audio regression artifact comparison and evidence storage.
- Evidence-grounded root-cause candidates. If the supplied evidence cannot support a conclusion, the result is `Insufficient Evidence`.
- Release-risk/completion-window forecasts with confidence and rationale, never certainty.
- PM Decision Brief: Situation → Why → Next Action, plus daily changes and Project Truth change drivers.

## Safety / governance

P2 never computes a developer or artist trust score. It evaluates claims and evidence. Community hardware submission requires explicit opt-in. Root-cause suggestions do not automatically modify source code. Predictions are ranges/risks with confidence and supporting evidence.
