# Completion / Hardening overlay

This archive is a **path-preserving full-file overlay** for
`MKyosei2/development-intelligence-platform` at base commit
`f335606cbd4fbd489efeaf801667113e5cd01e03` (`main`, `Upgrade3`).

Each modified source file in this archive is the complete post-change file,
not a patch fragment. Extract the `development-intelligence-platform` folder
over an existing checkout of the base repository and allow files to be
replaced. Unchanged repository files are intentionally not duplicated in this
archive because the private GitHub archive cannot be materialized into the
artifact runtime.

## What this overlay fixes

1. Current `.NET CI` compile blockers:
   - `Home.razor` imports the dashboard DTO namespace.
   - `PerformanceSimulationService` explicitly uses nullable decimals for
     optional FPS/P95 values.
2. Minimum Hardware no longer treats target-defined environment variables as
   real constraints. It reuses the P1 constrained performance runner and
   fails closed when a requested constraint was not actually applied.
3. Soak/reliability samples the **target process** and treats reaching the
   soak observation window as success instead of timeout.
4. Regression search and mutation testing run in disposable detached Git
   worktrees instead of changing the user's active checkout.
5. Verification commands are restricted to workspace-local paths or an
   allowlist of development tools.
6. Low-precision media fallback is no longer stored as High-confidence
   evidence.
7. Forecast completion windows use Project Truth verified-progress history;
   missing history returns no completion dates rather than inferring velocity
   from raw evidence count.
8. Community Hardware retries are pseudonymously deduplicated for 24 hours and
   aggregate confidence includes sample count, freshness, diversity, and
   dispersion.
9. Production/default API configuration requires authentication. Development
   and Testing explicitly retain unauthenticated local/E2E behavior.
10. P2 live E2E and execution-safety tests are added, and `Verify-P2.ps1`
    distinguishes PASS from PARTIAL verification.

## Apply

From the parent folder of your existing repository, extract this archive with
path preservation. If extracting manually, copy the contents of the included
`development-intelligence-platform` directory into the repository root and
replace matching files.

## Verify

On the target Windows development machine:

```powershell
Set-Location <repository-root>
.\scripts\Verify-P2.ps1
```

A full PASS requires no skip switches. The script also invokes the full live
solution verification. Simulation evidence remains distinct from real-device
evidence even after the script passes.

## Deliberately not claimed as fixed by this overlay

These require a larger deployment/product phase rather than a safe local bug
patch: a formal EF migration baseline, durable asynchronous P2 jobs, remote
artifact retention, external Claim/work-item connectors and canonical work-item
resolution, and production-scale real-device/forecast calibration.
