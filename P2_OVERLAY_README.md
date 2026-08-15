# P0 + P1 + P2 cumulative full-file overlay

Repository: `MKyosei2/development-intelligence-platform`

P2 base revision inspected immediately before packaging:
`29d9b15dd02ad65f173f40230f995c9a7fcf3185` (`Upgrade2`).

This archive is a **cumulative full-file overlay**. It contains the complete
post-edit contents of every file previously included in the P0/P1 overlay plus
every file added or changed for P2. It is not a patch-hunk archive.

Unchanged private-repository files are not duplicated. Extract the top-level
`development-intelligence-platform` directory over an existing checkout and
allow same-path files to be replaced.

## P2 purpose

P2 keeps Project Truth / Evidence First as the product core and adds decision
intelligence around it. QA, simulations, forecasts, Community Hardware and AI
remain Evidence Providers or decision-support context; they do not replace
Project Truth.

## Apply

1. Start from commit `29d9b15dd02ad65f173f40230f995c9a7fcf3185`
   or a compatible checkout that already contains P0/P1.
2. Commit or back up local changes.
3. Extract this archive over the repository root.
4. Review `P2_CHANGESET.md` and `docs/P2_IMPLEMENTATION_STATUS.md`.
5. On Windows run:

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-P2.ps1"
```

6. To include live E2E, start PostgreSQL, Redis, API and Worker, set
   `DIP_RUN_LIVE_E2E=1`, then run `Verify-P2.ps1` again.

## Critical semantics

- Claims are evaluated; people are never assigned a truthfulness/trust score.
- Community Hardware submission is explicit opt-in and raw HardwareKey is only
  hashed for pseudonymous deduplication; it is not persisted.
- Simulation/community aggregate/forecast results are not real-device proof.
- External AI is optional, disabled by default, evidence-grounded, and cannot
  create a Project Truth failure by itself.
- Suggested fixes are human-review advice only. No automatic source change or
  PR is created.
- P2 execution paths are restricted to the configured QA workspace. Shared
  deployments must enable authentication and restrict ProjectWrite access.
- Source presence is not completion evidence. CI, .NET tests, live E2E and
  representative Windows/experiment evidence must still be green.
