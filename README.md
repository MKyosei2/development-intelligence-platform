# Development Intelligence Platform

Development Intelligence Platform（DIP）は、GitHub、CI/CD、ゲームエンジン、DCCツール、テスト、ビルド、性能計測に分散した情報を統合し、PM・プロデューサー・テックリードの意思決定を支援する基盤です。


## GUI Phase 1: GitHub Repository Onboarding

GUIの最優先導線を「GitHub接続→Repository選択→DIP Project作成→Project Overview→Repository / CI / QA」に統一しました。多言語QAの追加拡張より先に、対象ProjectをGUIだけで選択・確認できる状態を固定するフェーズです。

- GitHub OAuth（ユーザー認証）とGitHub App（Repository権限）を分離
- GitHub App InstallationからRepository Pickerへ一覧表示
- Repository URL / default branchの手入力なしでDIP ProjectへImport
- `ProjectState`によるActive Project共通状態
- Project Overview / Repository / CI / QAのProject単位GUI
- SignalRによるRepository sync / QA lifecycleの更新通知＋手動Refresh fallback
- Loading / Error / Empty state共通Component
- PlaywrightによるGUI E2E

GitHub Appを使う場合は`.env`へ次を設定します。秘密鍵はブラウザへ送信されません。

```text
DIP_GITHUB_APP_ID=<app id>
DIP_GITHUB_APP_INSTALLATION_ID=<installation id>
DIP_GITHUB_APP_SLUG=<app slug>
DIP_GITHUB_APP_PRIVATE_KEY_BASE64=<base64 encoded PEM>
```

GUI Phase 1の実機E2E:

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-GuiPhase1.ps1"
```

詳細: [`docs/GUI_PHASE1_GITHUB_ONBOARDING_20260815.md`](docs/GUI_PHASE1_GITHUB_ONBOARDING_20260815.md)

## GUI Quick Start（Windows / Docker Desktop）

GUIを含む標準構成は、リポジトリ直下の`Start-DIP.cmd`から一括起動します。Docker Desktopを起動した状態でダブルクリックするか、PowerShell / cmdから実行してください。

```powershell
.\Start-DIP.cmd
```

起動スクリプトはPostgreSQL、Redis、Python Analysis、ASP.NET Core API、Worker、Blazor Webをbuildし、各health endpointが応答するまで待ってからブラウザを開きます。標準URLは次のとおりです。

- GUI: `http://127.0.0.1:5081`
- API / Swagger: `http://127.0.0.1:5080/swagger`
- Python Analysis: `http://127.0.0.1:8100/health`
- Worker metrics: `http://127.0.0.1:9091/metrics`
- GUI System Status: `http://127.0.0.1:5081/system`

標準起動はローカル開発用で認証を無効化します。本番相当のGitHub OAuthを確認する場合は、`DIP_GITHUB_OAUTH_CLIENT_ID`、`DIP_GITHUB_OAUTH_CLIENT_SECRET`、32文字以上の`DIP_JWT_SIGNING_KEY`を環境変数へ設定した上で次を実行します。

```powershell
.\Start-DIP.cmd -RequireAuthentication
```

GUIではPortfolioからProjectを作成でき、Project Truth、Project Intelligence、QA Governance、System Statusへ移動できます。Evidence artifactとManagement ExcelはWeb側の認証付きstreaming proxyを経由するため、Docker内部API URLやBearer tokenをブラウザへ直接露出しません。

### GUIを実際にE2E検証する

通常データを汚さずに「Docker build→全service起動→System Status→Portfolio→Project作成→Project切替→Overview→Repository→CI→QA→Intelligence→QA Governance」まで確認するisolated Playwright E2Eを用意しています。

```powershell
.\Verify-GUI.cmd
# または
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-GuiPhase1.ps1"
```

検証は一時Compose project / PostgreSQL volumeを使い、成功時・通常の失敗時とも検証用volumeを削除します。成功した場合だけ`artifacts/gui-phase1-e2e/GUI_PHASE1_E2E_PASS.txt`を生成します。旧来のHTTP中心smoke testは`scripts/Verify-GuiRuntime.ps1`として残しています。

停止:

```powershell
.\Stop-DIP.cmd
```

通常停止ではPostgreSQL / Redis / artifact volumeを保持します。完全初期化が必要な場合だけ`Stop-DIP.cmd -PurgeData`を使ってください。

