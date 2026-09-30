# 最新RevisionのEvidence状態

## 目的

このfileでは**Implemented**、**Tested**、**Runtime Verified**、**Infrastructure Blocked**、**NOT_EVALUATED**を分離する。変化するlatest SHAをhard-codeせず、generated artifactが`git rev-parse HEAD` / `GITHUB_SHA`を記録する。

## Native-first Evidence

Current runtime verificationはhost-nativeで行う。Dockerとexternal queue-daemon executionはrequired Evidenceではない。`GUI E2E`はruntime artifactを出力し、`Native Package Build`はexact-SHA release archive/hashを生成する。Benchmark / DR / migration / QA Bug Zooもrevision-bound Evidenceを個別に書き出す。

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
10. CSharp / Cpp / Pythonを必須にしたQA Bug Zoo
11. New SHAについてrequired 11 workflowsを実行し、`Unreal Collector Contract CI`を含め11/11 Greenを確認
12. same-SHA benchmark / soak
13. QA -> Project Truth expected-failure scenario
14. `tools/finalize_portfolio_evidence.py`でCI / benchmark / QA Bug Zoo / Project Truthをsame-SHA binding

Historical Evidenceはregression baselineとして利用できるが、verification stateを後続commitへ継承しない。Zero-step workflow failureをsource failureまたはPASSへ再分類しない。

## Engine integration

実target projectを供給していないrevisionではengine-specific runtimeは`NOT_EVALUATED`とする。Hosted contract/unit checksだけでactual Editor runtimeをVerifiedと表現しない。Optional runtime Evidenceを追加する場合も、DIP SHAとtarget project SHAを別々に記録する。
