# Development Intelligence Platform (DIP)

DIPは、ゲーム・メディア・ソフトウェア開発チーム向けのDevelopment Intelligence基盤です。Git / CI / QA / Runtime / Performance / Project Dataを共通のProject Contextへ集約し、出所とrevisionを追跡できるEvidenceとして扱います。

**Portfolio focus:** C# / .NET 8、PostgreSQL、Python、C++ / native collector、Unity / Unreal、GitHub Actionsを、別々のデモではなく**Evidenceとexact-revision verificationを中心に接続した開発基盤**として示します。

![DIP portfolio demo](docs/09-portfolio/demo/dip-portfolio-demo.gif)

> このGIFはsample-data previewです。Runtime Verifiedの根拠には使いません。実Projectの検証結果はcommit-bound Evidenceとして別に扱います。

## 60秒レビュー

1. 「まず見てほしい3領域」で主軸を確認する。
2. 「実Project Case Study」で実Unity ProjectからDIPへEvidenceが流れる経路を見る。
3. 「Verification status」と「Final exact-revision gate」でImplemented / Tested / Runtime Verifiedの分離を確認する。
4. 設計判断の変化は [`ENGINEERING_JOURNEY.md`](docs/09-portfolio/ENGINEERING_JOURNEY.md) を参照する。

## まず見てほしい3領域

### 1. Project Truth / Evidence

ClaimとEvidenceを分離し、source、revision、freshness、contradictionを追跡します。Sourceが存在するだけではVerifiedにせず、現在revisionを裏付けるEvidenceがある状態だけをConfirmedとして扱います。

### 2. Game Development QA

Unity / Unreal / C++ / toolchain integrationから得たEvidenceを、QA Finding、再現性、regression、performance、quality gateへ接続します。Engine固有payloadを保持しつつ、意思決定に必要な情報をprovider-neutral Evidenceへ昇格します。

### 3. Verification / Release Operations

Unit / Integration / E2E、GUI、security、benchmark、migration、backup/restore、release readinessをexact revisionへ結び付けます。古いPASSや実行できなかった状態を、現在revisionの成功として扱いません。

その他の機能は、この3領域を支える周辺機能です。

## Verification status

READMEの説明そのものを検証結果とは扱いません。**最終提出revisionは、同一SHAに紐付いたCI / benchmark / engine runtime / Project Truth / Release Evidenceが揃うまで `Current Revision PASS` を主張しません。**

| Claim | Verifiedと扱う条件 |
|---|---|
| Implemented | 対応sourceが存在する |
| Tested | 対象revisionのtest / workflowが成功 |
| Runtime Verified | 対象revisionを実runtimeで検証したEvidenceがある |
| Performance Verified | clean tree + exact SHAのbenchmark artifactがある |
| Final Portfolio Revision | required CI、runtime Evidence、final binding、Releaseが同一SHA |

最終Source of TruthはGitHub Actions / Release artifact / `artifacts/final-evidence/` です。

## 実Project Case Study

デフォルトの実Project Case Studyには、別repositoryのUnity Project **MAYAtoUnity** を使用します。

```text
MAYAtoUnity
    ↓ actual Unity Editor batchmode
DIP Unity exact-revision collector
    ↓
Collector Evidence
    ↓
DIP API ingestion
    ↓
Project Truth + Quality Gate
    ↓
Release decision / PM・Lead review
```

Case Studyでは次を分離します。

- `dipSourceCommit`: 検証しているDIP自身のSHA
- `targetCommit`: Collectorが解析したMAYAtoUnity側のSHA

これは**実Projectとのend-to-end integration**を示すもので、第三者チームへのproduction導入実績を意味しません。

- 実行方法: [`REAL_PROJECT_CASE_STUDY.md`](docs/09-portfolio/REAL_PROJECT_CASE_STUDY.md)
- 2〜3分デモ構成: [`REAL_PROJECT_DEMO.md`](docs/09-portfolio/REAL_PROJECT_DEMO.md)

## エンジニアリング原則

- **Implemented ≠ Tested ≠ Runtime Verified**
- **ClaimとEvidenceを分離**
- **AIを真実の根拠にしない** — Autonomous Debuggingは `Hypothesis → Candidate → Isolated Verification → Human Review`
- **Historical PASS ≠ Current Revision PASS**
- **Production Operationsはfail-closed**
- Flexible Spreadsheet IntelligenceもEvidence Modelへ参加するが、主軸はProject Truth / Game Development QA / Verification

開発方針の変化は [`ENGINEERING_JOURNEY.md`](docs/09-portfolio/ENGINEERING_JOURNEY.md) に残しています。

## CI実行方針

Hosted runner quotaを通常pushで消費しないため、3段階で運用します。

```text
1. work/* / feat/* / fix/* / chore/*
   -> Hosted Actionsを自動起動しない

2. workflow_dispatch
   -> Fast / Targeted / Fullを明示実行

3. main + .portfolio-ci-trigger
   -> final提出 / Release時だけrequired workflowを起動
```

手動CI:

```powershell
.\scripts\Invoke-ManualCI.ps1 -Preset Fast -Ref work/example
.\scripts\Invoke-ManualCI.ps1 -Preset Targeted -Target DotNet,Python,Native -Ref work/example
.\scripts\Invoke-ManualCI.ps1 -Preset Full -Ref work/example
```