## コア方針: Project Truth / Evidence First

DIPの中心はQAではありません。人間の自己申告だけに依存せず、Git、CI、Build、Test、Runtime、Performance、Unity、Maya、QA等から得られる客観的なEvidenceを集約し、報告（Claim）と照合します。

DIPは「誰が嘘をついたか」を評価しません。代わりに、報告内容を以下の状態として表示します。

- `Confirmed`: 客観的証拠が報告を支持
- `PartiallyConfirmed`: 一部のみ確認可能
- `Unverified`: 客観的証拠が不足
- `Contradicted`: 現在の客観的証拠と不一致
- `Stale`: 報告が古く、最新証拠が不足

PM向けDashboardでは技術ログを直接読む必要がないよう、Project Truth、Evidence-backed Progress、Reported Progress、Risk、Blocker、Report vs Reality、Evidence Timelineを先に表示し、Quality GateやMetric等の技術情報は詳細Evidenceとして下位に配置します。

## プロジェクト状態

**Project Truth P0、Technical Evidence P1、Decision Intelligence P2のソース経路を実装しています。ただし、最新コミットのCI、P1/P2 Windowsローカル検証、必要なLive E2Eが成功するまでは「検証完了」とは扱いません。**

完了条件は次のいずれかです。

1. 最新コミットで`.NET CI`、`Integration and E2E Tests`、Pythonテスト、Native Collector CIがすべて成功する。
2. Windowsで基盤検証とP2検証を実行し、必要なcompletion artifactが生成される。

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-Solution.ps1"
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-QaGovernance.ps1"
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-P2.ps1"
```

P2 Live E2Eを含める場合は、PostgreSQL / Redis / API / Workerを起動し、`DIP_RUN_LIVE_E2E=1`を設定して`Verify-P2.ps1`を実行します。

詳細は[`docs/IMPLEMENTATION_STATUS.md`](docs/IMPLEMENTATION_STATUS.md)を参照してください。

## 2026-08-11 Priority QA hardening

2026-08-14のS-priority更新でUniversal QA抽象化を実装しました。Python / C# / C++の深い専用QAを維持しつつ、`LanguageId`、Adapter Registry、Repository自動検出、structured diagnostic normalization、manifest adapterを追加しています。

- QA Bug Zoo: intentionally broken / clean-control projectsを実QA Engineで実行し、Detection Recall、False-positive Rate、Fingerprint Reproduction RateをJSON/Markdownで出力
- Unity Collector 0.2.0: Build Settings、Animation、Scene reference、runtime frame-time / managed-memory Evidenceを既存CollectorReportへ統合
- Unity EditMode/Runtime testsとBatchMode検証
- `Verify-Solution.ps1`、`Verify-P1.ps1`、`Verify-P2.ps1`、`Verify-PortfolioReadiness.ps1`によるfail-closed検証経路
- 既存GitHub webhook / Redis / Outbox / Worker / Quality Gate経路の依存補完

Bug Zoo:

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Invoke-QaBugZoo.ps1"
```

Unity Collector:

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-UnityCollector.ps1" -UnityPath "<Unity.exe>"
```

全優先項目をまとめて確認:

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-PortfolioReadiness.ps1" -UnityPath "<Unity.exe>"
```

`PORTFOLIO_READINESS_PASS.txt`はP0 Live E2E、P1 + Bug Zoo、P2、Unity実機検証の必要Evidenceがすべて存在する場合にのみ生成します。ソースの存在だけではPASSにしません。



## 2026-08-14 Universal QA / Formal Portfolio Evidence

- `QaLanguage enum`を廃止し、DB互換aliasを持つ拡張可能な`LanguageId`へ移行
- `IQaAdapter` / `IQaAdapterRegistry`で言語追加をDomain変更から分離
- Language / Build System / Framework / Unity・Unreal・Godotの自動検出
- Unity C#は`dotnet`決め打ちを避け、Unity Editor BatchMode/EditMode Testを使うengine-aware Adapterを優先
- C++はCMake時にASan/UBSan深度Adapter、非CMake時にMSBuild/cppcheck/compile databaseを使うfallback Adapterへ自動分岐
- SARIF / JSON / JUnit-like XMLを`QaFindingCandidate`へ正規化
- `.dip/qa.json`で未知言語・社内独自toolchainをshell連結なしに追加
- Bug ZooをPython/C#/C++に加えてJavaScript/Go/Java/Ruby/PHP/Bash/custom manifest languageへ拡張
- `Verify-PortfolioFormal.ps1`でWindows/.NET/Unity/Universal QA/ProjectControl/P2/Benchmarkをexact commitに束縛してfail-closed検証
- `Run-PortfolioBenchmarks.ps1`でProject list p95、Collector p95、100 req/s error rate、1M Metric query p95を実測しJSON/Markdown Evidenceを生成

