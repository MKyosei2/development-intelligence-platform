# Latest revision evidence status

## Purpose

This file keeps **Implemented**, **Tested**, **Runtime Verified**, and **Infrastructure Blocked** separate. It intentionally does not hard-code a mutable latest SHA; generated artifacts record `git rev-parse HEAD` / `GITHUB_SHA`.

## Native-first evidence

Current runtime verification is host-native. Docker and external queue-daemon execution are not required evidence. `GUI E2E` writes `artifacts/gui-runtime-verification/<timestamp>/...`; `Native Package Build` creates an exact-SHA release archive/hash; benchmark/DR/migration scripts write their own revision-bound evidence.

## Required verification after a source change

1. `dotnet restore DevelopmentIntelligencePlatform.sln`
2. `dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore`
3. Domain / Application / Infrastructure tests
4. Integration and E2E tests against native PostgreSQL
5. `scripts/Verify-GuiRuntime.ps1 -RunPlaywright`
6. `scripts/Verify-DatabaseMigrations.ps1`
7. `scripts/Verify-DisasterRecovery.ps1`
8. `python tools/verify_publication_safety.py --root .`
9. `python tools/validate_portfolio_readiness.py --root .`
10. Conventional Commit push
11. Confirm that the new SHA actually executed `.NET CI / Python CI / Native Collector CI / Integration and E2E / GUI E2E / Native Package Build / Security Scan / Universal QA Benchmark / Unreal Collector CI / Publication Readiness`.

Historical evidence can be a regression baseline, but verification state is never inherited to a later commit. A zero-step workflow failure is not reclassified as either a source failure or a PASS.
