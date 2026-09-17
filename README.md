# Development Intelligence Platform (DIP)

DIPは、ゲーム・メディア・ソフトウェア開発チーム向けのDevelopment Intelligence（開発インテリジェンス）基盤です。Git / CI / QA / Runtime / Performance / Project Dataを共通のProject Contextへ集約し、出所とrevisionを追跡できるEvidenceとして扱います。Evidenceを基に、品質判断、Project Truth、性能分析、リリース準備状況、Evidenceに基づくデバッグを支援します。

**Portfolio focus:** C# / .NET 8、PostgreSQL、Python、C++ / native Collector、Unity / Unreal連携、GitHub Actionsを別々のデモとして並べるのではなく、**Evidenceとexact-revision verificationを中心に一つの開発基盤として接続した設計・実装・検証プロセス**を示すProjectです。

![DIP portfolio demo](docs/09-portfolio/demo/dip-portfolio-demo.gif)

> **このGIFはsample-data previewです。** 製品範囲を短時間で確認するためのUI previewであり、Runtime Verifiedの根拠には使用しません。実Projectの検証結果はcommit-bound Evidenceとして別に扱います。

## 60秒レビュー

1. 下の「まず見てほしい3領域」で、DIPが解こうとしている問題と主軸を確認する。
2. 「実Project Case Study」で、実Unity ProjectからDIPへEvidenceが流れる経路を確認する。
3. 「Verification status」と「Final exact-revision gate」で、Implemented / Tested / Runtime Verifiedをどう分離しているか確認する。
4. さらに設計判断を見る場合は [`ENGINEERING_JOURNEY.md`](docs/09-portfolio/ENGINEERING_JOURNEY.md) を読む。

## Verification status

DIPはREADME上の説明を検証結果そのものとして扱いません。**最終提出revisionは、同一SHAに紐付いたCI / benchmark / engine runtime / Project Truth / Release Evidenceが揃うまで `Current Revision PASS` を主張しません。**

| Claim | Verifiedと扱う条件 |
|---|---|
| Implemented | 対応するsourceが存在する |
| Tested | 対象revisionのtest artifact / workflowが成功している |
| Runtime Verified | 対象revisionを実runtimeで検証したEvidenceがある |
| Performance Verified | clean working tree + exact SHAで生成したbenchmark artifactがある |
| Final Portfolio Revision | required 11 workflow、runtime Evidence、final Evidence binding、Releaseが同一SHAに紐付く |

最終状態のSource of Truthは、README中の固定文言ではなく、GitHub Actions / Release artifact / `artifacts/final-evidence/` に保存されたexact-revision Evidenceです。

## まず見てほしい3領域

### 1. Project Truth / Evidence

ClaimとEvidenceを分離し、出所、revision、freshness、contradictionを追跡します。実装済み・テスト済み・runtime検証済みを同一視せず、現在のrevisionに根拠がある状態だけをVerifiedとして扱います。

### 2. Game Development QA

Unity / Unreal / C++ / toolchain integrationから得たEvidenceを、QA Finding、再現性、regression、performance、quality gateへ接続します。Engine固有payloadを保持しつつ、意思決定に必要な情報をprovider-neutral Evidenceへ昇格します。

### 3. Verification / Release Operations

Unit / Integration / E2E、GUI、security、benchmark、migration、backup/restore、release readinessをexact revisionへ結び付けます。CIが実行できなかった状態や古いPASSを、現在revisionの成功として扱いません。

その他の機能は、この3領域を支える周辺機能として位置付けています。

## 実Project Case Study

デフォルトの実Project Case Studyには、別repositoryで開発しているUnity Project **MAYAtoUnity** を使用します。

