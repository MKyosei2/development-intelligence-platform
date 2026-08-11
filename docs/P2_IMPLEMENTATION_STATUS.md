# P2 implementation status

This file distinguishes code state from verified completion.

| P2 area | Code state | Required completion evidence |
|---|---|---|
| Community Hardware DB | Implemented, opt-in + pseudonymous key | persistence/integration test |
| Hardware profile confidence | Implemented | multi-sample test |
| Community aggregate profile | Implemented | profile creation test |
| Portability Analyzer | Implemented | application/API test |
| Minimum Hardware Finder | Implemented, 5-dimension boundary search | repeated target simulation |
| Safe hardware recommendation | Implemented separately from technical minimum | boundary test |
| Adaptive QA Planner | Implemented | unit + representative change-set tests |
| QA cost estimator | Implemented in planner | planner tests |
| Historical risk prioritisation | Implemented | historical Evidence test |
| Flaky test detection | Implemented | repeated-run fixture |
| Runtime reliability | Implemented | repeated-run fixture |
| Soak test | Implemented | long-running fixture |
| Performance drift | Implemented | telemetry fixture |
| Git regression bisect | Implemented with isolated worktrees | disposable-repo fixture |
| Regression origin | Implemented | metric-history test |
| Fuzz testing | Implemented, deterministic seed | safe fixture |
| Mutation testing | Implemented in isolated workspace | mutation fixture |
| Visual regression | Implemented | ffmpeg media fixture |
| Video regression | Implemented | ffmpeg media fixture |
| Audio regression | Implemented | ffmpeg media fixture |
| Evidence-grounded root cause | Implemented, deterministic fallback | grounding tests |
| Optional external AI | Implemented, disabled by default | opt-in mock endpoint test |
| Suggested fix | Implemented as human-review advice | grounding test |
| Similar incident search | Implemented | historical record test |
| Evidence Freshness | Implemented | unit tests |
| Evidence Conflict Resolution | Implemented | unit tests |
| Schedule Risk | Implemented as estimate | decision-intelligence E2E |
| Completion Forecast | Implemented as range | history test |
| Work Stagnation | Implemented as evidence-staleness signal | claim/history test |
| Dependency Risk | Implemented | dependency graph test |
| What Changed Today | Implemented | E2E |
| Why Health Changed | Implemented | snapshot-history E2E |
| PM Decision Assistant | Implemented | E2E |
| Decision Trace | Implemented on Project Truth transitions | E2E/dashboard |
| PM Dashboard P2 signals | Implemented | dashboard query/live UI test |

P2 must not be described as fully verified because source files exist. Full verification requires .NET build/tests, live E2E, representative P2 experiment fixtures, media checks and the Windows/P1 native evidence path to remain green.
