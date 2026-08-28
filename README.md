# Development Intelligence Platform

**Development Intelligence Platform（DIP）** は、分散している **Git / CI / QA / Runtime / Performance / Project data** を追跡可能なEvidenceとして統合し、開発・制作・プロジェクト状態を同じProject Contextで判断するためのブラウザ中心のDevelopment Intelligence Platformです。

対象領域はソフトウェア／ゲーム開発を中心に、映像・VFXなどの制作管理へ拡張できるよう、Coreを特定業界のEntityへ固定しない構成にしています。

[![.NET CI](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/dotnet-ci.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/dotnet-ci.yml)
[![Integration and E2E](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/integration-test.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/integration-test.yml)
[![GUI E2E](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/gui-e2e.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/gui-e2e.yml)
[![Security Scan](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/security-scan.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/security-scan.yml)

<p align="center">
  <img src="docs/09-portfolio/demo/dip-portfolio-demo.gif" alt="DIP portfolio demo using sample data" width="920" />
</p>

> GIF・スクリーンショットは決定論的なサンプルデータによる製品デモです。画像が存在することや過去RevisionがPASSしたことを、最新RevisionのVerificationとして扱いません。Historical EvidenceとExact Revision Evidenceは [`docs/09-portfolio/evidence/`](docs/09-portfolio/evidence/) で分離します。

---

## 5分で分かるDIP

### What

DIPは、GitHub、CI、QA、Runtime、Performance、Spreadsheet、Project Management情報をEvidenceとして正規化し、Project / Development Graph上で関連付けます。

```text
Development / Production activity
            ↓
        Evidence
            ↓
Universal Project / Development Graph
            ↓
Deterministic evaluation
            ↓
Risk / Progress / QA / Debug hypotheses
            ↓
Action / Verification
```

### Why

通常の開発では、進捗表・GitHub・CI・QA・Runtime・制作管理が別々に存在します。その結果、次の問いに答えるために複数ツールを横断する必要があります。

- 報告された進捗のうち、Evidenceで確認できる部分はどこか
- 現在RevisionはRelease可能か
- Regressionは製品側か、QA環境か、Infrastructure側か
- どの変更がFailureと関係しているか
- AIが提案した修正は隔離環境でも成立するか
- Spreadsheet上の工程・Shot・InspectionなどをCoreを書き換えず統合できるか

### 3つの中心機能

1. **Project Truth / Evidence Graph** — ClaimとEvidenceを分離し、Confirmed / PartiallyConfirmed / Unverified / Contradicted / Staleとして評価します。
2. **Evidence-Grounded Autonomous Debugging** — AIをTruth Sourceにせず、Hypothesis → Candidate → Isolated Verification → Human Reviewの境界を維持します。
3. **Cross-domain Spreadsheet Intelligence** — 任意XLSXのHeaderを推定し、Software / Media / Construction / Generic ProjectとしてDry Run後にUniversal Graphへ取り込みます。

### Current verification rule

DIPでは以下を同一視しません。

```text
Implemented ≠ Tested ≠ Runtime Verified
Historical PASS ≠ Current Revision PASS
```

最新Revisionの状態は [`docs/09-portfolio/evidence/latest-revision.md`](docs/09-portfolio/evidence/latest-revision.md) を参照してください。GitHub ActionsがRunner/account側の理由で1 stepも実行されていない場合、そのRunをSource failureにもPASSにも変換しません。

---

## 目次

- [アーキテクチャ](#アーキテクチャ)
- [Project Truth / Evidence Graph](#project-truth--evidence-graph)
- [Universal Project / Development Graph](#universal-project--development-graph)
- [QA / Defect Intelligence](#qa--defect-intelligence)
- [Evidence-Grounded Autonomous Debugging](#evidence-grounded-autonomous-debugging)
- [Spreadsheet / Project Management Intelligence](#spreadsheet--project-management-intelligence)
- [GUIでの確認](#guiでの確認)
- [Quick Start](#quick-start)
- [Verification](#verification)
- [Troubleshooting](#troubleshooting)
- [Portfolio Evidence](#portfolio-evidence)

---

# アーキテクチャ

<p align="center">
  <img src="docs/09-portfolio/architecture.svg" alt="DIP architecture" width="960" />
</p>

```text
Browser
  │
  ▼
Dip.Web (Blazor)
  │
  ▼
Dip.Api
  ├── PostgreSQL       Durable project truth
  ├── Redis            Coordination / delivery
  ├── Dip.Worker       Asynchronous jobs
  └── Python Analysis  Analysis / computation

External / Production Sources
  ├── GitHub / CI
  ├── Unity / Unreal
  ├── Maya / Native / Runtime
  ├── Spreadsheet
  └── Project-management integrations
```

PostgreSQLをDurable Source of Truthとし、Redis・Worker・Analysis AdapterのFailureがProject Stateそのものを勝手に書き換えないように責務を分離しています。

詳細: [`docs/09-portfolio/ARCHITECTURE_OVERVIEW.md`](docs/09-portfolio/ARCHITECTURE_OVERVIEW.md)

---

# Project Truth / Evidence Graph

DIPでは、ClaimとEvidenceを分離します。

```text
Claim
  ↓
Evidence
  ↓
Verification state
  ↓
Risk / Blocker / Progress
  ↓
Action
  ↓
Verification
```

代表的なVerification State:

```text
Confirmed
PartiallyConfirmed
Unverified
Contradicted
Stale
```

ThresholdやPenaltyはEvaluator内へMagic Numberとして分散させず、Version管理可能な`ProjectTruthPolicy`へ集約します。Default Policyは既存挙動を定義するものであり、統計的最適値であることを主張しません。

詳細: [`docs/09-portfolio/PROJECT_TRUTH_POLICY.md`](docs/09-portfolio/PROJECT_TRUTH_POLICY.md)

---

# Universal Project / Development Graph

特定業界ごとにCore Entityを増やし続けないため、Domain固有情報はUniversal GraphのNode / Edge / Metadataへ正規化します。

```text
Universal Evidence Model
        ↓
Universal Project Graph
        ↓
Domain Adapter / Domain Pack
        ↓
Intelligence Engine
```

代表Metadata:

```text
domainId
kind
sourceObjectId
relationKind
schemaVersion
```

例:

```text
software.task
software.issue
software.build

media.task
media.shot
media.render-job
media.review

construction.task
construction.inspection
construction.approval
construction.drawing
```

この構造により、未知Domainを扱うたびにCore Enum / DB Table / Serviceを増殖させることを避けます。

---

# QA / Defect Intelligence

DIPは専用QAとCapability-based QAの両方を扱います。

主な概念:

- Stable Finding Fingerprint
- Reproduction
- Regression Origin
- Defect Evidence
- Isolated Verification
- Regression Replay
- Release Gate
- QA Governance
- Runtime Diagnostics

対象または拡張可能な技術領域:

- C# / C++ / Python
- JavaScript / TypeScript
- Go / Rust / Java / Kotlin
- Ruby / PHP / Shell / Lua
- HLSL / GLSL
- Unity / Unreal Engine / Native Runtime
- Custom toolchain

---

# Evidence-Grounded Autonomous Debugging

Autonomous Debuggingは、AIにSource Workspaceを自由編集させる機能ではありません。

```text
Failure Evidence
      ↓
Root-cause hypotheses
      ↓
Bounded experiments
      ↓
Small candidate patch
      ↓
Disposable verification workspace
      ↓
Regression / available QA
      ↓
ReadyForHumanReview / NotVerified
```

安全境界:

- AI自身は自分のPatchをVerifiedと判定できない
- Evidence IDの捏造を許可しない
- Experiment / AI Call / Wall ClockへBudget上限を設ける
- Test無効化やDiagnostic Suppressionを目的とするPatchをVerification境界で拒否できる
- Source Workspaceは自動変更しない
- Verified Candidateでも最終Human Reviewを要求する
- Session / Hypothesis / ExperimentをDevelopment Graphへ記録する
- Session数、Experiment数、AI Call数、最終Status、DurationをTelemetryとして記録する

GUIではProjectの **QA → Autonomous Debug** から実行・確認できます。AI Providerが無効な環境では`NoGroundedHypothesis`として明示し、成功したようには見せません。

---

# Spreadsheet / Project Management Intelligence

XLSXを単なるExport形式ではなく、Project WorkItem / Evidence Providerとして扱います。

推定対象の代表Field:

```text
Key / External ID / Title / Description
Owner / Status / Progress
Start Date / Due Date / Priority
Dependency / Milestone / Cost / Risk / Approval
```

日本語・英語Headerを同じSemantic Fieldへ正規化し、Sheet名とHeaderから以下のDomainを推定します。

```text
software
media.production
construction
generic.project
```

GUIでは **Management Operations → Flexible Spreadsheet Intelligence** から、

```text
Upload XLSX
    ↓
Analyze schema
    ↓
Mapping / Domain preview
    ↓
Dry Run
    ↓
Apply Import
```

の順に操作します。直接Importせず、Preview / Dry Runを挟むことを基本とします。

---

# Management Operations

管理系では以下を扱います。

- Daily Cockpit
- Risk / Decision / Action
- Approval / RACI
- Resource Capacity / Calendar
- Milestone / Progress
- Notification / Audit
- Source Authority / Sync Conflict
- Google Sheets / CalendarなどのIntegration境界

External IntegrationはCredential・Sandbox・実Runが確認できるまで、README上の記述だけでRuntime Verifiedとはしません。

---

# GUIでの確認

代表的なProject-scoped GUI:

```text
Portfolio
  ↓
Project Overview
  ↓
Repository / CI / QA
  ↓
QA → Autonomous Debug
  ↓
Development Intelligence
  ↓
QA Governance
  ↓
Management Operations
  ↓
Flexible Spreadsheet Intelligence
  ↓
Settings / Audit / Notifications
```

Playwright E2Eでは、既存導線に加えてAutonomous Debug panelとFlexible Spreadsheet panelがProject Context内で表示できることを確認します。External AIやGitHub CredentialがなくてもDeterministic GUI Testは実行可能です。

---

# 使用技術

| 分野 | 技術 |
|---|---|
| Backend | .NET 8 / C# / ASP.NET Core |
| Web | Blazor |
| Database | PostgreSQL |
| Coordination | Redis |
| Worker | .NET Worker |
| Analysis | Python |
| Native / Engine | C++ / CMake / Unity / Unreal / Maya |
| Container | Docker / Docker Compose |
| CI | GitHub Actions |
| Observability | OpenTelemetry / Prometheus / Grafana / .NET Metrics |
| UI E2E | Playwright / Chromium |
| Automation | PowerShell / Python |

---

# リポジトリ構成

```text
src/
  Dip.Api/
  Dip.Application/
  Dip.Domain/
  Dip.Infrastructure/
  Dip.Shared/
  Dip.Web/
  Dip.Worker/

collectors/
services/
tests/
tools/
scripts/
docs/
```

新しい概念を追加する際は、既存Graph / Service / Pageへ自然に統合できる場合は新規Source Fileを増やさない方針です。External-format固有Parserや独立Orchestrationなど、責務分離に意味がある場合のみ新規ファイルを許容します。

---

# Quick Start

## 必要環境

- Windows 10 / 11（主開発環境）またはDockerが利用できる環境
- Git
- .NET SDK 8.x
- Docker Desktop / Docker Engine + Docker Compose v2
- PowerShell / PowerShell Core
- Node.js / npm（Playwright実行時）

確認:

```powershell
git --version
dotnet --version
docker version
docker compose version
node --version
npm --version
```

## Clone

```powershell
git clone https://github.com/MKyosei2/development-intelligence-platform.git
cd development-intelligence-platform
```

## 1. Release Build

```powershell
dotnet restore .\DevelopmentIntelligencePlatform.sln
dotnet build .\DevelopmentIntelligencePlatform.sln -c Release --no-restore
```

Compile Errorが0になる前にGUI/Docker問題へ進まないことを推奨します。

## 2. Isolated GUI Runtime Verification

固定Portを前提にせず、空きPortと固有Compose Projectを自動選択します。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-GuiRuntime.ps1
```

Playwrightまで同じStackで確認する場合:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-GuiRuntime.ps1 -RunPlaywright
```

Chromiumを表示して確認する場合:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-GuiRuntime.ps1 -RunPlaywright -Headed
```

既存Imageを再利用したい場合:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-GuiRuntime.ps1 -ReuseImages -RunPlaywright
```

失敗時にContainerを残す場合:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-GuiRuntime.ps1 -KeepOnFailure
```

Verification artifactは以下へ保存されます。

```text
artifacts/gui-runtime-verification/<timestamp>/
```

---

# Verification

推奨順序:

```text
1. dotnet restore
2. dotnet build -c Release
3. Changed-area unit tests
4. Domain / Application / Infrastructure tests
5. Integration tests
6. Isolated Docker runtime
7. API / Worker / Web health
8. Playwright GUI E2E
9. Verify-Solution.ps1
10. Publication readiness
```

代表コマンド:

```powershell
dotnet test .\tests\Dip.Domain.Tests\Dip.Domain.Tests.csproj -c Release
dotnet test .\tests\Dip.Application.Tests\Dip.Application.Tests.csproj -c Release
dotnet test .\tests\Dip.Infrastructure.Tests\Dip.Infrastructure.Tests.csproj -c Release
```

Full verification:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-Solution.ps1
```

Publication check:

```powershell
python tools/verify_publication_safety.py --root .
python tools/validate_portfolio_readiness.py --root .
```

## Commit message

`main`へpushするCommit SubjectはConventional Commitを使用します。

```text
feat(web): expose autonomous debugging workflow
fix(ci): stabilize isolated GUI verification
test(management): cover cross-domain spreadsheet inference
docs(portfolio): clarify exact revision evidence
```

現在のvalidatorをローカル確認する場合:

```powershell
git log -1 --format=%s | python tools/validate_commit_message.py
```

Patch適用後のCommitも、`その他の修正`のような自由形式ではなくConventional Commitで作成してください。

---

# Troubleshooting

## DockerのstderrがPowerShell Errorとして扱われる

Dockerは正常な進捗もstderrへ出すことがあります。`Verify-GuiRuntime.ps1`はNative CommandのstderrをLogとして保持し、**Process Exit Code**で成否判定します。

## APIがUnhealthy

```powershell
docker compose ps api
docker compose logs --tail 400 api
```

`container ... unhealthy`は結果でありRoot Causeではありません。API内部Exception、Migration、Database Connectionを確認してください。

## Port bind failure

Verification scriptではOSから空きPortを動的取得します。通常運用で固定Portを使用する場合は以下を変更できます。

```text
POSTGRES_PORT
REDIS_PORT
ANALYSIS_PORT
API_PORT
WORKER_METRICS_PORT
WEB_PORT
```

## Persistent Dataを消したくない

```powershell
docker compose down --remove-orphans
```

は通常Named Volumeを保持します。

```powershell
docker compose down -v
```

はVolumeを削除するため、意図的なReset以外では使用しないでください。

---

# Portfolio Evidence

Historical verified baselineは測定履歴として保持しますが、新しいCommitへ自動継承しません。

- Historical baseline: [`docs/09-portfolio/evidence/verified-baseline/`](docs/09-portfolio/evidence/verified-baseline/)
- Latest revision policy/status: [`docs/09-portfolio/evidence/latest-revision.md`](docs/09-portfolio/evidence/latest-revision.md)
- Runtime verification policy: [`docs/09-portfolio/runtime-verification.md`](docs/09-portfolio/runtime-verification.md)
- Implementation status: [`docs/IMPLEMENTATION_STATUS.md`](docs/IMPLEMENTATION_STATUS.md)

代表的なHistorical Verified Baseline:

| Evidence | Measured result |
|---|---:|
| Project list p95 | **4.174 ms** |
| Collector p95 | **34.507 ms** |
| 100 req/s error rate | **0.0000%**（1,500 requests） |
| 1M Metric query p95 | **363.796 ms** |
| QA Bug Zoo recall | **19 / 19 = 100%** |
| QA clean-control false positives | **0 / 7 = 0%** |
| QA reproducibility | **9 / 9 = 100%** |

これらはEvidenceに記録された特定Revisionの値であり、最新RevisionのPASSを意味しません。

---

# Screenshots

| Project Intelligence | QA / Defect Intelligence | Management Operations |
|---|---|---|
| ![](docs/09-portfolio/screenshots/project-intelligence.png) | ![](docs/09-portfolio/screenshots/qa-defect.png) | ![](docs/09-portfolio/screenshots/management-operations.png) |

---

# Reviewer向け資料

- [`docs/09-portfolio/reviewer-brief.md`](docs/09-portfolio/reviewer-brief.md)
- [`docs/09-portfolio/ENGINEERING_CASE_STUDY.md`](docs/09-portfolio/ENGINEERING_CASE_STUDY.md)
- [`docs/09-portfolio/ARCHITECTURE_OVERVIEW.md`](docs/09-portfolio/ARCHITECTURE_OVERVIEW.md)
- [`docs/09-portfolio/PROJECT_TRUTH_POLICY.md`](docs/09-portfolio/PROJECT_TRUTH_POLICY.md)
- [`SECURITY.md`](SECURITY.md)
- [`CONTRIBUTING.md`](CONTRIBUTING.md)

---

# License

MIT — [`LICENSE`](LICENSE)
