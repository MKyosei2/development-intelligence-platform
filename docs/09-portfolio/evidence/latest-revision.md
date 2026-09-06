# 最新RevisionのEvidence状態

## 目的

このfileでは**Implemented**、**Tested**、**Runtime Verified**、**Infrastructure Blocked**を分離する。変化するlatest SHAをhard-codeせず、generated artifactが`git rev-parse HEAD` / `GITHUB_SHA`を記録する。

## Native-first Evidence（Native-first証拠）

Current runtime verificationはhost-nativeで行う。Dockerとexternal queue-daemon executionはrequired Evidenceではない。`GUI E2E`は`artifacts/gui-runtime-verification/<timestamp>/...`へ出力し、`Native Package Build`はexact-SHA release archive/hashを生成する。Benchmark/DR/migration scriptもrevision-bound Evidenceを個別に書き出す。

## Source Change後に必要なVerification

1. `dotnet restore DevelopmentIntelligencePlatform.sln`
2. `dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore`
3. Domain / Application / Infrastructure tests
4. Native PostgreSQLを使ったIntegration / E2E tests
5. `scripts/Verify-GuiRuntime.ps1 -RunPlaywright`
6. `scripts/Verify-DatabaseMigrations.ps1`
7. `scripts/Verify-DisasterRecovery.ps1`
8. `python tools/verify_publication_safety.py --root .`
9. `python tools/validate_portfolio_readiness.py --root .`
10. Conventional Commit push
11. New SHAが`.NET CI / Python CI / Native Collector CI / Integration and E2E / GUI E2E / Native Package Build / Security Scan / Universal QA Benchmark / Unreal Collector CI / Publication Readiness`を実際に実行したことを確認。

Historical Evidenceはregression baselineとして利用できるが、verification stateを後続commitへ継承しない。Zero-step workflow failureをsource failureまたはPASSへ再分類しない。