Architecture: [`docs/02-architecture/universal-qa.md`](docs/02-architecture/universal-qa.md)  
Formal verification: [`docs/09-portfolio/runtime-verification.md`](docs/09-portfolio/runtime-verification.md)

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-UniversalQa.ps1"
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-PortfolioFormal.ps1" -UnityPath "<Unity.exe>"
```

**正式PASSやBenchmark数値は、そのrevisionを実機で走らせたEvidenceが存在する場合だけ表示します。**

## 2026-08-14 QA Governance / Reliability Expansion

Universal QAの「検出」結果を管理判断へ変換するGovernance層を追加しました。多言語Adapterの追加より先に、QAそのものの信頼性・安全性・追跡可能性を固定する層です。

- Flaky Test / Test Reliability: StablePass / StableFail / Flaky / EnvironmentFailureを履歴から判定
- Finding Lifecycle +期限付きWaiver: New→Confirmed→Assigned→InProgress→Fixed→Verified→Closedを管理し、Suppressed/AcceptedRisk/Duplicate/WontFixも記録
- Regression Baseline: baseline / Last Known Good / First Bad Commitを保存
- Ownership: DIP path ruleを優先し、未一致時は`CODEOWNERS`へfallback
- QA Policy / Release Gate: Critical・Warning・Flaky・QA infrastructure・wall/CPU/artifact budgetからGO / REVIEW REQUIRED / NO-GO
- Execution Sandbox: shell非経由、workspace/symlink境界、trusted tool allowlist、secret除去、timeout/output/memory/process-tree制御、Windows Job Object CPU/Memory制限
- Provenance / Freshness / Confidence: exact commit、環境・tool/version・policy/normalizer version・hashを固定し、古いEvidenceやQA基盤劣化時のconfidenceを低下
- QA Self-Health: Product FailureとQA Infrastructure Failureを分離
- Root-cause correlation / Technical Debt trend / Smart Test Selection / Diagnostic Schema Versioning / Artifact Retention
- QA Cost / Test Recommendation / safe argument minimization / Cross-project benchmark comparison

Management APIは`/api/v1/qa-governance`、管理職向けsummaryは既存Project Intelligence画面へ統合しています。詳細は[`docs/QUALITY_GOVERNANCE_20260814.md`](docs/QUALITY_GOVERNANCE_20260814.md)を参照してください。

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-QaGovernance.ps1"
```

**Governanceのソース実装と、そのrevisionのRuntime PASSは別です。正式PASSはexact commitで検証runnerが成功した場合だけ生成します。**

## 2026-08-12 Development Intelligence Expansion

Upgrade8を基準に、Durable Artifact / Job / Incident / Benchmark基盤の上へ次を既存レイヤーへ統合しています。

- Intelligence Dashboard
- deterministic Game Session Regression Replay（Unity Replay Protocolを含む）
- PC / Console / Mobile / VR Performance Budget
- Unreal Engine Collector
- real-device Hardware Backtesting / Calibration
- Change Risk Intelligence
- Automatic Regression Triage
- Release Readiness `GO / CONDITIONAL GO / NO-GO` gate
- explicit GitHub Issue work-item connector
- Release Provenance + CycloneDX SBOM bundle
- OpenTelemetry + Prometheus + Grafana observability
- Multi-repository Dependency Graph / cross-repository impact

新しい永続モデルは`202608120003_AddDevelopmentIntelligenceExpansion`で管理します。Game ReplayはDurable Job/Artifact/Evidence/Incidentへ、Performance BudgetはReplay/Benchmarkへ、Change RiskはAdaptive QAへ、Release ReadinessはIncident/Job/Budget/Evidence/Release Evidenceへ接続されています。

