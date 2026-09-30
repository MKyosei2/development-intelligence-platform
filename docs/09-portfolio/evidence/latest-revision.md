# 最新RevisionのEvidence状態

## 目的

このfileでは**Implemented**、**Tested**、**Runtime Verified**、**Infrastructure Blocked**、**NOT_EVALUATED**を分離する。変化するlatest SHAをhard-codeせず、generated artifactが`git rev-parse HEAD` / `GITHUB_SHA`を記録する。

## Native-first Evidence

Current runtime verificationはhost-nativeで行う。Dockerとexternal queue-daemon executionはrequired Evidenceではない。`GUI E2E`はruntime artifactを出力し、`Native Package Build`はexact-SHA release archive/hashを生成する。Benchmark / DR / migration / QA Bug Zooもrevision-bound Evidenceを個別に書き出す。

## C++ QA Evidence boundary

CTestはtest orchestration Evidenceとして扱い、sanitizer-instrumented direct runtimeをC++ memory-safety findingの固定判定経路として扱う。CTest infrastructure timeoutは製品の`Critical/hang`へ読み替えず、PASSにも読み替えない。該当runでは`tools/ctest/execution-diagnostics.json`へstdout/stderr path、exit/timeout、CPU、peak working set、sandbox、sanitizer environment、pre-kill process treeを保存し、停止位置を追跡可能にする。

Current revisionのC++ QA determinismは、同一SHAを10回実行した`artifacts/qa-bug-zoo-determinism/qa-bug-zoo-determinism-summary.json`がPASSになるまでVerifiedとは扱わない。特に`cpp/clean`は全runでfinding 0、bug fixtureはfinding category/rule/tool pathが安定していることを要求する。

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
11. `scripts/DIP_QA_BUG_ZOO_DETERMINISM.ps1 -ExpectedSha <SHA> -Runs 10`でsame-SHA C++ determinismを確認
12. New SHAについてrequired 11 workflowsを実行し、`Unreal Collector Contract CI`を含め11/11 Greenを確認
13. same-SHA benchmark / soak
14. QA -> Project Truth expected-failure scenario
15. `tools/finalize_portfolio_evidence.py`でCI / benchmark / QA Bug Zoo / Project Truthをsame-SHA binding
16. `tools/validate_portfolio_readiness.py --require-current-final-evidence`でcurrent revision readinessを確認
17. `HEAD == origin/main == evidence SHA`かつtracked tree cleanを最終確認
18. `scripts/Package-PortfolioEvidence.ps1 -ExpectedSha <SHA>`でfinal / Docker Primary / determinism Evidenceを1つのhash付きZIPへ束ねる

Historical Evidenceはregression baselineとして利用できるが、verification stateを後続commitへ継承しない。Zero-step workflow failure、CTest infrastructure timeout、未実行stageをsource failureまたはPASSへ再分類しない。

## Engine integration

実target projectを供給していないrevisionではengine-specific runtimeは`NOT_EVALUATED`とする。Hosted contract/unit checksだけでactual Editor runtimeをVerifiedと表現しない。Optional runtime Evidenceを追加する場合も、DIP SHAとtarget project SHAを別々に記録する。
