# Development Intelligence Platform

**Development Intelligence Platform（DIP）** は、分散している **Git / CI / QA / Runtime / Performance / Project data** を、追跡可能なEvidenceとして統合する、ブラウザ中心の開発・プロジェクトインテリジェンス基盤です。

ソフトウェア開発、ゲーム開発、映像制作など、複数の制作・開発領域を対象としています。

[![.NET CI](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/dotnet-ci.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/dotnet-ci.yml)
[![Integration and E2E](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/integration-test.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/integration-test.yml)
[![Security Scan](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/security-scan.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/security-scan.yml)

<p align="center">
  <img src="docs/09-portfolio/demo/dip-portfolio-demo.gif" alt="DIP portfolio demo using sample data" width="920" />
</p>

> 上記GIFおよびリポジトリ内のスクリーンショットでは、決定論的なサンプルデータを使用しています。これらは製品・GUIのデモンストレーションであり、最新コミットが実行環境上で検証済みであることを示す証拠ではありません。コミット単位の検証Evidenceは [`docs/09-portfolio/evidence/`](docs/09-portfolio/evidence/) に分離して管理しています。

---

# 概要

DIPは単なるプロジェクト管理ダッシュボードではなく、

**Evidence + Automation + Project Intelligence Platform**

として設計しています。

基本的な考え方は以下です。

```text
Development activity
        ↓
Evidence
        ↓
Project / Development Graph
        ↓
Deterministic evaluation
        ↓
Risk / Progress / QA / Debug hypotheses
        ↓
Action
        ↓
Verification
```

通常は別々のツールに分散している開発・制作情報を、同じProject Context上で扱えるようにします。

例えば、以下のような問いに答えることを目的としています。

* 現在の進捗のうち、実際にEvidenceによって裏付けられているのはどこか
* 「完了」と報告されているが、独立したEvidenceによって確認されていない項目は何か
* 現在のRevisionはRelease可能な状態か
* Regressionの原因は製品側なのか、QA環境なのか、Infrastructureなのか
* どの変更がFailureと最も強く関連しているか
* AIが提案した修正は、隔離された検証環境でも本当に成立するか
* Spreadsheetや外部制作管理システムの情報を、特定業界にCoreを依存させず統合できるか

---

# 技術的に難しいポイント

DIPは、単なるPM Dashboardではなく、**開発プラットフォームエンジニアリングのプロジェクト**として設計しています。

主な技術課題は以下です。

## 1. 異種EvidenceをProvenanceを失わず正規化する

DIPは、以下のような異なる情報源を扱います。

* GitHub
* CI
* Build system
* Unit / Integration / E2E Test
* Unity
* Unreal Engine
* Maya
* Native application
* Runtime telemetry
* Performance measurement
* Project-management system
* Spreadsheet
* External production tool

これらをProvider依存ではないEvidenceとして正規化しつつ、以下の情報を保持します。

* Source
* Revision
* Timestamp
* Confidence
* Project
* Verification state
* External identity
* Provenance

単純にデータ形式を統一するのではなく、

**「どこから来た情報なのか」**

を失わないことを重要視しています。

---

## 2. Durable TruthとDelivery / Analysisを分離する

Runtime architectureでは、責務を明確に分離しています。

```text
PostgreSQL
    ↓
Durable project truth

Redis
    ↓
Coordination / delivery

Dip.Worker
    ↓
Asynchronous jobs

Python Analysis
    ↓
Analysis / computation

Dip.Api
    ↓
Application/API boundary

Dip.Web
    ↓
Browser GUI
```

PostgreSQLをDurable Source of Truthとし、RedisやWorker、Analysis ServiceのFailureによってProject Stateそのものが勝手に変更されないようにします。

---

## 3. Revisionをまたいで信頼状態を継承しない

あるSource Fileが存在していることと、そのRevisionが検証済みであることは別です。

例えば、

```text
Commit A
  └─ tests PASS

Commit B
  └─ source exists
```

だからといって、

```text
Commit B
  └─ tests PASS
```

とは判断しません。

過去のPASS Evidenceは履歴として利用できますが、新しいRevisionは新しいRevisionとして検証を行う必要があります。

---

## 4. AIの出力をTruthとして扱わない

DIPでは、AIが修正案を生成しただけではVerifiedとはしません。

基本フローは以下です。

```text
Evidence
   ↓
Hypothesis
   ↓
Experiment
   ↓
Candidate Fix
   ↓
Isolated workspace
   ↓
Build / QA / Regression verification
   ↓
Human review
```

基本原則は、

> AIは提案する。Evidenceが検証する。

です。

---

# 主な機能

## Project Truth / Evidence Graph

DIPでは、ClaimとEvidenceを明確に分離してProject Stateを評価します。

代表的なVerification Stateは以下です。

```text
Confirmed
PartiallyConfirmed
Unverified
Contradicted
Stale
```

Project Stateは、例えば以下のように接続されます。

```text
Claim
  ↓
Evidence
  ↓
Risk
  ↓
Blocker
  ↓
Action
  ↓
Verification
```

扱うEvidenceには以下があります。

* Git
* GitHub
* CI
* Build
* Test
* Runtime
* Performance
* Unity
* Unreal
* Maya
* Spreadsheet
* Approval
* Inspection
* Production Evidence

Project Evaluationに使用するThresholdやPenaltyは、各EvaluatorにMagic Numberとして分散させず、明示的にVersion管理可能な `ProjectTruthPolicy` として管理しています。

Default Policyは既存挙動を維持する目的のものであり、統計的に最適であることを主張するものではありません。

詳細：

[`docs/09-portfolio/PROJECT_TRUTH_POLICY.md`](docs/09-portfolio/PROJECT_TRUTH_POLICY.md)

---

# Universal Project / Development Graph

DIPでは、特定業界ごとにCoreを作り直さないため、一般化されたGraph Modelを利用します。

概念的には以下です。

```text
Universal Evidence Model
        ↓
Universal Project Graph
        ↓
Domain Adapter / Domain Pack
        ↓
Intelligence Engine
```

Domain固有情報は、例えば以下のMetadataを利用して表現できます。

```text
domainId
kind
sourceObjectId
relationKind
schemaVersion
```

このため、Core Enumを大量に追加しなくてもSoftware Developmentだけでなく、制作・工程管理情報を扱えるようにしています。

例：

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

---

# Game / Software QA・Defect Intelligence

DIPには、専用QAとCapability-based QAの両方を実装しています。

対象または拡張可能な技術領域：

* C#
* C++
* Python
* JavaScript
* TypeScript
* Go
* Rust
* Java
* Kotlin
* Ruby
* PHP
* Shell
* Lua
* HLSL
* GLSL
* Custom toolchain
* Unity
* Unreal Engine
* Native Runtime

QA Systemでは以下のような概念を扱います。

* Stable Finding Fingerprint
* Reproduction
* Regression Origin
* Defect Evidence
* Isolated Verification
* Regression Replay
* Release Gate
* QA Governance
* Runtime Diagnostics

---

# Evidence-Grounded Autonomous Debugging

Autonomous Debuggingは、無制限にSourceを変更するAI Agentではなく、**EvidenceとBounded Experimentを中心にした検証システム**として設計しています。

```text
Failure Evidence
      ↓
Root-cause hypotheses
      ↓
Evidence validation
      ↓
Small candidate patch
      ↓
Regression test
      ↓
Disposable verification workspace
      ↓
QA rerun
      ↓
Verified / Not Verified
```

主な方針：

* AI自身が自分のPatchをVerifiedとは判定できない
* Evidence IDは実在するものを要求する
* Experiment回数に上限を設ける
* Test無効化による見かけ上のPASSを拒否する
* Diagnostics Suppressionを目的としたPatchを拒否できる
* Source変更はWorking Treeとは別のIsolation環境で検証する
* Verified Candidateであっても最終Human Reviewを要求する

---

# Spreadsheet / Project Management Intelligence

DIPでは、Source Code以外から来るProject Informationも扱います。

Spreadsheetを単なるExport形式としてではなく、

**Project WorkItem / Evidence Provider**

として扱います。

代表的に推定・認識可能なField：

```text
Key
External ID
Title
Description
Owner
Status
Progress
Start Date
Due Date
Priority
Dependency
Milestone
Cost
Risk
Approval
```

日本語・英語の管理表を同じProject Graphへ正規化することを想定しています。

---

# Management Operations

Project Management系では以下の概念を扱います。

* Daily Cockpit
* Risk
* Decision
* Action
* Approval
* RACI
* Resource Capacity
* Meeting Mode
* Milestone
* Project Progress
* Notification
* Audit History

Integration Layerとして以下を想定・実装しています。

* GitHub
* Jira
* Linear
* Slack
* Teams
* Google Sheets
* Calendar
* Redmine
* WBS

基本的な運用フロー：

```text
Detect
  ↓
Decide
  ↓
Assign
  ↓
Approve
  ↓
Act
  ↓
Verify
  ↓
Close
  ↓
Review
```

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
  ├── PostgreSQL
  │     └── Durable Source of Truth
  │
  ├── Redis
  │     └── Coordination / Delivery
  │
  ├── Dip.Worker
  │     └── Async Job / Ingestion
  │
  └── Python Analysis
        └── Analysis / Computation

External Sources
  ├── GitHub / CI
  ├── Unity / Unreal
  ├── Maya
  ├── Native / Runtime
  ├── Spreadsheet
  └── Project-management tools
```

詳細：

[`docs/09-portfolio/ARCHITECTURE_OVERVIEW.md`](docs/09-portfolio/ARCHITECTURE_OVERVIEW.md)

---

# 使用技術

| 分野              | 技術                                   |
| --------------- | ------------------------------------ |
| Backend         | .NET 8 / C# / ASP.NET Core           |
| Web             | Blazor                               |
| Database        | PostgreSQL                           |
| Coordination    | Redis                                |
| Worker          | .NET Worker                          |
| Analysis        | Python                               |
| Native / Engine | C++ / CMake / Unity / Unreal / Maya  |
| Container       | Docker / Docker Compose              |
| CI              | GitHub Actions                       |
| Observability   | OpenTelemetry / Prometheus / Grafana |
| UI E2E          | Playwright / Chromium                |
| Automation      | PowerShell / Python                  |

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
  Unity / Unreal / Maya / Native / Runtime collectors

services/
  Python analysis service

tests/
  Dip.Application.Tests/
  Dip.Domain.Tests/
  Dip.Infrastructure.Tests/
  Dip.IntegrationTests/
  Dip.EndToEndTests/
  Dip.UiE2E/
  load/

tools/
  QA / benchmark / evidence / publication tooling

scripts/
  Windows verification and automation

docs/
  Architecture
  Implementation status
  Portfolio evidence
  Reviewer documentation
```

---

# 必要環境

Windowsでの推奨開発環境：

* Windows 10 / 11
* Git
* .NET SDK 8.x
* Docker Desktop
* Docker Compose v2
* PowerShell
* Node.js / npm

  * Browser E2Eを実行する場合に必要

確認：

```powershell
git --version
dotnet --version
docker version
docker compose version
node --version
npm --version
```

.NET SDKは `8.x` を使用してください。

---

# Clone

```powershell
git clone https://github.com/MKyosei2/development-intelligence-platform.git
cd development-intelligence-platform
```

---

# Dockerを使用しないBuild確認

DockerやGUIの問題を調査する前に、まずSourceそのものがBuildできることを確認します。

```powershell
dotnet restore .\DevelopmentIntelligencePlatform.sln

dotnet build `
  .\DevelopmentIntelligencePlatform.sln `
  -c Release `
  --no-restore
```

正常時：

```text
Build succeeded.
0 Error(s)
```

Analyzer Warningが存在する場合がありますが、Compile ErrorはGUI確認へ進む前に0にしてください。

---

# DockerでDIPを起動する

## 1. Docker Desktopを起動する

`docker compose` 実行前にDocker Desktopが起動している必要があります。

確認：

```powershell
docker info
```

例えば以下のようなエラーが出た場合：

```text
dockerDesktopLinuxEngine
The system cannot find the file specified
```

Docker Desktopを起動してください。

---

# 2. Environment設定

通常のLocal Developmentでは以下のPortを使用します。

| Service         | Default Port |
| --------------- | -----------: |
| PostgreSQL      |         5432 |
| Redis           |         6379 |
| Python Analysis |         8100 |
| API             |         5080 |
| Web GUI         |         5081 |
| Worker Metrics  |         9091 |

必要に応じて：

```powershell
Copy-Item .env.example .env
```

External Integrationを使用する場合のみ必要な設定を追加してください。

SecretはSource ControlへCommitしないでください。

---

# 3. Stackを起動する

```powershell
docker compose up -d --build
```

GUI確認に必要なServiceだけ起動する場合：

```powershell
docker compose up -d --build `
  postgres `
  redis `
  python-analysis `
  api `
  worker `
  web
```

初回のAPI Image Buildでは、多言語QA環境用のCompiler / AnalyzerをInstallするため、かなり大きなDocker Buildになる場合があります。

2回目以降は通常Docker Layer Cacheが利用されます。

---

# 4. Service状態を確認する

```powershell
docker compose ps
```

重要なService：

```text
postgres
redis
python-analysis
api
worker
web
```

これらがRunning / Healthyになることを確認してください。

---

# GUIを開く

Default：

```text
http://127.0.0.1:5081
```

GitHub Import：

```text
http://127.0.0.1:5081/projects/import/github
```

API Health：

```text
http://127.0.0.1:5080/health
```

Python Analysis：

```text
http://127.0.0.1:8100/health
```

Worker Metrics：

```text
http://127.0.0.1:9091/metrics
```

---

# 推奨GUI動作確認

単にTop Pageが開くだけではなく、主要画面を操作してください。

推奨Smoke Test：

```text
Portfolio
   ↓
Create Project
   ↓
Project Overview
   ↓
Repository
   ↓
CI
   ↓
QA
   ↓
Development Intelligence
   ↓
QA Governance
   ↓
Settings
   ↓
Archive / Restore
   ↓
Audit
   ↓
Notifications
```

確認内容：

1. Portfolioを表示できる
2. Manual Projectを作成できる
3. Project Dashboardへ遷移できる
4. Repository画面を開ける
5. CI画面を開ける
6. QA画面を開ける
7. Development Intelligenceを開ける
8. QA Governanceを開ける
9. Settingsを開ける
10. Project名を変更して保存できる
11. ProjectをArchiveできる
12. Restoreできる
13. AuditにLifecycle Eventが表示される
14. Notificationsを開ける
15. BrowserをRefreshする
16. Project Dataが保持されている

これにより、単純なHTTP Health Check以上のRuntime Verificationが可能です。

---

# PlaywrightによるGUI自動確認

DIPにはPlaywrightによるBrowser E2E Testがあります。

通常のDeterministic TestではGitHub Credentialは必要ありません。

```powershell
cd .\tests\Dip.UiE2E

npm install

npx playwright install chromium

$env:DIP_WEB_BASE_URL = "http://127.0.0.1:5081"

npm test
```

Chromiumが実際に動く様子を確認する場合：

```powershell
npm run test:headed
```

Headed GUI Testでは、代表的に以下を確認します。

* Portfolio表示
* Active Project Selector
* GitHub Import画面
* Manual Project作成
* Project Overview
* Repository
* CI
* QA
* Development Intelligence
* QA Governance
* Project Settings
* Archive / Restore
* Owner Safety
* Audit History
* Notification Drawer

---

# Live GitHub GUI Test

Live GitHub確認は通常のDeterministic GUI Testとは分離しています。

GitHub App / Tokenから参照可能なRepositoryを指定します。

```powershell
$env:DIP_E2E_GITHUB_REPOSITORY = "owner/repository"
```

実行：

```powershell
cd .\tests\Dip.UiE2E
npm run test:live-github
```

External Credentialが存在しないことによって通常CIがFlakyにならないよう、Live Testは分離しています。

---

# WindowsでのPort競合

Windowsでは、他Processが使用していないように見えるPortでもDockerがBindできない場合があります。

例：

```text
ports are not available

listen tcp 0.0.0.0:55432:
bind: An attempt was made to access a socket
in a way forbidden by its access permissions.
```

原因例：

* 他Processが使用中
* WindowsのReserved Port
* Excluded Port Range
* Docker Desktopから利用不可

この場合、別の固定Portへ適当に変更し続けるのではなく、Host Portを変更します。

例：

```powershell
$env:POSTGRES_PORT = "55245"
$env:REDIS_PORT = "55246"
$env:ANALYSIS_PORT = "55247"
$env:API_PORT = "55248"
$env:WORKER_METRICS_PORT = "55249"
$env:WEB_PORT = "55252"

docker compose up -d `
  postgres `
  redis `
  python-analysis `
  api `
  worker `
  web
```

この場合GUIは、

```text
http://127.0.0.1:55252
```

になります。

Playwrightも同じURLを使用します。

```powershell
$env:DIP_WEB_BASE_URL = "http://127.0.0.1:55252"
```

---

# APIがUnhealthyになった場合

以下のような状態の場合：

```text
postgres          Healthy
redis             Healthy
python-analysis   Healthy
api               Unhealthy
```

Web GUIから調査を始めないでください。

まずAPIを確認します。

```powershell
docker compose ps api
```

次に：

```powershell
docker compose logs --tail 400 api
```

APIではHTTP Server起動前にPostgreSQL Schema Initialization / EF Migrationが実行されるため、Database / Migration Errorによって `/health` が利用可能になる前に停止する場合があります。

その場合はAPI LogをRoot Causeとして扱ってください。

---

# Isolated GUI Verification

通常のPersistent StackでAPI / Database Problemが発生した場合、既存Databaseを削除せず、

**別Compose Project**

で検証できます。

これにより別のContainer / Network / Named Volumeが作成されます。

例：

```powershell
$env:POSTGRES_PORT = "55245"
$env:REDIS_PORT = "55246"
$env:ANALYSIS_PORT = "55247"
$env:API_PORT = "55248"
$env:WORKER_METRICS_PORT = "55249"
$env:WEB_PORT = "55252"

docker compose `
  -p dipguitest `
  up `
  -d `
  --build `
  postgres `
  redis `
  python-analysis `
  api `
  worker `
  web
```

確認：

```powershell
docker compose -p dipguitest ps
```

API Log：

```powershell
docker compose -p dipguitest logs --tail 400 api
```

これによって、

```text
Source / Configuration problem
```

なのか、

```text
既存Persistent Database problem
```

なのかを切り分けることができます。

Isolated Stackでは正常に動作し、通常Stackだけ失敗する場合は、Web GUIではなく既存Database / Migration Stateを確認してください。

---

# Persistent Dataを誤って削除しないための注意

以下はContainerを停止しますがNamed Volumeは保持します。

```powershell
docker compose down
```

これもVolumeは保持します。

```powershell
docker compose down --remove-orphans
```

以下には注意してください。

```powershell
docker compose down -v
```

`-v` を付けるとCompose管理のNamed Volumeを削除します。

意図的なLocal Reset以外では使用しないでください。

Isolated Testでは例えば、

```text
dipguitest
```

のような専用Project Nameを使用すると安全です。

---

# Docker Troubleshooting

## Docker Engineが起動していない

症状：

```text
failed to connect to the docker API
dockerDesktopLinuxEngine
```

対応：

1. Docker Desktopを起動
2. Docker EngineがRunningになるまで待つ
3. 以下を実行

```powershell
docker info
```

---

## Port Bind Failure

症状：

```text
ports are not available

bind:
An attempt was made to access a socket
in a way forbidden by its access permissions.
```

変更可能なEnvironment Variable：

```text
POSTGRES_PORT
REDIS_PORT
ANALYSIS_PORT
API_PORT
WORKER_METRICS_PORT
WEB_PORT
```

---

## APIがUnhealthy

```powershell
docker compose ps
docker compose logs --tail 400 api
```

```text
container api is unhealthy
```

という表示自体をRoot Causeとして扱わず、API内部Exceptionを確認してください。

---

## Worker / Webが起動しない

Worker / WebはAPIに依存しています。

先にAPIを確認します。

```powershell
docker compose ps api
docker compose logs api
```

---

## GUIは開くが操作に失敗する

まず、

```powershell
docker compose ps
```

で以下を確認します。

```text
postgres
redis
api
worker
web
```

さらに、

```text
API /health
Worker /metrics
```

も確認します。

Top Pageが表示されたことだけでは、Backend Stack全体が正常である証明にはなりません。

---

# DIPを停止する

通常Stack：

```powershell
docker compose down
```

Isolated Stack：

```powershell
docker compose -p dipguitest down
```

Persistent Dataを残す場合は `-v` を付けないでください。

---

# 既存Docker Imageを再利用する

Docker Image Buildが完了しており、Container Startupだけ失敗した場合は、毎回巨大なImageをBuildし直す必要はありません。

通常：

```powershell
docker compose up -d
```

SourceやDocker Build Inputを変更した場合のみ：

```powershell
docker compose up -d --build
```

---

# Core Verification

Repository RootからFull Solution Verificationを実行します。

```powershell
powershell `
  -ExecutionPolicy Bypass `
  -File .\scripts\Verify-Solution.ps1
```

その他：

```powershell
powershell `
  -ExecutionPolicy Bypass `
  -File .\scripts\Verify-ProjectControl.ps1
```

```powershell
powershell `
  -ExecutionPolicy Bypass `
  -File .\scripts\Verify-P2.ps1
```

```powershell
powershell `
  -ExecutionPolicy Bypass `
  -File .\scripts\Verify-UniversalQa.ps1
```

```powershell
powershell `
  -ExecutionPolicy Bypass `
  -File .\scripts\Verify-DatabaseMigrations.ps1
```

```powershell
powershell `
  -ExecutionPolicy Bypass `
  -File .\scripts\Verify-GuiRuntime.ps1
```

```powershell
powershell `
  -ExecutionPolicy Bypass `
  -File .\scripts\Verify-PortfolioFormal.ps1 `
  -UnityPath "<Unity.exe>"
```

---

# Targeted Test

Application：

```powershell
dotnet test `
  .\tests\Dip.Application.Tests\Dip.Application.Tests.csproj `
  -c Release
```

Domain：

```powershell
dotnet test `
  .\tests\Dip.Domain.Tests\Dip.Domain.Tests.csproj `
  -c Release
```

Infrastructure：

```powershell
dotnet test `
  .\tests\Dip.Infrastructure.Tests\Dip.Infrastructure.Tests.csproj `
  -c Release
```

Integration：

```powershell
dotnet test `
  .\tests\Dip.IntegrationTests\Dip.IntegrationTests.csproj `
  -c Release
```

End-to-End：

```powershell
dotnet test `
  .\tests\Dip.EndToEndTests\Dip.EndToEndTests.csproj `
  -c Release
```

---

# 推奨Verification順序

大きな変更を行った場合、Compile Errorが残っている状態で毎回Full Verificationを行うのは非効率です。

推奨順：

```text
1. dotnet restore

2. dotnet build -c Release
      ↓
   Compile Error = 0

3. 変更対象のTargeted Test

4. Application / Domain / Infrastructure Test

5. Integration Test

6. Docker Runtime

7. API / Worker / Web Health

8. Playwright GUI E2E

9. Verify-Solution.ps1

10. Publication / Portfolio Verification
```

これにより、最初に発生した本当のFailureを特定しやすくなります。

---

# Verification Philosophy

DIPでは以下を明確に区別します。

## Implemented

Source Codeが存在する。

## Tested

特定Revisionに対してTestが実行され、PASSした。

## Runtime Verified

想定Runtime Environment上で実際のApplication / Serviceが実行され、Evidenceが生成された。

これらは同じではありません。

例えば、

```text
source exists
```

だからといって、

```text
runtime verified
```

ではありません。

また、

```text
historical revision passed
```

だからといって、

```text
latest revision passed
```

でもありません。

---

# Portfolio Evidence

DIPでは意図的に以下を分離しています。

* Implemented Source
* Historical Verified Evidence
* Exact Revision Verification

関連資料：

* Historical verified baseline
  [`docs/09-portfolio/evidence/verified-baseline/`](docs/09-portfolio/evidence/verified-baseline/)

* Latest revision status
  [`docs/09-portfolio/evidence/latest-revision.md`](docs/09-portfolio/evidence/latest-revision.md)

* Runtime verification policy
  [`docs/09-portfolio/runtime-verification.md`](docs/09-portfolio/runtime-verification.md)

* Implementation / Runtime distinction
  [`docs/IMPLEMENTATION_STATUS.md`](docs/IMPLEMENTATION_STATUS.md)

代表的なHistorical Verified Baseline：

| Evidence                         |             Measured result |
| -------------------------------- | --------------------------: |
| Project list p95                 |                **4.174 ms** |
| Collector p95                    |               **34.507 ms** |
| 100 req/s error rate             | **0.0000%**（1,500 requests） |
| 1M Metric query p95              |              **363.796 ms** |
| QA Bug Zoo recall                |          **19 / 19 = 100%** |
| QA clean-control false positives |              **0 / 7 = 0%** |
| QA reproducibility               |            **9 / 9 = 100%** |

これらの測定値は、Evidenceに記録されている特定Revisionに対するものです。

新しいRevisionへ自動的に引き継がれるものではありません。

---

# Screenshots

| Project Intelligence                                        | QA / Defect Intelligence                         | Management Operations                                        |
| ----------------------------------------------------------- | ------------------------------------------------ | ------------------------------------------------------------ |
| ![](docs/09-portfolio/screenshots/project-intelligence.png) | ![](docs/09-portfolio/screenshots/qa-defect.png) | ![](docs/09-portfolio/screenshots/management-operations.png) |

---

# Scope / Non-Goals

DIPの対象範囲が広いのは、Product Boundaryを単一アプリケーションではなく、Development / Production Pipeline全体としているためです。

ただし、実装範囲の広さ自体を完成度の証拠とは扱いません。

以下の方針を採用しています。

* Source PresenceをRuntime PASSとして扱わない
* External Integrationは実際にSandbox / Live VerificationするまでVerifiedと呼ばない
* AI / Fix Agentの出力はIsolated Verificationが完了するまでAdvisoryとする
* Project TruthはEmployee Truthfulness Scoreではない
* Project TruthはML Prediction Systemではない
* Engine / Device依存のClaimは実機Runner実行までUnverifiedとする
* Historical Evidenceを新しいCommitへ自動継承しない
* QA Infrastructure FailureをProduct Failureとして自動分類しない

---

# Publication Verification

公開前：

```powershell
python tools/verify_publication_safety.py --root .
```

```powershell
python tools/validate_portfolio_readiness.py --root .
```

Source FileやRunnerが存在するだけではCompleteとは判断しません。

Publication / Release要件：

[`docs/09-portfolio/PUBLICATION_CHECKLIST.md`](docs/09-portfolio/PUBLICATION_CHECKLIST.md)

---

# Hosted Deployment

通常の `docker-compose.yml` はLocal Development向けです。

Browser-only / Hosted Deploymentでは以下を使用します。

```text
deploy/
docker-compose.web.yml
```

Production SecretはSource Control外で管理してください。

---

# Reviewer向け資料

* Reviewer brief
  [`docs/09-portfolio/reviewer-brief.md`](docs/09-portfolio/reviewer-brief.md)

* Engineering case study
  [`docs/09-portfolio/ENGINEERING_CASE_STUDY.md`](docs/09-portfolio/ENGINEERING_CASE_STUDY.md)

* Architecture overview
  [`docs/09-portfolio/ARCHITECTURE_OVERVIEW.md`](docs/09-portfolio/ARCHITECTURE_OVERVIEW.md)

* Project Truth policy
  [`docs/09-portfolio/PROJECT_TRUTH_POLICY.md`](docs/09-portfolio/PROJECT_TRUTH_POLICY.md)

* Runtime verification
  [`docs/09-portfolio/runtime-verification.md`](docs/09-portfolio/runtime-verification.md)

* Implementation status
  [`docs/IMPLEMENTATION_STATUS.md`](docs/IMPLEMENTATION_STATUS.md)

* Security policy
  [`SECURITY.md`](SECURITY.md)

* Contribution guide
  [`CONTRIBUTING.md`](CONTRIBUTING.md)

---

# License

MIT — 詳細は [`LICENSE`](LICENSE) を参照してください。