```text
MAYAtoUnity (real Git-backed Unity Project)
        ↓
actual Unity Editor batchmode
        ↓
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

Case Studyでは次の2つを混同しません。

- `dipSourceCommit`: 検証しているDIP自身のSHA
- `targetCommit`: Collectorが解析したMAYAtoUnity側のSHA

このCase Studyが示すのは**実Projectとのend-to-end integration**です。第三者チームへのproduction導入実績を意味するものではなく、そのようなclaimは別Evidenceなしに行いません。

実行方法とEvidence contract: [`REAL_PROJECT_CASE_STUDY.md`](docs/09-portfolio/REAL_PROJECT_CASE_STUDY.md)

2〜3分の実演構成: [`REAL_PROJECT_DEMO.md`](docs/09-portfolio/REAL_PROJECT_DEMO.md)

## エンジニアリング原則

- **Implemented ≠ Tested ≠ Runtime Verified.** Sourceの存在だけで現在revisionを検証済みとは扱いません。
- **ClaimとEvidenceを分離する。** Project TruthはConfirmed、Partially Confirmed、Unverified、Contradicted、Staleを区別します。
- **AIを真実の根拠にしない。** **Evidence-Grounded Autonomous Debugging** は `Hypothesis → Candidate → Isolated Verification → Human Review` の順で進みます。
- **Historical PASS ≠ Current Revision PASS.** Historical Evidenceはbaselineであり、新しいrevisionへ暗黙に継承しません。
- **Production Operationsはfail-closed。** Authentication、QA isolation、backup integrity、exact-revision packaging、readinessを明示的なGateとして扱います。
- **Flexible Spreadsheet Intelligence** もProject / Evidence Modelへ参加しますが、ポートフォリオの主軸はProject Truth、Game Development QA、Verification / Release Operationsです。

初期実装から現在の検証中心設計への変化は [`ENGINEERING_JOURNEY.md`](docs/09-portfolio/ENGINEERING_JOURNEY.md) にまとめています。過去commitを書き換えて見栄えを整えるのではなく、開発プロセスの改善自体をEvidenceとして残します。

## GitHub Actions — 3段階実行

Hosted runner quotaを通常のpushで消費しないため、CIを3段階に分けています。

```text
1. work/* / feat/* / fix/* / chore/*
   -> push / PRでHosted Actionsを自動起動しない

2. workflow_dispatch
   -> Fast / Targeted / Full を明示的に選ぶ

3. main + .portfolio-ci-trigger
   -> final提出 / Release時だけrequired 11 workflowを起動
```

手動CI:

```powershell
.\scripts\Invoke-ManualCI.ps1 -Preset Fast -Ref work/example
.\scripts\Invoke-ManualCI.ps1 -Preset Targeted -Target DotNet,Python,Native -Ref work/example
.\scripts\Invoke-ManualCI.ps1 -Preset Full -Ref work/example
```

詳細: [`CI_EXECUTION_POLICY.md`](docs/09-portfolio/CI_EXECUTION_POLICY.md)

## Final exact-revision gate

Final source freezeは`.portfolio-ci-trigger`だけを変更するPRとして行います。merge後にできたmain SHAについて、次の11 workflowをすべてGreenにする必要があります。

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

Final helper:

```powershell
.\scripts\Request-FinalVerification.ps1 -Commit -CreatePullRequest
```

Final main SHAでは次の1本が、11/11 CI、performance、real Unity、real Unreal、Project Truth、Evidence bundle、Releaseを同一SHAへ束ねます。

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

現在値を測定していないSHAでは`NOT YET MEASURED`のまま扱います。詳細: [`benchmark-results.md`](docs/09-portfolio/benchmark-results.md)

## Unity / Unreal Runtime Evidence

UnityはMAYAtoUnityを実Projectとして検証でき、実Unity Editor process、DIP SHA、target SHA、Collector reportをEvidenceへ保存します。

```powershell
python tools/verify_unity_real_project.py --expected-dip-sha <DIP_SHA>
```

Unrealは実`UnrealEditor-Cmd`とGit-backed `.uproject`を要求します。生成fixtureをreal-project claimの代替にはしません。

```powershell
python tools/verify_unreal_real_project.py --expected-dip-sha <DIP_SHA> --project <UnrealProject>
```

最終Evidenceは`tools/finalize_portfolio_evidence.py`が同一DIP SHAであることをfail-closedに確認します。

## Live Native Deploymentの意味

`workflow success`と`Production Deployed`を同一視しません。

| 状態 | 意味 |
|---|---|
| Policy Verified | deployment条件・設定の検査が成功 |
| Production Deployed | production mutationを実際に実行 |
| Production Mutation Disabled | production変更を意図的に実行していない |

`Live Native Deployment`はmanual-onlyで、既定は`deploy=false`です。明示的な`deploy=true`の場合だけproduction mutationへ進みます。

## Native-firstアーキテクチャ

```text
Browser
  │
  ├── Dip.Web (.NET / Blazor)          :5081
  │       │
  │       └── Dip.Api (.NET)           :5080
  │               │
  │               ├── PostgreSQL       durable truth + durable queue
  │               ├── Python Analysis  :8100
  │               └── filesystem       artifacts / workspaces
  │
  └── Dip.Worker (.NET Worker)         :9091 metrics
          │
          ├── PostgreSQL-backed jobs / queue
          ├── native QA processes
          └── Unity / Unreal / C++ / toolchain integrations
```

PostgreSQLをdurable Source of Truthかつqueue / coordination boundaryとして使い、.NET / Python serviceはhost上で直接実行します。Docker Desktop / Docker Engine / external queue daemonは正式なNative-first runtimeの必須依存ではありません。

詳細: [`ARCHITECTURE_OVERVIEW.md`](docs/09-portfolio/ARCHITECTURE_OVERVIEW.md)

## Evidenceと検証境界

| 状態 | 意味 |
|---|---|
| Implemented | Sourceが存在する |
| Tested | 対象testが成功している |
| Runtime Verified | 対象revisionを実runtimeで検証したEvidenceがある |
| Infrastructure Blocked | runner / tool / environment都合で検証を完了できない |

- Historical baseline: [`docs/09-portfolio/evidence/verified-baseline/`](docs/09-portfolio/evidence/verified-baseline/)
- Exact-revision policy: [`docs/09-portfolio/evidence/latest-revision.md`](docs/09-portfolio/evidence/latest-revision.md)
- Benchmark publication rule: [`docs/09-portfolio/benchmark-results.md`](docs/09-portfolio/benchmark-results.md)
- Publication checklist: [`docs/09-portfolio/PUBLICATION_CHECKLIST.md`](docs/09-portfolio/PUBLICATION_CHECKLIST.md)

## 主な技術領域

- C# / .NET 8: Domain、Application、API、Worker、Blazor Web
- PostgreSQL: durable truth、queue、coordination、migration / DR
- Python 3.12+: analysis / QA integration / final Evidence tooling
- C++ / native collector
- Unity / Unreal / DCC / toolchain integrations
- GitHub Actions: manual CI、exact-revision final matrix、release
- Playwright: browser E2E

## ローカルRuntime

通常のNative-first runtimeには.NET 8、PostgreSQL 16-compatible server + client tools、Python 3.12+、Gitが必要です。Browser E2EではNode.js 22+を使用します。Unity / Unreal / CMakeは、それぞれのintegrationを実runtimeで検証するときだけ必要です。

`Start-DIP.ps1` はPostgreSQLの到達性を確認し、Python Analysis / API / Worker / Webをhost processとして起動します。`Stop-DIP.ps1` はDIP processを停止します。

## Reviewer向け読み順

1. このREADMEの「60秒レビュー」
2. このREADMEの「まず見てほしい3領域」
3. このREADMEの「実Project Case Study」
4. このREADMEの「Verification status」と「Final exact-revision gate」
5. [`ENGINEERING_JOURNEY.md`](docs/09-portfolio/ENGINEERING_JOURNEY.md)
6. [`GAME_DEVELOPMENT_SCENARIO.md`](docs/09-portfolio/GAME_DEVELOPMENT_SCENARIO.md)
7. [`REAL_PROJECT_DEMO.md`](docs/09-portfolio/REAL_PROJECT_DEMO.md)
8. [`reviewer-brief.md`](docs/09-portfolio/reviewer-brief.md)
9. Architecture / Evidence / CI artifacts

DIPはgameplayやrendering単体のshowcaseではなく、**ゲーム制作を支えるDevelopment Infrastructure / QA / Developer Productivity / Production Engineering**を示すProjectです。