ソース実装とRuntime検証は分離します。このリポジトリを検証する場合は次を実行してください。

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-DevelopmentIntelligenceExpansion.ps1"
```

このスクリプトが生成するPASSは、実行したフェーズが実際に成功した場合だけ生成されます。Unity/Unreal/実機hardwareの証拠は対応環境で別途取得する必要があります。Universal QAは実装済みですが、各toolchainの実行Evidenceはそのtoolchainが存在する環境で取得します。

詳細: [`docs/DEVELOPMENT_INTELLIGENCE_EXPANSION_20260812.md`](docs/DEVELOPMENT_INTELLIGENCE_EXPANSION_20260812.md)

## P0の処理経路

```text
Collector / GitHub Webhook / Human Claim
  -> ASP.NET Core API
  -> PostgreSQL + Outbox / Redis
  -> Pipeline / Metric / Quality Gate
  -> Evidence Record
  -> Claim vs Evidence
  -> Project Truth Evaluator
  -> Progress / Risk / Blocker / Confidence
  -> Dashboard API
  -> PM-first Blazor Dashboard
```

Quality GateはProject Truthを構成するEvidence Providerの一つであり、DIP全体の最終判断そのものではありません。

## Project Truth P0

P0では次を実装しています。

- `EvidenceRecord`: GitHub / Collector / Quality / Performance等の客観的証拠
- `ProjectClaim`: 人間が報告した状態をEvidenceと分離して保存
- `ClaimEvidenceLink`: 報告を支持・矛盾・補足するEvidenceの関係
- `EvidenceConfidence`: Evidenceの信頼度を明示
- `ProgressEvaluator`: Reported ProgressとEvidence-backed Progressを分離
- `ProjectRiskEvaluator`: 失敗Evidence、Blocker、進捗差、未検証状態からRiskを算出
- `ProjectBlockerEvaluator`: PMが今確認すべき項目を抽出
- `ProjectTruthSnapshot`: PM向けの時点状態を保存
- `Report vs Reality`: ClaimとEvidenceの不一致を人物評価なしで可視化
- `Evidence Timeline`: 報告と客観的Evidenceを同じ時系列で確認

詳細は[`docs/02-architecture/project-truth-p0.md`](docs/02-architecture/project-truth-p0.md)を参照してください。


## P1: Technical Evidence Expansion

P1はQAをDIPの中心へ移すものではありません。P0のProject Truthをより強い客観Evidenceで裏付けるため、以下を追加します。

- Python専用Hardcoded QA: compileall / Ruff / mypy / pytest / runtime traceback / asyncio警告 / tracemallocメモリ増加
- C#専用Hardcoded QA: restore / build / test / analyzer / nullable・async警告 / runtime / hang時stack・dump
- C++専用Hardcoded QA: CMake / MSVC・clang warnings / CTest / clang-tidy / Cppcheck / ASan・UBSan・LeakSanitizer / native crash dump
- Stable Bug Fingerprintと3〜5回のAutomatic Reproduction
- Windows画面録画: DXGI Desktop Duplication + Media Foundation H.264 MP4
- システム音声: WASAPI Loopback WAV（マイクは自動録音しない）
- `environment.json` / stdout / stderr / dump / resultをEvidence Artifactとして保存
- Local Hardware Calibration
- 相対Target Hardware Profile
- Windows Job ObjectによるCPU / process memory制約
- メモリ帯域・ストレージのsynthetic contention
- D3D11 GPU contention helperとThermal Curve
- Hardware Simulation Evidence（実機証拠とは明確に区別）
- Minimum Hardwareの簡易Boundary Estimate
- 技術FindingをPM向け説明へ変換したうえでProject Truthへ入力

P1のPython / C# / C++専用深度は維持しながら、上位に`IQaAdapter` / `IQaAdapterRegistry`を追加しました。専用Adapterがない言語はbuilt-in generic adapterまたは`.dip/qa.json` manifest adapterで同じEvidence pipelineへ入ります。

詳細: [`docs/02-architecture/p1-technical-evidence.md`](docs/02-architecture/p1-technical-evidence.md)

Windowsローカル検証:

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-P1.ps1"
```

P1もソースが存在するだけでは検証完了と扱いません。特に録画、WASAPI、MiniDump、GPU制約はWindows実機Evidenceが必要です。

## P2: Decision Intelligence

P2は、P0のProject TruthとP1のTechnical Evidenceを使って、PM・制作進行が「なぜ今この状態なのか」「このまま進むと何が危ないか」「次に何を確認すべきか」を判断できるようにする層です。QAやAIがProject Truthを置き換えることはありません。

