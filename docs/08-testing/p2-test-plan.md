# P2 test plan

P2 is complete only when source implementation and execution evidence both exist.

## Automated .NET tests

- Domain validation for Community Hardware consent/privacy and P2 records
- Evidence Freshness decay
- Evidence Conflict authority rules
- Adaptive QA planning
- P0/P1 regression tests remain green
- E2E: Collector Evidence -> Project Truth -> Daily Change -> Decision Brief -> Health Change

## Local functional tests

### Reliability

Run a deterministic pass/fail target repeatedly and confirm pass/fail counts, rate and confidence.

### Soak / drift

Use `samples/p2-bug-zoo/python/soak_memory_growth/app.py`. The sample prints `DIP_METRIC` telemetry while retaining memory. Verify memory growth and performance drift are visible in the resulting P2 record.

### Fuzz

Use `samples/p2-bug-zoo/python/fuzz_target/app.py` through a safe repository wrapper/executable appropriate to the host. Confirm the magic crashing input is retained as an artifact and fingerprinted.

### Regression bisect

Use a disposable test repository or dedicated fixture branch with one known good and one known bad revision. Verify DIP performs evaluation in detached worktrees and reports the first bad commit without changing the active checkout.

### Mutation

Use a mutation fixture where one mutation is killed and one deliberately survives. Confirm mutation score and per-case detail.

### Media regression

Generate known baseline/current image, video and audio pairs. Verify:

- identical files -> Passed
- small difference -> Warning according to configured threshold
- large difference -> Failed
- missing ffmpeg/ffprobe -> InfrastructureFailure, never product Failed

## Hardware intelligence

- Community submission without consent must fail
- raw hardware key must not be persisted
- <3 community samples cannot create a community aggregate profile
- profile confidence must increase with representative sample count/diversity and remain bounded
- portability blockers must identify the limiting capability dimensions
- minimum hardware search must label its result simulated/non-real-device
- recommended minimum must include safety margin and remain separate from technical minimum

## AI grounding

External AI is optional and disabled by default. When enabled:

- send only the evidence bundle assembled for the requested subject
- drop AI candidates that contain no valid supplied Evidence ID
- never create a Failed Evidence solely from AI output
- never auto-modify source or open a PR

## PM semantics

Verify Dashboard/API distinguish:

- Claim vs Evidence
- measured vs simulated/predicted
- current vs stale evidence
- current authoritative evidence vs historical conflicts
- schedule risk estimate vs promised schedule
- completion range vs exact date
- evidence stagnation vs employee misconduct

## Required P2 completion evidence

On a full Windows development machine, `scripts/Verify-P2.ps1` must complete successfully. Live E2E additionally requires PostgreSQL, Redis, API and Worker. CI must remain green after the P2 changeset is committed.
