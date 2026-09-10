# GitHub Actions Execution Policy

DIP uses a three-stage, quota-aware verification model. The policy separates normal development feedback from explicit hosted CI consumption and final exact-revision publication evidence.

## Stage 1 — work/* and normal development branches

Normal `work/*`, `feat/*`, `fix/*`, and `chore/*` pushes do not automatically start GitHub-hosted Actions. Pull requests also do not automatically start hosted workflows.

The default feedback loop is local verification. This prevents routine iteration from consuming the hosted-runner quota.

## Stage 2 — explicit manual CI

Every workflow keeps `workflow_dispatch`. Use `scripts/Invoke-ManualCI.ps1` and choose one of exactly three presets.

```powershell
# Minimal confidence loop
.\scripts\Invoke-ManualCI.ps1 -Preset Fast -Ref <branch>

# Run only named required workflows
.\scripts\Invoke-ManualCI.ps1 -Preset Targeted -Target DotNet,Python,Native -Ref <branch>

# Run the complete required matrix manually
.\scripts\Invoke-ManualCI.ps1 -Preset Full -Ref <branch>
```

`Fast` runs Commit Message, .NET CI, and Portfolio Publication Readiness. `Targeted` requires one or more explicit target names. `Full` dispatches all 11 required publication workflows.

## Stage 3 — final main exact-revision verification

Automatic hosted verification is reserved for the publication/release revision. It occurs only when both conditions are true:

- branch is `main`
- `.portfolio-ci-trigger` changed

The required final workflows are:

1. Commit Message
2. .NET CI
3. Python CI
4. Native Collector CI
5. Integration and E2E Tests
6. GUI E2E
7. Native Package Build
8. Security Scan
9. Universal QA Benchmark
10. Unreal Collector CI
11. Portfolio Publication Readiness

A missing, cancelled, skipped, runner-blocked, or failed execution is not reclassified as PASS. Historical green runs do not satisfy a new final SHA.

## Release sequence

1. Finish work on a development branch.
2. Merge the material hardening change through a pull request.
3. Freeze source on `main`.
4. Create a small final-verification branch that changes only `.portfolio-ci-trigger`.
5. Merge that branch through a pull request.
6. Wait for all 11 workflows on the resulting exact `main` SHA.
7. Run same-SHA performance and real-engine runtime verification.
8. Publish the release only after all evidence agrees on the same SHA.

`Live Native Deployment` and `Release` are explicit manual workflows. A successful deployment-policy check is not evidence that production was mutated.