P2で追加する主な機能:

- opt-in Community Hardware DatabaseとHardware Profile Confidence
- Community Aggregate Hardware ProfileとPortability Analyzer
- CPU/GPU/Memory bandwidth/Storage/Memory budgetのMinimum Hardware Finder
- Technical MinimumとSafety-margin Recommended Minimumの分離
- change set + historical risk + costによるAdaptive QA Planner
- Flaky Test / Runtime Reliability / Soak / Performance Drift
- isolated Git worktreeによるRegression BisectとRegression Origin
- bounded deterministic Fuzzingとisolated Mutation Testing
- Test Impact Analysis（変更project参照 + path/symbol tokenで対象testを優先）
- Game Regression Gate（FPS/frame budget/CPU/GPU/memory/GC/draw/triangle/network latency/jitter/packet loss）
- QA Crash Cluster（stable fingerprint単位で発生回数・初回/最終発生・再現回数を集約）
- Native Symbolication（llvm-symbolizer / addr2lineでaddress→function/file/line）
- Fuzz failure corpus + unique fingerprint dedupe + bounded input minimization
- Mutation baseline gate + automatic common mutation generation + survivor source location
- Visual / Video / Audio Regression
- Evidence-grounded Root Cause、Similar Incident、human-review Suggested Fix
- Evidence Freshnessとauthority-weighted Conflict Resolution
- Historical Risk、Schedule Risk、Completion Forecast range、Stagnation、Dependency Risk
- What Changed Today、Why Health Changed、PM Decision Brief、Decision Trace

予測・Simulation・Community aggregateは測定実機Evidenceと区別します。AIはoptionalかつdefault OFFで、Evidence IDを持たない候補は破棄し、AI単独でBlockerやProject Truthを変更しません。

詳細: [`docs/02-architecture/p2-decision-intelligence.md`](docs/02-architecture/p2-decision-intelligence.md)

Windowsローカル検証:

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-P2.ps1"
```

## 構成

| プロジェクト | 役割 |
|---|---|
| `Dip.Domain` | Entity、Value Object、状態遷移、Evidence / Claim / Truth |
| `Dip.Shared` | API・Worker・Collector間のContract |
| `Dip.Application` | ユースケース、Project Truth判定、品質ルール、Repository Interface |
| `Dip.Infrastructure` | PostgreSQL、Redis、GitHub連携、Evidence永続化 |
| `Dip.Api` | HTTP API、Webhook、Collector Report、Claim受付 |
| `Dip.Worker` | Outbox配信、Quality Gate評価、再試行、Dead Letter |
| `Dip.Web` | PM-first Project Truth Dashboard |
| `services/python-analysis` | FastAPI分析サービス |
| `collectors/native-collector` | C++ Native Collector |
| `tests/Dip.Application.Tests` | Application Unit Test |
| `tests/Dip.Infrastructure.Tests` | Infrastructure Unit Test |
| `tests/Dip.IntegrationTests` | API / PostgreSQL Integration Test |
| `tests/Dip.EndToEndTests` | API・Redis・Workerを使うLive E2E Test |

## 必要環境

- .NET SDK 8.0
- Docker DesktopまたはDocker Engine + Compose V2
- PowerShell 5.1以上またはPowerShell 7
- Git
- CMake / C++20対応コンパイラ（Native Collectorを検証する場合）
- Python 3.11以上（分析サービスを検証する場合）

## セットアップ

```powershell
git clone <repository-url>
cd development-intelligence-platform
docker compose up -d
dotnet restore DevelopmentIntelligencePlatform.sln
dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
```

Unit Test：

```powershell
dotnet test tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj -c Release --no-build
dotnet test tests/Dip.Application.Tests/Dip.Application.Tests.csproj -c Release --no-build
dotnet test tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj -c Release --no-build
```

Integration Test：

```powershell
dotnet test tests/Dip.IntegrationTests/Dip.IntegrationTests.csproj -c Release --no-build
```

Live E2E TestはPostgreSQL、Redis、`Dip.Api`、`Dip.Worker`を起動した状態で実行します。

```powershell
$env:DIP_API_URL = "http://127.0.0.1:7001"
dotnet test tests/Dip.EndToEndTests/Dip.EndToEndTests.csproj -c Release --no-build
```

## アプリケーション起動

```powershell
dotnet run --project src/Dip.Api/Dip.Api.csproj
dotnet run --project src/Dip.Worker/Dip.Worker.csproj
dotnet run --project src/Dip.Web/Dip.Web.csproj
```

主要環境変数：

```text
ConnectionStrings__PostgreSql
Redis__ConnectionString
GitHub__WebhookSecret
GitHub__Token
Api__BaseUrl
```

## Quality Gate

品質判定は、Collectorが付与した明示的Severity、未解決Issue、Baseline差分を評価します。

- `fps`、`throughput`等は低下をRegressionとして扱う。
- `memory`、`latency`、`frame_time`、`size`等は増加をRegressionとして扱う。
- 10%以上をWarning、20%以上をCriticalとして扱う。
- Criticalが1件以上なら`Failed`、Warningが1件以上なら`Warning`、違反なしなら`Passed`。

Quality Gateの結果はEvidenceとしてProject Truthへ入力されます。

## GitHub Webhook

- Payload URL: `https://<host>/api/v1/webhooks/github`
- Content type: `application/json`
- Events: `push`、`pull_request`、`check_run`、`workflow_run`、`ping`
- HMAC SHA-256署名検証とDelivery IDによる冪等性を実装しています。
- 署名検証済みWebhookは`Verified` EvidenceとしてProject Truthへ利用します。

