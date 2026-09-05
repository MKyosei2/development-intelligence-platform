# Commercial QA / PM Quality Operations

DIP Quality Operations converts automated QA observations into evidence-backed operational decisions without treating AI or heuristic output as the source of truth.

## Operator entry points

- `/dashboard/{projectId}/quality-operations` — commercial Quality Operations Command Center.
- `/dashboard/{projectId}/qa/operations` — QA-oriented alias of the same command center.
- `/dashboard/{projectId}/qa` — run/detail/debug view.
- `/dashboard/{projectId}/qa-governance` — lifecycle, waiver, baseline and governance controls.
- `/dashboard/{projectId}/management` — PM decisions, actions, approvals and source-of-truth operations.

## Release gate semantics

`Blocked / DO NOT PROMOTE`
: One or more P0 release blockers remain open. A mechanically reproduced P0 increases confidence in the block but does not change the underlying evidence source.

`Hold / HOLD FOR REVIEW`
: No P0 blocker is open, but verification is stale/non-successful, an SLA is breached, or accumulated P1 risk requires explicit review.

`Ready / READY FOR HUMAN APPROVAL`
: No P0 blocker is open and the latest QA evidence is current and successful. This is not an automatic release approval; the normal human approval process still applies.

`Unknown / NO CURRENT VERIFICATION`
: DIP has no current QA evidence. Unknown is never upgraded to Ready.

## Default operational response windows

- P0: 4 hours
- P1: 24 hours
- P2: 72 hours
- Fresh release verification: 24 hours

The defaults are centralized in `CommercialQaServiceLevelPolicy` and can be replaced by host-specific policy registration. They are operational targets, not universal industry standards.

## Commercial QA evidence chain

`Repository / Game / Plugin / Media / DCC`
→ technology + production capability detection
→ adapters + cross-cutting production QA
→ finding classification
→ optional mechanical reproduction
→ evidence + durable quality issue
→ PM decision translation
→ Project Truth + Quality Operations release gate

A skipped/unavailable tool, absent runtime executable, missing media baseline, stale verification or missing QA run is never represented as PASS.

## Production integrity coverage

The cross-cutting Production QA Pack includes bounded deterministic checks for:

- Unity asset `.meta` presence and duplicate GUID collisions.
- Unreal `.uplugin` descriptor parsing/integrity.
- Maya missing and machine-dependent absolute references.
- zero-byte production assets.
- image/frame-sequence gaps.
- configured visual/video/audio regression through the existing media-regression runner.

`.dip/production.json` extends the production domain, genre/capability hints and media baseline/current comparisons. Runtime/reference checks that are not configured remain explicitly unverified.

## PM workflow

1. Open Quality Operations Command Center.
2. Read the Release Gate and Decision Brief first.
3. Resolve P0 and SLA-breached lanes before lower-priority triage.
4. Use owner role, reproduction status, occurrence count and evidence age when assigning work.
5. For a blocked release, fix and rerun the same evidence-producing scenario. If scope is removed instead, record explicit approval, rollback and residual risk.
6. Use QA Governance for waivers/baselines and Management for actions/approvals. The command center does not silently waive or approve risk.

## QA workflow

1. Run the appropriate QA path from the QA page.
2. Inspect mechanically reproduced failures before one-off warnings.
3. Use production-capability findings for game/plugin/media/DCC integrity, not only code diagnostics.
4. Keep artifacts and Evidence IDs attached to the affected revision.
5. Rerun after fixes. Older successful evidence does not prove the current revision.

## Quality score

The `0–100` number on the command center is an operational prioritization score derived from open P0/P1, SLA breaches and recent failed QA runs. It is deliberately subordinate to the categorical Release Gate and must not be presented as a universal measure of product quality.

## Validation

Run locally before publishing a revision:

```powershell
python tools/validate_commercial_quality_operations.py
python tools/universal_qa_benchmark.py --output artifacts/universal-qa-benchmark

dotnet build
dotnet test
```

Then execute GUI E2E/Playwright in the normal DIP environment. Toolchains unavailable on the verification host remain skipped and must not be described as passing.
