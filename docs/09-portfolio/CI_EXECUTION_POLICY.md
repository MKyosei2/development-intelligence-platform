# GitHub Actions Execution Policy

DIP uses a three-stage, quota-aware verification model. The policy separates normal development feedback from explicit hosted CI consumption and final exact-revision publication evidence.

## Stage 1 — work/* and normal development branches

Normal `work/*`, `feat/*`, `fix/*`, and `chore/*` pushes do not automatically start GitHub-hosted Actions. Pull requests also do not automatically start hosted workflows.

The default feedback loop is local verification. This prevents routine iteration from consuming the hosted-runner quota.

## Stage 2 — explicit manual CI

Every required workflow keeps `workflow_dispatch`. Use `scripts/Invoke-ManualCI.ps1` and choose one of exactly three presets.

```powershell
.\scripts\Invoke-ManualCI.ps1 -Preset Fast -Ref <branch>
.\scripts\Invoke-ManualCI.ps1 -Preset Targeted -Target DotNet,Python,Native -Ref <branch>
.\scripts\Invoke-ManualCI.ps1 -Preset Full -Ref <branch>
```

`Fast` runs Commit Message, .NET CI, and Portfolio Publication Readiness. `Targeted` requires explicit target names. `Full` dispatches all 11 required publication workflows.

## Stage 3 — final main exact-revision verification

Automatic hosted verification is reserved for the publication/release revision. It occurs only when both conditions are true:

- branch is `main`
- `.portfolio-ci-trigger` changed

Required final workflows:

1. Commit Message
2. .NET CI
3. Python CI
4. Native Collector CI
5. Integration and E2E Tests
6. GUI E2E
7. Native Package Build
8. Security Scan
9. Universal QA Benchmark
10. Unreal Collector Contract CI
11. Portfolio Publication Readiness

A missing, cancelled, skipped, runner-blocked, or failed execution is not reclassified as PASS. Historical green runs do not satisfy a new final SHA.

## Release sequence

1. Finish and review the material source change.
2. Freeze source on `main`.
3. Resolve the exact publication SHA.
4. Run local build/test, strict QA Bug Zoo, runtime E2E, migration and DR verification.
5. Dispatch or trigger all 11 required workflows for that same SHA.
6. Require 11/11 Green.
7. Run same-SHA benchmark / soak.
8. Run the deterministic QA -> Project Truth scenario.
9. Bind CI + benchmark + QA Bug Zoo + Project Truth with the finalizer.
10. Publish a release only after all mandatory Evidence agrees on the same SHA.

Engine-specific runtime verification is optional and separate unless a real target project is explicitly supplied. Absence of that optional evidence must remain visible as `NOT_EVALUATED`.

`Live Native Deployment` and `Release` are explicit manual workflows. A successful deployment-policy check is not evidence that production was mutated.