## ライセンス

本リポジトリはルートの`LICENSE`に従います。
## Project Control / Development Graph

DIP now includes a provider-neutral Development Graph that connects GitHub/Perforce changes, CI, Python/C++/C# QA, reviews, tests, features, acceptance evidence, assets, milestones and releases. The goal is not to score people; it is to give PMs, directors, planners, lead engineers and lead artists evidence-backed **Action Required** information.

Key flows include Change Impact / Blast Radius, Test Impact, Review Intelligence, Feature Readiness, Milestone Critical Path, Asset Intelligence, Cross-repository dependencies, Release Readiness, Decision Log/Brief and What-if simulation. GitHub webhooks project push/PR/review/workflow/check/release/deployment events into the graph; optional Perforce and Jenkins adapters use the same normalized models.

The API surface is documented in [`docs/PROJECT_CONTROL_IMPLEMENTATION_20260812.md`](docs/PROJECT_CONTROL_IMPLEMENTATION_20260812.md). Multi-language QA now uses the Universal QA adapter layer while preserving deep Python/C++/C# adapters.



## Management Intelligence (PM / Director / Lead)

DIP is not centered on QA. QA, Git, CI, runtime and asset data are evidence providers for a management layer that explains project state without requiring implementation-level expertise. The management layer covers Team/Workstream capacity, tasks, dependency-aware schedule and Gantt data, critical path, blockers, milestones, requirements/scope, risk register, decision/action tracking, resource-allocation options, scope-change impact and management escalation.

The dashboard presents management health before supporting technical evidence. It exposes a plain-language management summary, primary concern, pending decisions/actions, risk exposure and escalation feed. Excel `.xlsx` export/import and Google Sheets bidirectional synchronization include editable management sheets for risks, decisions, actions, change requests and resource allocations as well as calculated project-health/escalation sheets. See [`docs/MANAGEMENT_INTELLIGENCE.md`](docs/MANAGEMENT_INTELLIGENCE.md).

## QA Governance hardening (2026-08-14)

The QA control plane now includes project-scoped membership authorization, server-derived audit actors, exactly-once/concurrency controls, formal Waiver and Policy lifecycles, commit-bound Release Gate Overrides, dedicated Governance observability, operational backup/restore, DTO/keyset-pagination boundaries, load/concurrency verification and an expanded management UI. See [`docs/02-architecture/qa-governance-hardening.md`](docs/02-architecture/qa-governance-hardening.md).

Formal proof remains fail-closed: `scripts/Verify-PortfolioFormal.ps1` only writes PASS for the exact clean Git revision after Windows/.NET/Docker/Unity, database migration/rollback, DR, load/concurrency and benchmark checks succeed.


### Existing project authorization bootstrap

After upgrading an installation that already has projects, use an authenticated Admin with `scripts/Bootstrap-ProjectMemberships.ps1` to grant explicit Owner memberships. The migration does not infer ownership automatically; grants are written through the audited membership API.