詳細: [`CI_EXECUTION_POLICY.md`](docs/09-portfolio/CI_EXECUTION_POLICY.md)

## Final exact-revision gate

Final source freezeは`.portfolio-ci-trigger`だけを変更するPRとして行います。merge後のmain SHAについて、次のrequired workflowをすべてGreenにします。

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

`missing / cancelled / skipped / infrastructure-blocked`をPASSへ読み替えません。

```powershell
.\scripts\Request-FinalVerification.ps1 -Commit -CreatePullRequest
```

最終SHAでは次のhelperが、CI、performance、Unity、Unreal、Project Truth、Evidence bundle、Releaseを同一SHAへ束ねます。

```powershell
.\scripts\Complete-PortfolioRelease.ps1 -Version <x.y.z> -PublishRelease
```

## Benchmark

Current revisionのperformance値として使用できるのは、clean working treeかつexact SHAで生成されたartifactだけです。

```powershell
$sha = (git rev-parse HEAD).Trim()
.\scripts\Run-PortfolioBenchmarks.ps1 -ExpectedSha $sha
```

Canonical artifact:

```text
artifacts/portfolio-benchmark/portfolio-benchmark-results.json
artifacts/portfolio-benchmark/portfolio-benchmark-results.sha256
```

未測定SHAは`NOT YET MEASURED`として扱います。

## Unity / Unreal Runtime Evidence

UnityはMAYAtoUnityを実Projectとして検証し、実Unity Editor process、DIP SHA、target SHA、Collector reportをEvidenceへ保存します。

```powershell
python tools/verify_unity_real_project.py --expected-dip-sha <DIP_SHA>
```

Unrealは実`UnrealEditor-Cmd`とGit-backed `.uproject`を要求します。

```powershell
python tools/verify_unreal_real_project.py --expected-dip-sha <DIP_SHA> --project <UnrealProject>
```

`tools/finalize_portfolio_evidence.py` が最終Evidenceを同一DIP SHAへfail-closedで束ねます。

## Native-first architecture

```text
Browser
  └─ Dip.Web (.NET / Blazor) :5081
       └─ Dip.Api (.NET)     :5080
            ├─ PostgreSQL    durable truth + durable queue
            ├─ Python Analysis :8100
            └─ filesystem    artifacts / workspaces

Dip.Worker (.NET Worker)     :9091 metrics
  ├─ PostgreSQL-backed jobs / queue
  ├─ native QA processes
  └─ Unity / Unreal / C++ / toolchain integrations
```

PostgreSQLをdurable Source of Truthとqueue / coordination boundaryとして使い、.NET / Python serviceはhost上で実行します。Dockerは正式なNative-first runtimeの必須依存ではありません。

詳細: [`ARCHITECTURE_OVERVIEW.md`](docs/09-portfolio/ARCHITECTURE_OVERVIEW.md)

## Evidenceと検証境界

| 状態 | 意味 |
|---|---|
| Implemented | Sourceが存在する |
| Tested | 対象testが成功 |
| Runtime Verified | 対象revisionを実runtimeで検証 |
| Infrastructure Blocked | runner / tool / environment都合で未完了 |

- Historical baseline: [`docs/09-portfolio/evidence/verified-baseline/`](docs/09-portfolio/evidence/verified-baseline/)
- Exact-revision policy: [`docs/09-portfolio/evidence/latest-revision.md`](docs/09-portfolio/evidence/latest-revision.md)
- Benchmark rule: [`docs/09-portfolio/benchmark-results.md`](docs/09-portfolio/benchmark-results.md)
- Publication checklist: [`docs/09-portfolio/PUBLICATION_CHECKLIST.md`](docs/09-portfolio/PUBLICATION_CHECKLIST.md)

## 主な技術領域

- C# / .NET 8: Domain / Application / API / Worker / Blazor
- PostgreSQL: durable truth / queue / migration / DR
- Python 3.12+: analysis / QA / Evidence tooling
- C++ / native collector
- Unity / Unreal / DCC / toolchain integrations
- GitHub Actions / Playwright

## ローカルRuntime

通常は.NET 8、PostgreSQL 16-compatible server + client tools、Python 3.12+、Gitを使用します。Browser E2EではNode.js 22+、Unity / Unreal / CMakeは対応integrationのruntime検証時だけ必要です。

`Start-DIP.ps1` はPostgreSQLを確認し、Python Analysis / API / Worker / Webを起動します。`Stop-DIP.ps1` はDIP processを停止します。

## Reviewer向け読み順

1. このREADME
2. [`ENGINEERING_JOURNEY.md`](docs/09-portfolio/ENGINEERING_JOURNEY.md)
3. [`GAME_DEVELOPMENT_SCENARIO.md`](docs/09-portfolio/GAME_DEVELOPMENT_SCENARIO.md)
4. [`REAL_PROJECT_DEMO.md`](docs/09-portfolio/REAL_PROJECT_DEMO.md)
5. [`reviewer-brief.md`](docs/09-portfolio/reviewer-brief.md)
6. Architecture / Evidence / CI artifacts

DIPはgameplayやrendering単体のshowcaseではなく、**ゲーム制作を支えるDevelopment Infrastructure / QA / Developer Productivity / Production Engineering**を示すProjectです。
