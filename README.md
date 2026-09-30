# Development Intelligence Platform (DIP)

DIPは、ゲーム・メディア・ソフトウェア開発チーム向けのDevelopment Intelligence基盤です。Git / CI / QA / Runtime / Performance / Project Dataを共通のProject Contextへ集約し、出所・revision・freshnessを追跡できるEvidenceとして扱います。

**Portfolio focus:** C# / .NET 8、PostgreSQL、Python、C++ / native QA、Unity / Unreal integration、GitHub Actionsを、別々のデモではなく**Evidenceとexact-revision verificationを中心に接続した開発基盤**として示します。

![DIP portfolio demo](docs/09-portfolio/demo/dip-portfolio-demo.gif)

> このGIFはsample-data previewです。Runtime Verifiedの根拠には使いません。Current revisionの検証状態はGitHub Actions / benchmark / QA Bug Zoo / Project Truth / final Evidence artifactを優先します。

## 60秒レビュー

1. 「まず見てほしい3領域」でProjectの主軸を確認する。
2. [`QA_EVIDENCE_CASE_STUDY.md`](docs/09-portfolio/QA_EVIDENCE_CASE_STUDY.md) で、Critical QA EvidenceがProject Truthのunsupported Complete claimを否定する流れを見る。
3. 「Verification status」と「Final exact-revision gate」でImplemented / Tested / Runtime Verifiedを分離していることを確認する。
4. 開発方針の変化は [`ENGINEERING_JOURNEY.md`](docs/09-portfolio/ENGINEERING_JOURNEY.md) を参照する。

## まず見てほしい3領域

### 1. Project Truth / Evidence

ClaimとEvidenceを分離し、source、revision、freshness、contradictionを追跡します。Sourceが存在するだけではVerifiedにせず、現在revisionを裏付けるEvidenceがある状態だけをConfirmedとして扱います。

### 2. Game Development QA

**Evidence-Grounded Autonomous Debugging** は、Hypothesis → Candidate → Isolated Verification → Human Review の順に進み、AI出力そのものを検証結果として扱いません。

Universal QA / C++ / runtime / CI / engine integrationから得たEvidenceを、QA Finding、再現性、regression、performance、quality gateへ接続します。Engine固有payloadを保持しつつ、意思決定に必要な情報をprovider-neutral Evidenceへ昇格します。

### 3. Verification / Release Operations

Unit / Integration / E2E、GUI、security、benchmark、migration、backup/restore、release readinessをexact revisionへ結び付けます。古いPASS、未実行、environment-blockedを現在revisionの成功として扱いません。

その他の機能は、この3領域を支える周辺機能です。

## Verification status

READMEの説明そのものを検証結果とは扱いません。**最終提出revisionは、同一SHAに紐付いたCI / benchmark / QA Bug Zoo / Project Truth / final Evidenceが揃うまで `Current Revision PASS` を主張しません。**

| Claim | Verifiedと扱う条件 |
|---|---|
| Implemented | 対応sourceが存在する |
| Tested | 対象revisionのtest / workflowが成功 |
| Runtime Verified | 対象revisionを実runtimeで検証したEvidenceがある |
| Performance Verified | clean tree + exact SHAのbenchmark artifactがある |
| Final Portfolio Revision | required CI、benchmark、QA Bug Zoo、Project Truth、final bindingが同一SHA |

最終Source of TruthはGitHub Actions / Release artifact / `artifacts/final-evidence/` です。

## QA Evidence Case Study

Final portfolioの中心scenarioは、実在するproduction defectを装うものではありません。**意図的にCritical QA findingを投入するdeterministic verification fixture**です。

```text
Intentional Critical QA finding
        ↓
DIP Collector/API ingestion
        ↓
Quality Gate = Failed
        ↓
Claim: Complete / 100%
        ↓
Project Truth
        ↓
Claim = Contradicted
Verified progress < Reported progress
Contradiction recorded
```

VerifierがPASSする条件は、「製品品質GateがPASSすること」ではありません。**Critical Evidenceが存在する状態でDIPがunsupported completion claimを正しく拒否すること**です。

- 実行・設計: [`QA_EVIDENCE_CASE_STUDY.md`](docs/09-portfolio/QA_EVIDENCE_CASE_STUDY.md)
- 2〜3分デモ構成: [`QA_EVIDENCE_DEMO.md`](docs/09-portfolio/QA_EVIDENCE_DEMO.md)

## エンジニアリング原則

