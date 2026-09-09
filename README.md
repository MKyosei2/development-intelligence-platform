# Development Intelligence Platform (DIP)

DIPは、ゲーム・メディア・ソフトウェア開発チーム向けのDevelopment Intelligence（開発インテリジェンス）基盤です。Git / CI / QA / Runtime / Performance / Project Dataを共通のProject Contextへ集約し、出所とrevisionを追跡できるEvidenceとして扱います。Evidenceを基に、品質判断、Project Truth、性能分析、リリース準備状況、Evidenceに基づくデバッグを支援します。

![DIP portfolio demo](docs/09-portfolio/demo/dip-portfolio-demo.gif)

> GIFは製品範囲を短時間で確認するためのsample-data previewです。Runtime Verifiedの根拠には使用せず、実測・検証結果はcommit-bound Evidenceとして分離します。

## ゲーム開発での最短ユースケース

```text
Unity / Unreal project
        ↓
Engine / QA Collector
        ↓
Asset / Build / Scene / Runtime / Performance Evidence
        ↓
Project Truth + Quality Gate
        ↓
Release decision / PM・Lead review
```

Unity CollectorはAsset QA、Build Settings、Scene / Reference、Animation、Runtime Performanceを同じEvidence pipelineへ送ります。Unrealやnative toolchainもprovider固有の結果を共通Project Contextへ正規化します。詳細は [`GAME_DEVELOPMENT_SCENARIO.md`](docs/09-portfolio/GAME_DEVELOPMENT_SCENARIO.md) を参照してください。

## 中核となる3領域

### 1. Project Truth / Evidence

ClaimとEvidenceを分離し、出所、revision、freshness、contradictionを追跡します。実装済み・テスト済み・runtime検証済みを同一視せず、現在のrevisionに根拠がある状態だけをVerifiedとして扱います。

### 2. Game Development QA

Unity / Unreal / C++ / toolchain integrationから得たEvidenceを、QA Finding、再現性、regression、performance、quality gateへ接続します。Engine固有payloadを保持しつつ、意思決定に必要な情報だけをprovider-neutral Evidenceへ昇格します。

### 3. Verification / Release Operations

Unit / Integration / E2E、GUI、security、benchmark、migration、backup/restore、release readinessをexact revisionへ結び付けます。CIが実行できなかった状態や古いPASSを、現在revisionの成功として扱いません。

## エンジニアリング原則

- **Implemented ≠ Tested ≠ Runtime Verified.** Sourceの存在だけで現在revisionを検証済みとは扱いません。
- **ClaimとEvidenceを分離する。** Project TruthはConfirmed、Partially Confirmed、Unverified、Contradicted、Staleを区別します。
- **AIを真実の根拠にしない。** **Evidence-Grounded Autonomous Debugging** は `Hypothesis → Candidate → Isolated Verification → Human Review` の順で進みます。
- **Historical PASS ≠ Current Revision PASS.** Historical Evidenceはbaselineであり、新しいrevisionへ暗黙に継承しません。
- **Production Operationsはfail-closed。** Authentication、QA isolation、backup integrity、exact-revision packaging、readinessを明示的なGateとして扱います。
- **Flexible Spreadsheet Intelligence** もProject / Evidence Modelへ参加しますが、ポートフォリオの主軸はProject Truth、Game Development QA、Verification / Release Operationsです。

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

DIPでは次を別状態として扱います。

| 状態 | 意味 |
|---|---|
| Implemented | Sourceが存在する |
| Tested | 対象testが成功している |
| Runtime Verified | 対象revisionを実runtimeで検証したEvidenceがある |
| Infrastructure Blocked | runner / tool / environment都合で検証を完了できない |

Historical benchmarkやQA Bug Zooはmeasurement pipelineが実際に動いたbaselineです。ただし、新しいsource revisionのperformance値として再ラベルしません。公開時はfinal SHA自身のCI / runtime artifactを確認します。

- Historical baseline: [`docs/09-portfolio/evidence/verified-baseline/`](docs/09-portfolio/evidence/verified-baseline/)
- Exact-revision policy: [`docs/09-portfolio/evidence/latest-revision.md`](docs/09-portfolio/evidence/latest-revision.md)
- Benchmark publication rule: [`docs/09-portfolio/benchmark-results.md`](docs/09-portfolio/benchmark-results.md)
- Publication checklist: [`docs/09-portfolio/PUBLICATION_CHECKLIST.md`](docs/09-portfolio/PUBLICATION_CHECKLIST.md)

## 主な技術領域

- C# / .NET 8: Domain、Application、API、Worker、Blazor Web
- PostgreSQL: durable truth、queue、coordination、migration / DR
- Python 3.12+: analysis / QA integration
- C++ / native collector
- Unity / Unreal / DCC / toolchain integrations
- GitHub Actions: build、test、security、E2E、publication readiness、release
- Playwright: browser E2E

## ローカルRuntime

通常のNative-first runtimeには.NET 8、PostgreSQL 16-compatible server + client tools、Python 3.12+、Gitが必要です。Browser E2EではNode.js 22+を使用します。Unity / Unreal / CMakeは、それぞれのintegrationを実runtimeで検証するときだけ必要です。

`Start-DIP.ps1` はPostgreSQLの到達性を確認し、Python Analysis / API / Worker / Webをhost processとして起動します。`Stop-DIP.ps1` はDIP processを停止します。Formal verificationの前提と境界は [`runtime-verification.md`](docs/09-portfolio/runtime-verification.md) に集約しています。

## CI / Release

公開対象revisionでは、少なくとも.NET、Python、Native Collector、Integration / E2E、GUI E2E、Security、Universal QA、Unreal Collector、Native Package、Portfolio Publication Readinessの対象checkを確認します。Step未実行・runner不足・cancelledをProduct PASSへ読み替えません。

`Live Native Deployment` はproduction mutationの可否を明示的に検証し、実deploy時はimmutable release、migration、backup、readiness、rollbackを扱います。

## Reviewer向け読み順

1. このREADMEのGIFと「ゲーム開発での最短ユースケース」
2. [`GAME_DEVELOPMENT_SCENARIO.md`](docs/09-portfolio/GAME_DEVELOPMENT_SCENARIO.md)
3. [`ENGINEERING_CASE_STUDY.md`](docs/09-portfolio/ENGINEERING_CASE_STUDY.md)
4. [`reviewer-brief.md`](docs/09-portfolio/reviewer-brief.md)
5. Architecture / Evidence / CI artifacts

DIPはgameplayやrendering単体のshowcaseではなく、**ゲーム制作を支えるDevelopment Infrastructure / QA / Developer Productivity / Production Engineering**を示すProjectです。