- **Implemented ≠ Tested ≠ Runtime Verified**
- **ClaimとEvidenceを分離**
- **AIを真実の根拠にしない**
- **Historical PASS ≠ Current Revision PASS**
- **Production / Publication Gateはfail-closed**
- **Infrastructure FailureをProduct FailureにもPASSにも読み替えない**

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

Final source freeze後のmain SHAについて、次のrequired workflowをすべてGreenにします。

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

`missing / cancelled / skipped / infrastructure-blocked`をPASSへ読み替えません。

```powershell
$sha = (git rev-parse HEAD).Trim()
python tools/verify_final_ci_matrix.py --sha $sha --wait-minutes 60
```

その後、同じSHAでbenchmark、QA Bug Zoo、Project Truth scenarioを生成し、finalizerで束ねます。

```powershell
.\scripts\Complete-PortfolioRelease.ps1 -Version <x.y.z>
```

Release assetまで進める場合:

```powershell
.\scripts\Complete-PortfolioRelease.ps1 -Version <x.y.z> -PublishRelease
```

## Benchmark / Soak

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

## Engine integrationの検証境界

Unity / Unreal collectorやintegration sourceはProjectの一部ですが、**実target projectがない状態ではengine-specific runtimeをFinal Publicationの必須条件にしません**。

Final Evidenceでは、実target projectを供給していない場合は次のように明示します。

```text
Unity engine runtime  = NOT_EVALUATED
Unreal engine runtime = NOT_EVALUATED
```

実target projectが後から用意できた場合だけ、optional verifierを個別に実行し、final mandatory gateとは別Evidenceとして提示します。

```powershell
python tools/verify_unity_real_project.py `
  --expected-dip-sha <DIP_SHA> `
  --project <UnityProject> `
  --unity <UnityEditor.exe>

python tools/verify_unreal_real_project.py `
  --expected-dip-sha <DIP_SHA> `
  --project <UnrealProject> `
  --unreal-editor-cmd <UnrealEditor-Cmd.exe>
```

GitHub-hosted Unreal workflowはcontract / unit behaviorを検証し、actual Editor runtimeの代替とは扱いません。

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
  └─ engine / native / toolchain integrations
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
| NOT_EVALUATED | claim対象外として明示的に未評価 |

- Historical baseline: [`docs/09-portfolio/evidence/verified-baseline/`](docs/09-portfolio/evidence/verified-baseline/)
- Exact-revision policy: [`docs/09-portfolio/evidence/latest-revision.md`](docs/09-portfolio/evidence/latest-revision.md)
- Benchmark rule: [`docs/09-portfolio/benchmark-results.md`](docs/09-portfolio/benchmark-results.md)
- Publication checklist: [`docs/09-portfolio/PUBLICATION_CHECKLIST.md`](docs/09-portfolio/PUBLICATION_CHECKLIST.md)

## 主な技術領域

- C# / .NET 8: Domain / Application / API / Worker / Blazor
- PostgreSQL: durable truth / queue / migration / DR
- Python 3.12+: analysis / QA / Evidence tooling
- C++ / native QA / collector
- Unity / Unreal / DCC / toolchain integration
- GitHub Actions / Playwright

## ローカルRuntime

通常は.NET 8、PostgreSQL 16-compatible server + client tools、Python 3.12+、Gitを使用します。Browser E2EではNode.js 22+、C++ QAではCMake + compiler toolchainを使用します。Unity / Unrealはoptional engine-specific verificationを実行する場合だけ必要です。

`Start-DIP.ps1` はPostgreSQLを確認し、Python Analysis / API / Worker / Webを起動します。`Stop-DIP.ps1` はDIP processを停止します。

## Reviewer向け読み順

1. このREADME
2. [`QA_EVIDENCE_CASE_STUDY.md`](docs/09-portfolio/QA_EVIDENCE_CASE_STUDY.md)
3. [`ENGINEERING_JOURNEY.md`](docs/09-portfolio/ENGINEERING_JOURNEY.md)
4. [`GAME_DEVELOPMENT_SCENARIO.md`](docs/09-portfolio/GAME_DEVELOPMENT_SCENARIO.md)
5. [`reviewer-brief.md`](docs/09-portfolio/reviewer-brief.md)
6. Architecture / Evidence / CI artifacts

DIPはgameplayやrendering単体のshowcaseではなく、**ゲーム制作を支えるDevelopment Infrastructure / QA / Developer Productivity / Production Engineering**を示すProjectです。
