# Development Intelligence Platform

## 1. 文書の目的

このREADMEは、ゲーム・映像・IT開発向けのプロジェクト意思決定支援基盤  
**Development Intelligence Platform（DIP）** の実装開始時点における、リポジトリ全体のコード設計書です。

本ZIPでは、ルートの `README.md` 以外のファイルはすべて空です。  
このREADMEを基準として、各層の責務、依存方向、主要データフロー、API境界、品質ルール、エラー処理、セキュリティ、試験を順番に実装します。

---

## 2. システムの目的

DIPは、GitHub、CI/CD、ゲームエンジン、DCCツール、タスク、ビルド、テスト、性能計測、アセット解析などに分散する情報を収集し、PM・プロデューサー・テックリードが次の問いへ答えられる状態を作ります。

- 現在のプロジェクトは安全か
- どのPull Requestが危険か
- 品質がどのCommitから悪化したか
- リリース予定日に間に合う可能性はどの程度か
- どの担当者やレビュー工程がボトルネックか
- 何を最優先で対応すべきか
- 判断の根拠となるMetric、Issue、Build、Testは何か

DIPはJiraやGitHubの代替ではありません。既存ツールを置き換えるのではなく、複数の情報を統合し、品質判定と意思決定支援を行う上位レイヤーです。

---

## 3. 技術スタック

### 3.1 C# / .NET

- ASP.NET Core Web API
- Blazor Server
- .NET BackgroundService
- Entity Framework Core
- PostgreSQL
- Redis
- SignalR
- OpenTelemetry

C#は、ドメイン、ユースケース、API、Dashboard、Worker、GitHub連携、品質ゲートを担当します。

### 3.2 Python

- FastAPI
- pandas / NumPy
- scikit-learn
- Pydantic
- pytest
- 必要に応じた生成AI APIまたはローカルモデル

Pythonは、異常検知、リスク分析、ログ分類、日次要約、リリース予測、Maya Collectorを担当します。

### 3.3 C++

- C++20
- CMake
- Native CLI
- 大量ファイル・ログの高速走査
- 重複検出、Hash計算、Unreal向け拡張

C++は、性能上の理由が明確なCollector処理だけを担当します。サーバー業務ロジックは実装しません。

---

## 4. アーキテクチャ

```text
GitHub / CI / Unity / Maya / Unreal / Generic JSON
                         |
                         v
                   Collector Layer
             C# / Python / C++ Collectors
                         |
                         v
                   ASP.NET Core API
                         |
                  validation / auth
                         |
                         v
                  Redis Job Queue
                         |
          +--------------+--------------+
          |              |              |
          v              v              v
   GitHub Worker   Quality Worker   Analysis Worker
          |              |              |
          +--------------+--------------+
                         |
                         v
                    PostgreSQL
                         |
                  MinIO / Local Storage
                         |
                         v
                    Blazor Dashboard
```

---

## 5. 依存方向

このリポジトリは、Clean Architectureを基準にします。

```text
Dip.Domain
    ↑
Dip.Application
    ↑
Dip.Infrastructure
    ↑
Dip.Api / Dip.Worker

Dip.Shared ← API / Web / Worker / Collector
Dip.Web → HTTP APIのみを利用
Python Analysis Service → 独立HTTPサービス
Collectors → Shared ContractまたはJSON Schemaに依存
```

### 5.1 禁止する依存

- `Dip.Domain` からEF Core、HTTP、Redis、GitHub SDKへ依存しない
- `Dip.Application` から具体的なDB、HTTP Client、Storage SDKへ依存しない
- `Dip.Web` から `Dip.Infrastructure` のRepositoryを直接利用しない
- Python ServiceからC#のDB Tableへ直接書き込まない
- CollectorからPostgreSQLへ直接接続しない
- Controller内にHealth Score計算やQuality Gate判定を書かない

---

## 6. 各プロジェクトの責務

### 6.1 Dip.Domain

外部技術に依存しない中核です。

実装対象：

- Entity
- Value Object
- Enum
- Domain Rule
- Domain Event
- Domain Exception

EntityはSetterを公開しすぎず、状態変更メソッドから不変条件を守ります。

例：

```text
PipelineRun.Complete(...)
QualityIssue.Resolve(...)
Project.UpdateReleaseDate(...)
QualityGateResult.Fail(...)
```

### 6.2 Dip.Application

ユーザーや外部イベントから見たユースケースを実装します。

実装対象：

- Command
- Query
- Handler
- DTO
- Repository Interface
- External Service Interface
- Validation
- Transaction境界

1つのCommand／Queryは1つの明確な目的だけを持たせます。

### 6.3 Dip.Infrastructure

技術詳細を実装します。

実装対象：

- PostgreSQL
- Redis
- GitHub API
- OAuth / JWT
- MinIO
- Slack / Email
- OpenTelemetry
- Retry / Circuit Breaker

外部SDKの型はInfrastructure内に閉じ込め、Application／Domainへ漏らしません。

### 6.4 Dip.Api

外部から呼び出されるHTTP境界です。

責務：

- 認証・認可
- 入力検証
- Command／Query呼び出し
- HTTP StatusとError形式への変換
- Webhook受付
- Collector Report受付

Controllerは薄く保ちます。

### 6.5 Dip.Web

PM、プロデューサー、テックリード向けのDashboardです。

責務：

- Project Health表示
- Pull Request Risk表示
- 品質トレンド
- Pipeline詳細
- Team Load
- Release Readiness
- 通知一覧
- Quality Rule設定

UI側で判定ロジックを再実装せず、APIの結果を表示します。

### 6.6 Dip.Worker

時間のかかる処理や再試行可能な処理を担当します。

対象：

- GitHub Webhook処理
- Pipeline Report処理
- Quality Gate評価
- AI分析要求
- 通知
- 日次要約
- Dead Letter再処理

API Request内で長時間処理を行いません。

### 6.7 Python Analysis Service

構造化されたMetricやIssueから分析結果を返します。

初期版：

- Rule Base
- 統計的異常検知
- 移動平均・Z-score・Isolation Forest
- 根拠付き要約

後期版：

- 履歴データによる遅延予測
- PR Risk補助モデル
- リリース信頼度推定

生成AIの出力は意思決定の根拠そのものにせず、必ず元データを併記します。

### 6.8 Collectors

Collectorは、対象ツールの情報を共通Reportへ変換するAdapterです。

必須要件：

- 同じ入力から可能な限り同じ出力を生成する
- Sourceを破壊しない
- 失敗箇所をIssueとして報告する
- 部分成功を許容する
- JSON Schemaに適合する
- Collector Versionを出力する

---

## 7. 主要ドメインモデル

### 7.1 Project

プロジェクト全体の単位です。

保持する主な情報：

- ID
- Name
- Repository
- Default Branch
- Release Date
- Health Score
- Current Risk Level
- Quality Configuration
- Members

### 7.2 PullRequest

GitHub Pull Requestの内部表現です。

保持する主な情報：

- GitHub Number
- Title
- Author
- Base / Head Branch
- Status
- Changed Files
- Risk Score
- Quality Gate Status
- Linked Pipeline Runs

### 7.3 PipelineRun

CI/CDまたはCollector実行の単位です。

状態遷移：

```text
Queued
  -> Running
  -> Succeeded
  -> Failed
  -> Cancelled
  -> TimedOut
```

不正な逆遷移はDomainで拒否します。

### 7.4 Metric

数値指標です。

例：

- average_fps
- memory_mb
- build_size_mb
- scene_load_ms
- test_pass_rate
- build_duration_ms
- overdue_task_count

Metricは、現在値、単位、Baseline、差分、Severityを持ちます。

### 7.5 QualityIssue

品質上の問題です。

例：

- Missing Reference
- Test Failure
- FPS Regression
- 4K Texture
- Review Delay
- Build Failure

IssueはCategory、Severity、Source、Evidence、Status、Assigned Userを持ちます。

### 7.6 QualityRule

Quality Gateの判定規則です。

規則例：

```text
average_fps regression <= -5%     => Warning
average_fps regression <= -10%    => Critical
missing_reference count > 0       => Critical
build_result == failed            => Critical
```

### 7.7 DailySummary

PM向けの日次要約です。

含める情報：

- Overall Status
- Top Risks
- Recommended Actions
- Evidence Links
- Health Score変化
- 前日との差分

---

## 8. Collector Report Contract

全Collectorは、共通形式でReportを送ります。

```json
{
  "schemaVersion": "1.0",
  "collectorType": "unity",
  "collectorVersion": "0.1.0",
  "projectId": "project-001",
  "repository": "owner/repository",
  "commitSha": "abc123",
  "branch": "develop",
  "startedAt": "2026-07-16T10:00:00Z",
  "finishedAt": "2026-07-16T10:01:12Z",
  "status": "succeeded",
  "metrics": [],
  "issues": [],
  "artifacts": []
}
```

### 8.1 互換性方針

- `schemaVersion` を必須にする
- 既存Fieldの意味を変更しない
- Field追加はOptionalから始める
- 破壊的変更はMajor Versionを上げる
- API側で未対応Versionを明示的に拒否する

---

## 9. 主要データフロー

### 9.1 Pull Request評価

```text
PR Open / Synchronize
    |
GitHub Webhook
    |
Signature Verification
    |
QueueへMessage登録
    |
PR / Commit / Changed Files取得
    |
CI / Collector実行
    |
Collector Report受付
    |
Metric / Issue保存
    |
Baseline比較
    |
Quality Gate評価
    |
Risk Score再計算
    |
GitHub PRへComment
    |
Dashboard更新
```

### 9.2 日次要約

```text
Scheduler
    |
Projectごとの最新状態取得
    |
Build / Test / Quality / Team Load集計
    |
Python Analysis Serviceへ要求
    |
根拠付きSummary受信
    |
DailySummary保存
    |
Dashboard / Slack / Email通知
```

### 9.3 リリース予測

```text
Release Date
Open Tasks
Overdue Tasks
Velocity
Build Stability
Critical Issues
Review Lead Time
    |
Feature生成
    |
Rule BaseまたはPrediction Model
    |
Confidence + Factors
    |
ReleaseReadiness画面へ表示
```

---

## 10. Quality Gate設計

### 10.1 判定レベル

```text
PASS
WARNING
FAIL
```

### 10.2 原則

- 絶対値とBaseline差分を分けて判定する
- 過去から存在する問題と今回追加された問題を区別する
- RuleごとにEvidenceを保存する
- Rule Versionを記録する
- 同じ入力に対して判定が再現できること
- AIに最終合否を決定させない

### 10.3 初期ルール

Critical：

- Build Failed
- Test Failed
- Missing Reference > 0
- FPS Regression > 10%
- Memory Regression > 20%

Warning：

- Texture Size > 4096
- Mesh Triangles > 100,000
- Build Size Regression > 10%
- FPS Regression > 5%
- Review Wait > 24 hours

---

## 11. Project Health Score

初期式：

```text
Schedule             20%
Build                 15%
Test                  15%
Performance           15%
Code Quality          10%
Asset Quality         10%
Team Load             10%
Release Readiness      5%
```

設計上の注意：

- 0から100へ正規化する
- Projectごとに重みを変更可能にする
- Scoreだけでなく内訳を保存する
- 前日比と7日移動平均を表示する
- 欠損値を0点として扱わず、未計測として明示する

---

## 12. Pull Request Risk Score

入力候補：

- Changed Files数
- 変更行数
- 重要Module変更
- Test Failure
- Build Failure
- Performance Regression
- Asset Regression
- Review Delay
- 過去のFailure Rate

初期版は説明可能なRule Baseを使用します。

```text
0 - 29   Low
30 - 59  Medium
60 - 79  High
80 - 100 Critical
```

必ず「なぜそのScoreになったか」を要因別に返します。

---

## 13. エラー処理

### 13.1 API Error形式

```json
{
  "type": "validation_error",
  "title": "Invalid collector report",
  "status": 400,
  "traceId": "00-...",
  "errors": {
    "schemaVersion": ["Unsupported schema version."]
  }
}
```

### 13.2 方針

- 予期可能な業務エラーは専用結果またはDomain Exception
- 予期しない例外はMiddlewareで500へ変換
- Trace IDを全応答・Logへ付与
- Token、Secret、個人情報をLogへ出さない
- 外部API失敗はRetry後にQueueへ戻す
- 再試行上限を超えたMessageはDead Letterへ送る

---

## 14. 冪等性と重複排除

GitHub WebhookやCI Reportは重複送信される可能性があります。

一意キー例：

```text
Webhook Delivery ID
Project ID + Commit SHA + Collector Type + Collector Version
GitHub Repository + Workflow Run ID
```

同一キーの再処理では、重複Entityを作成せず、既存結果を返します。

---

## 15. セキュリティ

必須事項：

- GitHub Webhook HMAC署名検証
- GitHub OAuth State検証
- JWT短時間有効化
- Refresh Token安全保存
- Project単位のアクセス制御
- Role Based Access Control
- Collector API Keyまたは署名
- Rate Limit
- CORS制限
- SecretをRepositoryへCommitしない
- Object Storageの公開Accessを禁止
- Audit Log保存

初期Role：

```text
Admin
ProjectManager
TechLead
Member
Viewer
```

---

## 16. 可観測性

### 16.1 Log

JSON形式の構造化Logを使用します。

主なField：

- timestamp
- level
- traceId
- projectId
- pullRequestId
- pipelineRunId
- worker
- message
- exceptionType

### 16.2 Metric

- HTTP Request Duration
- Error Rate
- Queue Length
- Worker Processing Time
- Dead Letter Count
- GitHub API Rate Limit Remaining
- Analysis Service Latency
- DB Connection Count

### 16.3 Trace

Webhook受付からQuality Gate、通知までを同じTraceで追跡します。

---

## 17. Database設計方針

- IDはUUIDを基本とする
- 外部IDは別Columnで保持する
- `created_at` と `updated_at` を持つ
- 必要なEntityにConcurrency Tokenを持たせる
- Metricは大量化するためIndexを設計する
- JSONは柔軟性が必要なEvidenceやRaw Payloadに限定する
- Domain Entity全体をJSON保存しない
- Migrationは自動実行せず、Deployment工程で明示的に実行する

重要Index例：

```text
PipelineRuns(project_id, commit_sha, collector_type)
Metrics(pipeline_run_id, metric_name)
QualityIssues(project_id, severity, status)
PullRequests(project_id, github_pr_number)
Notifications(project_id, status, created_at)
```

---

## 18. AI・分析設計

### 18.1 AIに任せる範囲

- 根拠付き要約
- Issue分類補助
- 優先対応候補
- 類似Failureの説明
- PM向け自然言語化

### 18.2 AIに任せない範囲

- Quality Gate最終合否
- Merge許可
- 人事評価
- 責任者断定
- 根拠のないRelease Date断定
- 自動修正の無確認適用

### 18.3 Evidence First

AI入力には構造化データと参照IDを渡し、出力にはEvidence IDを必須にします。

---

## 19. Testing Strategy

### 19.1 Unit Test

- Value Object検証
- Entity状態遷移
- Quality Gate Rule
- Health Score
- Risk Score
- DTO Mapping

### 19.2 Integration Test

- PostgreSQL Repository
- Redis Queue
- GitHub Webhook検証
- Analysis Service Client
- Object Storage

### 19.3 End-to-End Test

```text
PR Event
 -> Webhook
 -> Queue
 -> Collector Report
 -> Quality Gate
 -> PR Comment
 -> Dashboard
```

### 19.4 Contract Test

- Collector JSON Schema
- Python API Response
- GitHub Webhook Payload
- Shared Error Format

### 19.5 Load Test

- 100並列Webhook
- 10,000 Pipeline Runs
- 1,000,000 Metrics
- Dashboard Query
- Queue Backlog回復

---

## 20. 実装順序

### Phase 1: Vertical Slice

最初に次の一連を完成させます。

```text
Generic Collector JSON
 -> POST /api/collectors/reports
 -> PostgreSQL保存
 -> Project Dashboard表示
```

対象Entity：

- Project
- PipelineRun
- Metric
- QualityIssue

### Phase 2: Quality Gate

- Baseline比較
- Rule設定
- PASS / WARNING / FAIL
- PR Risk Score

### Phase 3: GitHub Integration

- OAuth
- Webhook
- PR同期
- Workflow Run同期
- PR Comment

### Phase 4: Worker / Queue

- Redis Queue
- Retry
- Dead Letter
- Daily Summary

### Phase 5: Python Analysis

- Anomaly Detection
- Evidence Builder
- Summary Generator
- Release Prediction

### Phase 6: Engine / DCC Collectors

- Unity
- Maya
- Native C++
- Generic JSON

### Phase 7: Operations

- Docker
- Monitoring
- Security Scan
- Load Test
- Release Packaging

---

## 21. Coding Rules

### C#

- Nullableを有効化する
- Public APIへXML Commentを付ける
- CancellationTokenを伝播する
- 非同期I/OはAsyncを使用する
- `DateTime.UtcNow` を直接使わず `ISystemClock`
- Domain EntityのSetterを不用意にPublicにしない
- ControllerからDbContextを直接使用しない

### Python

- 型Hintを必須にする
- PydanticでAPI境界を検証する
- DataFrameをAPI Responseへ直接返さない
- Model FileのVersionとFeature定義を保存する
- Random Seedを固定可能にする

### C++

- RAIIを使用する
- Raw owning pointerを避ける
- `std::filesystem` を利用する
- CLIは非対話で動作可能にする
- JSON出力はSchemaへ適合させる
- 大規模Directoryに対するMemory上限を考慮する

---

## 22. 完成条件

- Docker Composeで主要Serviceが起動する
- Generic Collector Reportを受信できる
- DBへProject、Pipeline、Metric、Issueを保存できる
- Quality Gateを再現可能に判定できる
- DashboardにProject HealthとPR Riskを表示できる
- GitHub PRへ結果をCommentできる
- Workerが再試行とDead Letterを処理できる
- Python分析が根拠付き要約を返す
- Unit、Integration、E2E TestがCIで成功する
- READMEだけで第三者が起動できる
- 3分のDemoで問題、処理、価値を説明できる

---

## 23. トップレベル構成

| パス | 責務 |
|---|---|
| `.github/` | Issue、PR、CODEOWNERS、CI/CD |
| `src/` | C#のDomain、Application、Infrastructure、API、Web、Worker |
| `services/python-analysis/` | Python分析・AIサービス |
| `collectors/` | Generic、Unity、Maya、Native Collector |
| `tests/` | Unit、Integration、E2E、Load Test |
| `infrastructure/` | Docker、DB、Monitoring、Reverse Proxy、運用Script |
| `docs/` | 要件、Architecture、API、Security、Test、Portfolio |
| `samples/` | 再現用Sample ProjectとReport |
| `schemas/` | JSON Schema |
| `scripts/` | Setup、Build、Test、Format、Release自動化 |
| `artifacts/` | 一時生成Artifact |
| `reports/` | Test、Benchmark、品質Report |

---

## 24. 各ファイルの実装内容

以下は、空ファイルへ何を書くかの一覧です。
- `.dockerignore` — Docker Build Contextから不要なBuild生成物、Git情報、秘密情報を除外する。
- `.editorconfig` — C#、Python、C++、JSON、YAMLの文字コード、改行、Indent、Styleを統一する。
- `.env.example` — 必要な環境変数名と安全なダミー値、用途を提示する。
- `.gitattributes` — 改行、Diff、Binary扱い、必要に応じたGit LFS属性を定義する。
- `.github/CODEOWNERS` — ディレクトリ単位の責任者と必須レビュアーを定義する。
- `.github/ISSUE_TEMPLATE/bug_report.yml` — Issue作成時の入力項目、再現手順、期待結果、優先度、影響範囲などを標準化する。
- `.github/ISSUE_TEMPLATE/config.yml` — Issue作成時の入力項目、再現手順、期待結果、優先度、影響範囲などを標準化する。
- `.github/ISSUE_TEMPLATE/feature_request.yml` — Issue作成時の入力項目、再現手順、期待結果、優先度、影響範囲などを標準化する。
- `.github/PULL_REQUEST_TEMPLATE.md` — Pull Requestの目的、変更内容、確認方法、影響範囲、ロールバック方法、品質ゲート結果を書くテンプレート。
- `.github/workflows/docker-build.yml` — GitHub Actionsでビルド、テスト、静的解析、Dockerイメージ作成、統合試験、セキュリティ検査、Release生成を自動化する。
- `.github/workflows/dotnet-ci.yml` — GitHub Actionsでビルド、テスト、静的解析、Dockerイメージ作成、統合試験、セキュリティ検査、Release生成を自動化する。
- `.github/workflows/integration-test.yml` — GitHub Actionsでビルド、テスト、静的解析、Dockerイメージ作成、統合試験、セキュリティ検査、Release生成を自動化する。
- `.github/workflows/native-collector-ci.yml` — GitHub Actionsでビルド、テスト、静的解析、Dockerイメージ作成、統合試験、セキュリティ検査、Release生成を自動化する。
- `.github/workflows/python-ci.yml` — GitHub Actionsでビルド、テスト、静的解析、Dockerイメージ作成、統合試験、セキュリティ検査、Release生成を自動化する。
- `.github/workflows/release.yml` — GitHub Actionsでビルド、テスト、静的解析、Dockerイメージ作成、統合試験、セキュリティ検査、Release生成を自動化する。
- `.github/workflows/security-scan.yml` — GitHub Actionsでビルド、テスト、静的解析、Dockerイメージ作成、統合試験、セキュリティ検査、Release生成を自動化する。
- `.gitignore` — Build生成物、秘密情報、仮想環境、ローカルDataをGit管理対象外にする。
- `CHANGELOG.md` — Versionごとの追加、変更、修正、破壊的変更を記録する。
- `CODE_OF_CONDUCT.md` — Project参加者の行動規範を記述する。
- `CONTRIBUTING.md` — 環境構築、Branch、Commit、PR、Test、Code Styleの貢献規約を記述する。
- `DevelopmentIntelligencePlatform.sln` — Visual StudioでC#プロジェクト群を一括管理するソリューション定義。
- `Directory.Build.props` — 全.NET Project共通のFramework、Nullable、Analyzer、Warning方針を定義する。
- `Directory.Build.targets` — .NET Build後の追加検証、Code Generation、Artifact収集など共通Targetを定義する。
- `Directory.Packages.props` — NuGet Package Versionを中央管理する。
- `LICENSE` — 公開・改変・再配布条件を記述する。
- `SECURITY.md` — 脆弱性報告窓口、対応範囲、Disclosure方針を記述する。
- `artifacts/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `collectors/generic-json-collector/Dockerfile` — 対象ServiceのBuild Stage、Runtime Stage、非Root実行、Health Checkを定義する。
- `collectors/generic-json-collector/GenericJsonCollector.csproj` — .NETプロジェクトのSDK、Target Framework、参照、NuGet依存、Analyzer、生成物設定を定義する。
- `collectors/generic-json-collector/schemas/collector-report.schema.json` — Collector／API入力のJSON構造、必須項目、型、範囲、互換Versionを定義する。
- `collectors/generic-json-collector/src/Commands/.gitkeep` — `.gitkeep`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `collectors/generic-json-collector/src/Parsing/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `collectors/generic-json-collector/src/Program.cs` — プロセス起動、設定読込、DI登録、Middleware、Endpoint、Graceful Shutdownを構成する。
- `collectors/generic-json-collector/src/Validation/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `collectors/generic-json-collector/tests/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `collectors/maya-collector/dip_maya_collector/analyzers/animation_analyzer.py` — `animation_analyzer`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `collectors/maya-collector/dip_maya_collector/analyzers/blendshape_analyzer.py` — `blendshape_analyzer`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `collectors/maya-collector/dip_maya_collector/analyzers/hierarchy_analyzer.py` — `hierarchy_analyzer`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `collectors/maya-collector/dip_maya_collector/analyzers/material_analyzer.py` — `material_analyzer`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `collectors/maya-collector/dip_maya_collector/analyzers/mesh_analyzer.py` — `mesh_analyzer`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `collectors/maya-collector/dip_maya_collector/analyzers/skin_analyzer.py` — `skin_analyzer`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `collectors/maya-collector/dip_maya_collector/config.py` — `config`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `collectors/maya-collector/dip_maya_collector/main.py` — `main`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `collectors/maya-collector/dip_maya_collector/reporting/report_writer.py` — `report_writer`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `collectors/maya-collector/setup.py` — `setup`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `collectors/maya-collector/tests/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `collectors/native-collector/CMakeLists.txt` — Native CollectorのTarget、Compiler Option、Dependency、Test、Install規則を定義する。
- `collectors/native-collector/Dockerfile` — 対象ServiceのBuild Stage、Runtime Stage、非Root実行、Health Checkを定義する。
- `collectors/native-collector/cmake/CompilerWarnings.cmake` — CMakeで再利用するCompiler Warningや共通Build設定を定義する。
- `collectors/native-collector/include/dip/AssetHasher.hpp` — `AssetHasher`のC++公開型、Interface、関数宣言を記述する。
- `collectors/native-collector/include/dip/CollectorOptions.hpp` — `CollectorOptions`のC++公開型、Interface、関数宣言を記述する。
- `collectors/native-collector/include/dip/DuplicateDetector.hpp` — `DuplicateDetector`のC++公開型、Interface、関数宣言を記述する。
- `collectors/native-collector/include/dip/FileScanner.hpp` — `FileScanner`のC++公開型、Interface、関数宣言を記述する。
- `collectors/native-collector/include/dip/LogAnalyzer.hpp` — `LogAnalyzer`のC++公開型、Interface、関数宣言を記述する。
- `collectors/native-collector/include/dip/ReportWriter.hpp` — `ReportWriter`のC++公開型、Interface、関数宣言を記述する。
- `collectors/native-collector/src/AssetHasher.cpp` — `AssetHasher`のC++実装またはNative Collector Testを記述する。
- `collectors/native-collector/src/DuplicateDetector.cpp` — `DuplicateDetector`のC++実装またはNative Collector Testを記述する。
- `collectors/native-collector/src/FileScanner.cpp` — `FileScanner`のC++実装またはNative Collector Testを記述する。
- `collectors/native-collector/src/LogAnalyzer.cpp` — `LogAnalyzer`のC++実装またはNative Collector Testを記述する。
- `collectors/native-collector/src/ReportWriter.cpp` — `ReportWriter`のC++実装またはNative Collector Testを記述する。
- `collectors/native-collector/src/main.cpp` — `main`のC++実装またはNative Collector Testを記述する。
- `collectors/native-collector/tests/DuplicateDetectorTests.cpp` — `DuplicateDetectorTests`のC++実装またはNative Collector Testを記述する。
- `collectors/native-collector/tests/FileScannerTests.cpp` — `FileScannerTests`のC++実装またはNative Collector Testを記述する。
- `collectors/native-collector/tests/LogAnalyzerTests.cpp` — `LogAnalyzerTests`のC++実装またはNative Collector Testを記述する。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Editor/Analysis/AnimationAnalyzer.cs` — Python分析サービスへのHTTP要求、Timeout、再試行、応答変換を実装する。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Editor/Analysis/BuildSettingsAnalyzer.cs` — Python分析サービスへのHTTP要求、Timeout、再試行、応答変換を実装する。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Editor/Analysis/MaterialAnalyzer.cs` — Python分析サービスへのHTTP要求、Timeout、再試行、応答変換を実装する。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Editor/Analysis/MeshAnalyzer.cs` — Python分析サービスへのHTTP要求、Timeout、再試行、応答変換を実装する。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Editor/Analysis/ReferenceAnalyzer.cs` — Python分析サービスへのHTTP要求、Timeout、再試行、応答変換を実装する。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Editor/Analysis/SceneAnalyzer.cs` — Python分析サービスへのHTTP要求、Timeout、再試行、応答変換を実装する。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Editor/Analysis/TextureAnalyzer.cs` — Python分析サービスへのHTTP要求、Timeout、再試行、応答変換を実装する。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Editor/Cli/UnityCollectorCli.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Editor/Configuration/UnityCollectorSettings.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Editor/Reporting/UnityCollectorReportWriter.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Editor/UI/UnityCollectorWindow.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Runtime/PerformanceProbe.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Tests/Editor/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/Tests/Runtime/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `collectors/unity-collector/Packages/com.mkyosei.dip.collector/package.json` — Unity Package Manager向けのName、Version、依存、Unity Version、説明を定義する。
- `collectors/unity-collector/samples/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `docker-compose.override.yml` — CI/CD、監視、Docker関連などのYAML設定を書く。
- `docker-compose.yml` — CI/CD、監視、Docker関連などのYAML設定を書く。
- `docs/00-overview/glossary.md` — `glossary`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/00-overview/goals-and-non-goals.md` — `goals-and-non-goals`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/00-overview/project-overview.md` — `project-overview`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/01-requirements/acceptance-criteria.md` — `acceptance-criteria`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/01-requirements/functional-requirements.md` — `functional-requirements`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/01-requirements/non-functional-requirements.md` — `non-functional-requirements`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/01-requirements/user-stories.md` — `user-stories`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/02-architecture/architecture-decisions/ADR-001-use-dotnet.md` — `ADR-001-use-dotnet`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/02-architecture/architecture-decisions/ADR-002-use-postgresql.md` — `ADR-002-use-postgresql`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/02-architecture/architecture-decisions/ADR-003-use-redis.md` — `ADR-003-use-redis`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/02-architecture/architecture-decisions/ADR-004-analysis-service-boundary.md` — `ADR-004-analysis-service-boundary`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/02-architecture/architecture-decisions/ADR-005-collector-contract.md` — `ADR-005-collector-contract`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/02-architecture/component-diagram.md` — `component-diagram`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/02-architecture/container-diagram.md` — `container-diagram`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/02-architecture/data-flow.md` — `data-flow`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/02-architecture/deployment-diagram.md` — `deployment-diagram`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/02-architecture/system-context.md` — `system-context`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/03-database/er-diagram.md` — `er-diagram`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/03-database/migration-policy.md` — `migration-policy`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/03-database/schema.md` — `schema`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/04-api/api-overview.md` — `api-overview`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/04-api/authentication.md` — `authentication`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/04-api/collector-api.md` — `collector-api`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/04-api/error-format.md` — `error-format`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/04-api/webhook-api.md` — `webhook-api`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/05-quality/baseline-comparison.md` — `baseline-comparison`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/05-quality/health-score.md` — `health-score`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/05-quality/pull-request-risk-score.md` — `pull-request-risk-score`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/05-quality/quality-gate-rules.md` — `quality-gate-rules`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/06-operations/backup-and-restore.md` — `backup-and-restore`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/06-operations/deployment.md` — `deployment`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/06-operations/incident-response.md` — `incident-response`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/06-operations/local-development.md` — `local-development`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/06-operations/monitoring.md` — `monitoring`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/07-security/access-control.md` — `access-control`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/07-security/secrets-management.md` — `secrets-management`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/07-security/threat-model.md` — `threat-model`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/07-security/webhook-security.md` — `webhook-security`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/08-testing/end-to-end-scenarios.md` — `end-to-end-scenarios`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/08-testing/load-test-plan.md` — `load-test-plan`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/08-testing/test-strategy.md` — `test-strategy`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/09-portfolio/benchmark-results.md` — `benchmark-results`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/09-portfolio/demo-script.md` — `demo-script`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/09-portfolio/reviewer-brief.md` — `reviewer-brief`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/09-portfolio/screenshots/.gitkeep` — `.gitkeep`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/09-portfolio/videos/.gitkeep` — `.gitkeep`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/diagrams/exported/.gitkeep` — `.gitkeep`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `docs/diagrams/source/.gitkeep` — `.gitkeep`に対応する要件、設計、運用、Security、Test、Portfolio資料を記述する。
- `global.json` — Sample Data、Grafana Dashboard、設定、期待結果などを記述する。
- `infrastructure/database/init/001-create-database.sql` — DB初期化、Extension、有効なSample Data投入を記述する。
- `infrastructure/database/init/002-create-extensions.sql` — DB初期化、Extension、有効なSample Data投入を記述する。
- `infrastructure/database/seed/sample-project.sql` — DB初期化、Extension、有効なSample Data投入を記述する。
- `infrastructure/docker/analysis.Dockerfile` — 対象ServiceのBuild Stage、Runtime Stage、非Root実行、Health Checkを定義する。
- `infrastructure/docker/api.Dockerfile` — 対象ServiceのBuild Stage、Runtime Stage、非Root実行、Health Checkを定義する。
- `infrastructure/docker/native-collector.Dockerfile` — 対象ServiceのBuild Stage、Runtime Stage、非Root実行、Health Checkを定義する。
- `infrastructure/docker/web.Dockerfile` — 対象ServiceのBuild Stage、Runtime Stage、非Root実行、Health Checkを定義する。
- `infrastructure/docker/worker.Dockerfile` — 対象ServiceのBuild Stage、Runtime Stage、非Root実行、Health Checkを定義する。
- `infrastructure/monitoring/grafana/dashboards/api-overview.json` — Sample Data、Grafana Dashboard、設定、期待結果などを記述する。
- `infrastructure/monitoring/grafana/dashboards/queue-overview.json` — Sample Data、Grafana Dashboard、設定、期待結果などを記述する。
- `infrastructure/monitoring/grafana/dashboards/worker-overview.json` — Sample Data、Grafana Dashboard、設定、期待結果などを記述する。
- `infrastructure/monitoring/grafana/provisioning/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `infrastructure/monitoring/otel/otel-collector-config.yml` — CI/CD、監視、Docker関連などのYAML設定を書く。
- `infrastructure/monitoring/prometheus/prometheus.yml` — CI/CD、監視、Docker関連などのYAML設定を書く。
- `infrastructure/reverse-proxy/nginx.conf` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `infrastructure/scripts/migrate-database.ps1` — Windows PowerShellで環境構築、起動停止、Migration、Test、Format、Packageを自動化する。
- `infrastructure/scripts/reset-local.ps1` — Windows PowerShellで環境構築、起動停止、Migration、Test、Format、Packageを自動化する。
- `infrastructure/scripts/run-all-tests.ps1` — Windows PowerShellで環境構築、起動停止、Migration、Test、Format、Packageを自動化する。
- `infrastructure/scripts/seed-sample-data.ps1` — Windows PowerShellで環境構築、起動停止、Migration、Test、Format、Packageを自動化する。
- `infrastructure/scripts/start-local.ps1` — Windows PowerShellで環境構築、起動停止、Migration、Test、Format、Packageを自動化する。
- `infrastructure/scripts/stop-local.ps1` — Windows PowerShellで環境構築、起動停止、Migration、Test、Format、Packageを自動化する。
- `reports/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `samples/sample-game-project/collector-reports/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `samples/sample-game-project/expected-results/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `samples/sample-pipeline-data/metrics.json` — Sample Data、Grafana Dashboard、設定、期待結果などを記述する。
- `samples/sample-pipeline-data/pipeline-runs.json` — Sample Data、Grafana Dashboard、設定、期待結果などを記述する。
- `samples/sample-pipeline-data/projects.json` — Sample Data、Grafana Dashboard、設定、期待結果などを記述する。
- `samples/sample-pipeline-data/pull-requests.json` — Sample Data、Grafana Dashboard、設定、期待結果などを記述する。
- `samples/sample-pipeline-data/quality-issues.json` — Sample Data、Grafana Dashboard、設定、期待結果などを記述する。
- `samples/sample-web-project/collector-reports/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `schemas/collector-report.schema.json` — Collector／API入力のJSON構造、必須項目、型、範囲、互換Versionを定義する。
- `schemas/metric.schema.json` — Collector／API入力のJSON構造、必須項目、型、範囲、互換Versionを定義する。
- `schemas/pipeline-run.schema.json` — Collector／API入力のJSON構造、必須項目、型、範囲、互換Versionを定義する。
- `schemas/quality-issue.schema.json` — Collector／API入力のJSON構造、必須項目、型、範囲、互換Versionを定義する。
- `schemas/quality-rules.schema.json` — Collector／API入力のJSON構造、必須項目、型、範囲、互換Versionを定義する。
- `scripts/build-all.ps1` — Windows PowerShellで環境構築、起動停止、Migration、Test、Format、Packageを自動化する。
- `scripts/format-all.ps1` — Windows PowerShellで環境構築、起動停止、Migration、Test、Format、Packageを自動化する。
- `scripts/generate-sample-data.ps1` — Windows PowerShellで環境構築、起動停止、Migration、Test、Format、Packageを自動化する。
- `scripts/package-release.ps1` — Windows PowerShellで環境構築、起動停止、Migration、Test、Format、Packageを自動化する。
- `scripts/setup-development.ps1` — Windows PowerShellで環境構築、起動停止、Migration、Test、Format、Packageを自動化する。
- `scripts/test-all.ps1` — Windows PowerShellで環境構築、起動停止、Migration、Test、Format、Packageを自動化する。
- `scripts/verify-environment.ps1` — Windows PowerShellで環境構築、起動停止、Migration、Test、Format、Packageを自動化する。
- `services/python-analysis/Dockerfile` — 対象ServiceのBuild Stage、Runtime Stage、非Root実行、Health Checkを定義する。
- `services/python-analysis/app/api/dependencies.py` — `dependencies`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/api/routes/anomaly.py` — `anomaly`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/api/routes/health.py` — `health`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/api/routes/release_prediction.py` — `release_prediction`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/api/routes/risk.py` — `risk`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/api/routes/summary.py` — `summary`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/core/config.py` — `config`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/core/logging.py` — `logging`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/core/security.py` — `security`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/main.py` — `main`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/models/anomaly_model.py` — `anomaly_model`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/models/release_model.py` — `release_model`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/models/risk_model.py` — `risk_model`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/prompts/daily_summary.txt` — 生成AIへ渡す入力形式、禁止事項、根拠引用、出力JSON Schemaを記述する。
- `services/python-analysis/app/prompts/release_recommendation.txt` — 生成AIへ渡す入力形式、禁止事項、根拠引用、出力JSON Schemaを記述する。
- `services/python-analysis/app/prompts/risk_explanation.txt` — 生成AIへ渡す入力形式、禁止事項、根拠引用、出力JSON Schemaを記述する。
- `services/python-analysis/app/repositories/metric_repository.py` — `metric_repository`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/repositories/project_repository.py` — `project_repository`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/schemas/anomaly.py` — `anomaly`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/schemas/release_prediction.py` — `release_prediction`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/schemas/risk.py` — `risk`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/schemas/summary.py` — `summary`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/services/anomaly_detector.py` — `anomaly_detector`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/services/evidence_builder.py` — `evidence_builder`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/services/log_classifier.py` — `log_classifier`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/services/release_predictor.py` — `release_predictor`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/services/risk_analyzer.py` — `risk_analyzer`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/app/services/summary_generator.py` — `summary_generator`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/data/models/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `services/python-analysis/data/samples/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `services/python-analysis/pyproject.toml` — Python Project Metadata、Build、Formatter、Lint、型検査設定を定義する。
- `services/python-analysis/pytest.ini` — pytestの探索、Marker、Warning、非同期試験方針を定義する。
- `services/python-analysis/requirements-dev.txt` — pytest、Formatter、Lint、型検査など開発用依存を固定する。
- `services/python-analysis/requirements.txt` — Python分析サービスの本番依存パッケージを固定する。
- `services/python-analysis/scripts/evaluate_models.py` — `evaluate_models`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/scripts/generate_sample_data.py` — `generate_sample_data`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/scripts/train_risk_model.py` — `train_risk_model`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/tests/test_anomaly_detector.py` — `test_anomaly_detector`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/tests/test_release_predictor.py` — `test_release_predictor`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/tests/test_risk_analyzer.py` — `test_risk_analyzer`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `services/python-analysis/tests/test_summary_generator.py` — `test_summary_generator`に対応するFastAPI Route、分析、予測、Repository、Collector、Test、Scriptを実装する。
- `src/Dip.Api/Controllers/AnalysisController.cs` — HTTP入力を検証し、Application層のCommand／Queryを呼び出し、適切なHTTP応答へ変換する。
- `src/Dip.Api/Controllers/AuthController.cs` — HTTP入力を検証し、Application層のCommand／Queryを呼び出し、適切なHTTP応答へ変換する。
- `src/Dip.Api/Controllers/CollectorsController.cs` — HTTP入力を検証し、Application層のCommand／Queryを呼び出し、適切なHTTP応答へ変換する。
- `src/Dip.Api/Controllers/MetricsController.cs` — HTTP入力を検証し、Application層のCommand／Queryを呼び出し、適切なHTTP応答へ変換する。
- `src/Dip.Api/Controllers/NotificationsController.cs` — HTTP入力を検証し、Application層のCommand／Queryを呼び出し、適切なHTTP応答へ変換する。
- `src/Dip.Api/Controllers/PipelineRunsController.cs` — HTTP入力を検証し、Application層のCommand／Queryを呼び出し、適切なHTTP応答へ変換する。
- `src/Dip.Api/Controllers/ProjectsController.cs` — HTTP入力を検証し、Application層のCommand／Queryを呼び出し、適切なHTTP応答へ変換する。
- `src/Dip.Api/Controllers/PullRequestsController.cs` — HTTP入力を検証し、Application層のCommand／Queryを呼び出し、適切なHTTP応答へ変換する。
- `src/Dip.Api/Controllers/QualityIssuesController.cs` — HTTP入力を検証し、Application層のCommand／Queryを呼び出し、適切なHTTP応答へ変換する。
- `src/Dip.Api/Controllers/TeamLoadController.cs` — HTTP入力を検証し、Application層のCommand／Queryを呼び出し、適切なHTTP応答へ変換する。
- `src/Dip.Api/Controllers/WebhooksController.cs` — HTTP入力を検証し、Application層のCommand／Queryを呼び出し、適切なHTTP応答へ変換する。
- `src/Dip.Api/Dip.Api.csproj` — .NETプロジェクトのSDK、Target Framework、参照、NuGet依存、Analyzer、生成物設定を定義する。
- `src/Dip.Api/Extensions/ApplicationBuilderExtensions.cs` — ASP.NET Coreのサービス登録、Middleware、Swagger設定を拡張メソッドへ分離する。
- `src/Dip.Api/Extensions/ServiceCollectionExtensions.cs` — ASP.NET Coreのサービス登録、Middleware、Swagger設定を拡張メソッドへ分離する。
- `src/Dip.Api/Extensions/SwaggerExtensions.cs` — ASP.NET Coreのサービス登録、Middleware、Swagger設定を拡張メソッドへ分離する。
- `src/Dip.Api/Filters/ValidationFilter.cs` — Controller／Actionに共通適用する検証や変換処理を実装する。
- `src/Dip.Api/Middleware/CorrelationIdMiddleware.cs` — 例外、Correlation ID、Request Log、Rate LimitなどHTTP共通処理を実装する。
- `src/Dip.Api/Middleware/ExceptionHandlingMiddleware.cs` — 例外、Correlation ID、Request Log、Rate LimitなどHTTP共通処理を実装する。
- `src/Dip.Api/Middleware/RateLimitMiddleware.cs` — 例外、Correlation ID、Request Log、Rate LimitなどHTTP共通処理を実装する。
- `src/Dip.Api/Middleware/RequestLoggingMiddleware.cs` — 例外、Correlation ID、Request Log、Rate LimitなどHTTP共通処理を実装する。
- `src/Dip.Api/Models/Requests/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `src/Dip.Api/Models/Responses/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `src/Dip.Api/Program.cs` — プロセス起動、設定読込、DI登録、Middleware、Endpoint、Graceful Shutdownを構成する。
- `src/Dip.Api/Properties/launchSettings.json` — Visual Studio実行時のURL、環境変数、起動Profileを定義する。
- `src/Dip.Api/appsettings.Development.json` — 接続先、ログレベル、認証、Queue、Storage、外部サービスなど非秘密設定を書く。
- `src/Dip.Api/appsettings.json` — 接続先、ログレベル、認証、Queue、Storage、外部サービスなど非秘密設定を書く。
- `src/Dip.Application/Abstractions/External/IAnalysisClient.cs` — GitHub、分析サービス、通知、Object Storage、Queueなど外部I/Oの抽象を定義する。
- `src/Dip.Application/Abstractions/External/IGitHubClient.cs` — GitHub、分析サービス、通知、Object Storage、Queueなど外部I/Oの抽象を定義する。
- `src/Dip.Application/Abstractions/External/INotificationClient.cs` — GitHub、分析サービス、通知、Object Storage、Queueなど外部I/Oの抽象を定義する。
- `src/Dip.Application/Abstractions/External/IObjectStorage.cs` — GitHub、分析サービス、通知、Object Storage、Queueなど外部I/Oの抽象を定義する。
- `src/Dip.Application/Abstractions/External/IQueueClient.cs` — GitHub、分析サービス、通知、Object Storage、Queueなど外部I/Oの抽象を定義する。
- `src/Dip.Application/Abstractions/Persistence/IMetricRepository.cs` — Application層から利用するRepository／Unit of Workの抽象を定義する。
- `src/Dip.Application/Abstractions/Persistence/IPipelineRunRepository.cs` — Application層から利用するRepository／Unit of Workの抽象を定義する。
- `src/Dip.Application/Abstractions/Persistence/IProjectRepository.cs` — Application層から利用するRepository／Unit of Workの抽象を定義する。
- `src/Dip.Application/Abstractions/Persistence/IQualityIssueRepository.cs` — Application層から利用するRepository／Unit of Workの抽象を定義する。
- `src/Dip.Application/Abstractions/Persistence/IUnitOfWork.cs` — Application層から利用するRepository／Unit of Workの抽象を定義する。
- `src/Dip.Application/Abstractions/Time/ISystemClock.cs` — 現在時刻取得を抽象化し、テスト時に固定時刻へ置き換え可能にする。
- `src/Dip.Application/Behaviors/LoggingBehavior.cs` — ユースケース共通の検証、構造化ログ、トランザクション境界をPipeline Behaviorとして実装する。
- `src/Dip.Application/Behaviors/TransactionBehavior.cs` — ユースケース共通の検証、構造化ログ、トランザクション境界をPipeline Behaviorとして実装する。
- `src/Dip.Application/Behaviors/ValidationBehavior.cs` — ユースケース共通の検証、構造化ログ、トランザクション境界をPipeline Behaviorとして実装する。
- `src/Dip.Application/DependencyInjection.cs` — 対象プロジェクトの実装をDIコンテナへ登録する拡張メソッドを提供する。
- `src/Dip.Application/Dip.Application.csproj` — .NETプロジェクトのSDK、Target Framework、参照、NuGet依存、Analyzer、生成物設定を定義する。
- `src/Dip.Application/Features/Analysis/Commands/RequestAnomalyDetectionCommand.cs` — `RequestAnomalyDetectionCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/Analysis/Commands/RequestDailySummaryCommand.cs` — `RequestDailySummaryCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/Analysis/Commands/RequestReleasePredictionCommand.cs` — `RequestReleasePredictionCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/Analysis/Dtos/AnomalyResultDto.cs` — UI、API、外部サービスへ渡すデータ転送オブジェクトを定義し、ドメインモデルを直接公開しない。
- `src/Dip.Application/Features/Analysis/Dtos/DailySummaryDto.cs` — UI、API、外部サービスへ渡すデータ転送オブジェクトを定義し、ドメインモデルを直接公開しない。
- `src/Dip.Application/Features/Analysis/Dtos/ReleasePredictionDto.cs` — UI、API、外部サービスへ渡すデータ転送オブジェクトを定義し、ドメインモデルを直接公開しない。
- `src/Dip.Application/Features/Health/Commands/RecalculateProjectHealthCommand.cs` — `RecalculateProjectHealthCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/Health/Queries/GetHealthHistoryQuery.cs` — `GetHealthHistoryQuery`として読み取り専用の取得条件、Projection、返却処理を実装する。
- `src/Dip.Application/Features/Health/Queries/GetProjectHealthQuery.cs` — `GetProjectHealthQuery`として読み取り専用の取得条件、Projection、返却処理を実装する。
- `src/Dip.Application/Features/Health/Services/ProjectHealthCalculator.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `src/Dip.Application/Features/Health/Services/RiskScoreCalculator.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `src/Dip.Application/Features/Notifications/Commands/RetryNotificationCommand.cs` — `RetryNotificationCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/Notifications/Commands/SendNotificationCommand.cs` — `SendNotificationCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/Notifications/Dtos/NotificationDto.cs` — UI、API、外部サービスへ渡すデータ転送オブジェクトを定義し、ドメインモデルを直接公開しない。
- `src/Dip.Application/Features/PipelineRuns/Commands/AddMetricsCommand.cs` — `AddMetricsCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/PipelineRuns/Commands/AddQualityIssuesCommand.cs` — `AddQualityIssuesCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/PipelineRuns/Commands/CompletePipelineRunCommand.cs` — `CompletePipelineRunCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/PipelineRuns/Commands/CreatePipelineRunCommand.cs` — `CreatePipelineRunCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/PipelineRuns/Dtos/PipelineRunDto.cs` — UI、API、外部サービスへ渡すデータ転送オブジェクトを定義し、ドメインモデルを直接公開しない。
- `src/Dip.Application/Features/PipelineRuns/Queries/GetPipelineRunQuery.cs` — `GetPipelineRunQuery`として読み取り専用の取得条件、Projection、返却処理を実装する。
- `src/Dip.Application/Features/PipelineRuns/Queries/GetPipelineRunsQuery.cs` — `GetPipelineRunsQuery`として読み取り専用の取得条件、Projection、返却処理を実装する。
- `src/Dip.Application/Features/Projects/Commands/CreateProjectCommand.cs` — `CreateProjectCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/Projects/Commands/DeleteProjectCommand.cs` — `DeleteProjectCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/Projects/Commands/UpdateProjectCommand.cs` — `UpdateProjectCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/Projects/Dtos/ProjectDashboardDto.cs` — UI、API、外部サービスへ渡すデータ転送オブジェクトを定義し、ドメインモデルを直接公開しない。
- `src/Dip.Application/Features/Projects/Dtos/ProjectDto.cs` — UI、API、外部サービスへ渡すデータ転送オブジェクトを定義し、ドメインモデルを直接公開しない。
- `src/Dip.Application/Features/Projects/Queries/GetProjectDashboardQuery.cs` — `GetProjectDashboardQuery`として読み取り専用の取得条件、Projection、返却処理を実装する。
- `src/Dip.Application/Features/Projects/Queries/GetProjectQuery.cs` — `GetProjectQuery`として読み取り専用の取得条件、Projection、返却処理を実装する。
- `src/Dip.Application/Features/Projects/Queries/GetProjectsQuery.cs` — `GetProjectsQuery`として読み取り専用の取得条件、Projection、返却処理を実装する。
- `src/Dip.Application/Features/PullRequests/Commands/RecalculatePullRequestRiskCommand.cs` — `RecalculatePullRequestRiskCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/PullRequests/Commands/SyncPullRequestCommand.cs` — `SyncPullRequestCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/PullRequests/Dtos/PullRequestRiskDto.cs` — UI、API、外部サービスへ渡すデータ転送オブジェクトを定義し、ドメインモデルを直接公開しない。
- `src/Dip.Application/Features/PullRequests/Queries/GetPullRequestQuery.cs` — `GetPullRequestQuery`として読み取り専用の取得条件、Projection、返却処理を実装する。
- `src/Dip.Application/Features/PullRequests/Queries/GetPullRequestRiskQuery.cs` — `GetPullRequestRiskQuery`として読み取り専用の取得条件、Projection、返却処理を実装する。
- `src/Dip.Application/Features/Quality/Commands/EvaluateQualityGateCommand.cs` — `EvaluateQualityGateCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/Quality/Commands/ResolveQualityIssueCommand.cs` — `ResolveQualityIssueCommand`として状態を変更するユースケースの入力、検証、実行処理を実装する。
- `src/Dip.Application/Features/Quality/Dtos/QualityIssueDto.cs` — UI、API、外部サービスへ渡すデータ転送オブジェクトを定義し、ドメインモデルを直接公開しない。
- `src/Dip.Application/Features/Quality/Dtos/QualityTrendDto.cs` — UI、API、外部サービスへ渡すデータ転送オブジェクトを定義し、ドメインモデルを直接公開しない。
- `src/Dip.Application/Features/Quality/Queries/GetQualityIssuesQuery.cs` — `GetQualityIssuesQuery`として読み取り専用の取得条件、Projection、返却処理を実装する。
- `src/Dip.Application/Features/Quality/Queries/GetQualityTrendsQuery.cs` — `GetQualityTrendsQuery`として読み取り専用の取得条件、Projection、返却処理を実装する。
- `src/Dip.Application/Features/TeamLoad/Dtos/TeamLoadDto.cs` — UI、API、外部サービスへ渡すデータ転送オブジェクトを定義し、ドメインモデルを直接公開しない。
- `src/Dip.Application/Features/TeamLoad/Queries/GetMemberWorkloadQuery.cs` — `GetMemberWorkloadQuery`として読み取り専用の取得条件、Projection、返却処理を実装する。
- `src/Dip.Application/Features/TeamLoad/Queries/GetTeamLoadQuery.cs` — `GetTeamLoadQuery`として読み取り専用の取得条件、Projection、返却処理を実装する。
- `src/Dip.Application/Mapping/ApplicationMappingProfile.cs` — Entity、DTO、外部モデル間のマッピング規則を集約する。
- `src/Dip.Domain/Dip.Domain.csproj` — .NETプロジェクトのSDK、Target Framework、参照、NuGet依存、Analyzer、生成物設定を定義する。
- `src/Dip.Domain/Entities/AuditLog.cs` — `AuditLog`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Entities/Commit.cs` — `Commit`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Entities/DailySummary.cs` — `DailySummary`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Entities/Metric.cs` — `Metric`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Entities/Notification.cs` — `Notification`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Entities/PipelineRun.cs` — `PipelineRun`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Entities/Project.cs` — `Project`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Entities/ProjectMember.cs` — `ProjectMember`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Entities/PullRequest.cs` — `PullRequest`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Entities/QualityGateResult.cs` — `QualityGateResult`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Entities/QualityIssue.cs` — `QualityIssue`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Entities/QualityRule.cs` — `QualityRule`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Entities/Repository.cs` — `Repository`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Entities/TaskItem.cs` — `TaskItem`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `src/Dip.Domain/Enums/IssueStatus.cs` — `IssueStatus`として利用する有限状態・区分値を定義する。
- `src/Dip.Domain/Enums/NotificationType.cs` — `NotificationType`として利用する有限状態・区分値を定義する。
- `src/Dip.Domain/Enums/PipelineStatus.cs` — `PipelineStatus`として利用する有限状態・区分値を定義する。
- `src/Dip.Domain/Enums/ProjectRole.cs` — `ProjectRole`として利用する有限状態・区分値を定義する。
- `src/Dip.Domain/Enums/QualityGateStatus.cs` — `QualityGateStatus`として利用する有限状態・区分値を定義する。
- `src/Dip.Domain/Enums/RiskLevel.cs` — `RiskLevel`として利用する有限状態・区分値を定義する。
- `src/Dip.Domain/Enums/Severity.cs` — `Severity`として利用する有限状態・区分値を定義する。
- `src/Dip.Domain/Events/CriticalIssueDetectedEvent.cs` — 状態変化を後続処理へ通知するドメインイベントまたはメッセージを定義する。
- `src/Dip.Domain/Events/PipelineCompletedEvent.cs` — 状態変化を後続処理へ通知するドメインイベントまたはメッセージを定義する。
- `src/Dip.Domain/Events/PullRequestRiskChangedEvent.cs` — 状態変化を後続処理へ通知するドメインイベントまたはメッセージを定義する。
- `src/Dip.Domain/Events/QualityGateFailedEvent.cs` — 状態変化を後続処理へ通知するドメインイベントまたはメッセージを定義する。
- `src/Dip.Domain/Exceptions/DomainException.cs` — ドメイン不変条件違反や不正入力を表現する専用例外を定義する。
- `src/Dip.Domain/Exceptions/InvalidMetricException.cs` — ドメイン不変条件違反や不正入力を表現する専用例外を定義する。
- `src/Dip.Domain/Exceptions/InvalidProjectStateException.cs` — ドメイン不変条件違反や不正入力を表現する専用例外を定義する。
- `src/Dip.Domain/Rules/MetricThresholdRule.cs` — 品質ゲート、閾値判定、差分劣化、チーム負荷などの純粋なドメインルールを実装する。
- `src/Dip.Domain/Rules/QualityGateRule.cs` — 品質ゲート、閾値判定、差分劣化、チーム負荷などの純粋なドメインルールを実装する。
- `src/Dip.Domain/Rules/RegressionRule.cs` — 品質ゲート、閾値判定、差分劣化、チーム負荷などの純粋なドメインルールを実装する。
- `src/Dip.Domain/Rules/TeamLoadRule.cs` — 品質ゲート、閾値判定、差分劣化、チーム負荷などの純粋なドメインルールを実装する。
- `src/Dip.Domain/ValueObjects/CommitSha.cs` — `CommitSha`を不変オブジェクトとして実装し、生成時検証、等価比較、正規化を持たせる。
- `src/Dip.Domain/ValueObjects/HealthScore.cs` — `HealthScore`を不変オブジェクトとして実装し、生成時検証、等価比較、正規化を持たせる。
- `src/Dip.Domain/ValueObjects/MetricValue.cs` — `MetricValue`を不変オブジェクトとして実装し、生成時検証、等価比較、正規化を持たせる。
- `src/Dip.Domain/ValueObjects/Percentage.cs` — `Percentage`を不変オブジェクトとして実装し、生成時検証、等価比較、正規化を持たせる。
- `src/Dip.Domain/ValueObjects/RepositoryUrl.cs` — `RepositoryUrl`を不変オブジェクトとして実装し、生成時検証、等価比較、正規化を持たせる。
- `src/Dip.Domain/ValueObjects/RiskScore.cs` — `RiskScore`を不変オブジェクトとして実装し、生成時検証、等価比較、正規化を持たせる。
- `src/Dip.Infrastructure/Analysis/AnalysisClient.cs` — Python分析サービスへのHTTP要求、Timeout、再試行、応答変換を実装する。
- `src/Dip.Infrastructure/Analysis/AnalysisOptions.cs` — Python分析サービスへのHTTP要求、Timeout、再試行、応答変換を実装する。
- `src/Dip.Infrastructure/Analysis/Models/AnalysisRequest.cs` — Python分析サービスへのHTTP要求、Timeout、再試行、応答変換を実装する。
- `src/Dip.Infrastructure/Analysis/Models/AnalysisResponse.cs` — Python分析サービスへのHTTP要求、Timeout、再試行、応答変換を実装する。
- `src/Dip.Infrastructure/Authentication/AuthenticationOptions.cs` — GitHub OAuth、JWT、Role／Project Access判定、Token更新を実装する。
- `src/Dip.Infrastructure/Authentication/GitHubOAuthService.cs` — GitHub OAuth、JWT、Role／Project Access判定、Token更新を実装する。
- `src/Dip.Infrastructure/Authentication/JwtTokenService.cs` — GitHub OAuth、JWT、Role／Project Access判定、Token更新を実装する。
- `src/Dip.Infrastructure/DependencyInjection.cs` — 対象プロジェクトの実装をDIコンテナへ登録する拡張メソッドを提供する。
- `src/Dip.Infrastructure/Dip.Infrastructure.csproj` — .NETプロジェクトのSDK、Target Framework、参照、NuGet依存、Analyzer、生成物設定を定義する。
- `src/Dip.Infrastructure/GitHub/GitHubClient.cs` — GitHub APIの認証、Pagination、Rate Limit、PR・Issue・Workflow取得／コメント投稿を実装する。
- `src/Dip.Infrastructure/GitHub/GitHubOptions.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `src/Dip.Infrastructure/GitHub/GitHubWebhookParser.cs` — GitHubのイベントJSONを内部Command／Queue Messageへ変換する。
- `src/Dip.Infrastructure/GitHub/GitHubWebhookVerifier.cs` — Webhook署名と時刻を検証し、改ざん・Replayを拒否する。
- `src/Dip.Infrastructure/GitHub/Models/GitHubIssueModel.cs` — GitHub API応答を受け取る外部境界モデルを定義する。
- `src/Dip.Infrastructure/GitHub/Models/GitHubPullRequestModel.cs` — GitHub API応答を受け取る外部境界モデルを定義する。
- `src/Dip.Infrastructure/GitHub/Models/GitHubWorkflowRunModel.cs` — GitHub API応答を受け取る外部境界モデルを定義する。
- `src/Dip.Infrastructure/Notifications/EmailNotificationClient.cs` — GitHub、Slack、Emailなどへの通知送信または通知ユースケースを実装する。
- `src/Dip.Infrastructure/Notifications/GitHubCommentNotificationClient.cs` — GitHub、Slack、Emailなどへの通知送信または通知ユースケースを実装する。
- `src/Dip.Infrastructure/Notifications/NotificationOptions.cs` — GitHub、Slack、Emailなどへの通知送信または通知ユースケースを実装する。
- `src/Dip.Infrastructure/Notifications/SlackNotificationClient.cs` — GitHub、Slack、Emailなどへの通知送信または通知ユースケースを実装する。
- `src/Dip.Infrastructure/Observability/LoggingExtensions.cs` — OpenTelemetry Trace、Metric、構造化Log、Correlation IDを設定する。
- `src/Dip.Infrastructure/Observability/MetricsRegistry.cs` — OpenTelemetry Trace、Metric、構造化Log、Correlation IDを設定する。
- `src/Dip.Infrastructure/Observability/OpenTelemetryExtensions.cs` — OpenTelemetry Trace、Metric、構造化Log、Correlation IDを設定する。
- `src/Dip.Infrastructure/Persistence/Configurations/MetricConfiguration.cs` — Entity Framework Core Fluent APIでテーブル、列、制約、Index、関連、Cascade動作を定義する。
- `src/Dip.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs` — Entity Framework Core Fluent APIでテーブル、列、制約、Index、関連、Cascade動作を定義する。
- `src/Dip.Infrastructure/Persistence/Configurations/PipelineRunConfiguration.cs` — Entity Framework Core Fluent APIでテーブル、列、制約、Index、関連、Cascade動作を定義する。
- `src/Dip.Infrastructure/Persistence/Configurations/ProjectConfiguration.cs` — Entity Framework Core Fluent APIでテーブル、列、制約、Index、関連、Cascade動作を定義する。
- `src/Dip.Infrastructure/Persistence/Configurations/PullRequestConfiguration.cs` — Entity Framework Core Fluent APIでテーブル、列、制約、Index、関連、Cascade動作を定義する。
- `src/Dip.Infrastructure/Persistence/Configurations/QualityIssueConfiguration.cs` — Entity Framework Core Fluent APIでテーブル、列、制約、Index、関連、Cascade動作を定義する。
- `src/Dip.Infrastructure/Persistence/DbInitializer.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `src/Dip.Infrastructure/Persistence/DipDbContext.cs` — Entity Framework CoreのDbSet、モデル構築、接続、監査列更新などを定義する。
- `src/Dip.Infrastructure/Persistence/Migrations/.gitkeep` — Entity Framework Coreが生成するDBスキーマ変更履歴を格納する。
- `src/Dip.Infrastructure/Persistence/Repositories/MetricRepository.cs` — Repository抽象の永続化実装、Query最適化、Tracking方針を実装する。
- `src/Dip.Infrastructure/Persistence/Repositories/PipelineRunRepository.cs` — Repository抽象の永続化実装、Query最適化、Tracking方針を実装する。
- `src/Dip.Infrastructure/Persistence/Repositories/ProjectRepository.cs` — Repository抽象の永続化実装、Query最適化、Tracking方針を実装する。
- `src/Dip.Infrastructure/Persistence/Repositories/QualityIssueRepository.cs` — Repository抽象の永続化実装、Query最適化、Tracking方針を実装する。
- `src/Dip.Infrastructure/Queue/QueueMessageSerializer.cs` — Redisを利用するQueue、Message Serialization、重複排除、再試行、Dead Letter処理を実装する。
- `src/Dip.Infrastructure/Queue/QueueOptions.cs` — Redisを利用するQueue、Message Serialization、重複排除、再試行、Dead Letter処理を実装する。
- `src/Dip.Infrastructure/Queue/RedisQueueClient.cs` — Redisを利用するQueue、Message Serialization、重複排除、再試行、Dead Letter処理を実装する。
- `src/Dip.Infrastructure/Resilience/CircuitBreakerPolicies.cs` — 外部通信のTimeout、Retry、Circuit Breaker、Bulkhead方針を定義する。
- `src/Dip.Infrastructure/Resilience/RetryPolicies.cs` — 外部通信のTimeout、Retry、Circuit Breaker、Bulkhead方針を定義する。
- `src/Dip.Infrastructure/Storage/LocalObjectStorage.cs` — MinIO／S3互換またはローカルへのArtifact保存、取得、署名URL生成を実装する。
- `src/Dip.Infrastructure/Storage/MinioObjectStorage.cs` — MinIO／S3互換またはローカルへのArtifact保存、取得、署名URL生成を実装する。
- `src/Dip.Infrastructure/Storage/StorageOptions.cs` — MinIO／S3互換またはローカルへのArtifact保存、取得、署名URL生成を実装する。
- `src/Dip.Infrastructure/Time/SystemClock.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `src/Dip.Shared/Constants/HeaderNames.cs` — Metric名、Queue名、Header名など、文字列の揺れを防ぐ定数を定義する。
- `src/Dip.Shared/Constants/MetricNames.cs` — Metric名、Queue名、Header名など、文字列の揺れを防ぐ定数を定義する。
- `src/Dip.Shared/Constants/QueueNames.cs` — Metric名、Queue名、Header名など、文字列の揺れを防ぐ定数を定義する。
- `src/Dip.Shared/Contracts/Api/ApiError.cs` — API共通のError、Pagination、Health Response形式を定義する。
- `src/Dip.Shared/Contracts/Api/HealthResponse.cs` — API共通のError、Pagination、Health Response形式を定義する。
- `src/Dip.Shared/Contracts/Api/PagedResponse.cs` — API共通のError、Pagination、Health Response形式を定義する。
- `src/Dip.Shared/Contracts/Collectors/CollectorArtifact.cs` — 全Collectorが出力する共通Report、Metric、Issue、Artifact契約を定義する。
- `src/Dip.Shared/Contracts/Collectors/CollectorIssue.cs` — 全Collectorが出力する共通Report、Metric、Issue、Artifact契約を定義する。
- `src/Dip.Shared/Contracts/Collectors/CollectorMetric.cs` — 全Collectorが出力する共通Report、Metric、Issue、Artifact契約を定義する。
- `src/Dip.Shared/Contracts/Collectors/CollectorReport.cs` — 全Collectorが出力する共通Report、Metric、Issue、Artifact契約を定義する。
- `src/Dip.Shared/Contracts/Events/PipelineCompletedMessage.cs` — 状態変化を後続処理へ通知するドメインイベントまたはメッセージを定義する。
- `src/Dip.Shared/Contracts/Events/QualityGateFailedMessage.cs` — 状態変化を後続処理へ通知するドメインイベントまたはメッセージを定義する。
- `src/Dip.Shared/Dip.Shared.csproj` — .NETプロジェクトのSDK、Target Framework、参照、NuGet依存、Analyzer、生成物設定を定義する。
- `src/Dip.Shared/Serialization/SharedJsonOptions.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `src/Dip.Web/App.razor` — `App`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/Common/HealthScoreCard.razor` — `HealthScoreCard`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/Common/LoadingPanel.razor` — `LoadingPanel`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/Common/MetricCard.razor` — `MetricCard`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/Common/RiskBadge.razor` — `RiskBadge`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/Common/SeverityBadge.razor` — `SeverityBadge`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/Common/TrendChart.razor` — `TrendChart`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/Layout/MainLayout.razor` — `MainLayout`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/Layout/NavMenu.razor` — `NavMenu`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/Layout/TopBar.razor` — `TopBar`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/Projects/ProjectHealthPanel.razor` — `ProjectHealthPanel`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/Projects/ProjectSummary.razor` — `ProjectSummary`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/PullRequests/PullRequestMetricsTable.razor` — `PullRequestMetricsTable`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/PullRequests/PullRequestRiskPanel.razor` — `PullRequestRiskPanel`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Components/Team/TeamLoadPanel.razor` — `TeamLoadPanel`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Dip.Web.csproj` — .NETプロジェクトのSDK、Target Framework、参照、NuGet依存、Analyzer、生成物設定を定義する。
- `src/Dip.Web/Pages/Home.razor` — `Home`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Pages/Login.razor` — `Login`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Pages/Notifications.razor` — `Notifications`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Pages/PipelineRunDetails.razor` — `PipelineRunDetails`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Pages/PipelineRuns.razor` — `PipelineRuns`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Pages/ProjectDashboard.razor` — `ProjectDashboard`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Pages/Projects.razor` — `Projects`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Pages/PullRequestDetails.razor` — `PullRequestDetails`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Pages/PullRequests.razor` — `PullRequests`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Pages/QualityIssues.razor` — `QualityIssues`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Pages/QualityTrends.razor` — `QualityTrends`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Pages/ReleaseReadiness.razor` — `ReleaseReadiness`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Pages/Settings.razor` — `Settings`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Pages/TeamLoad.razor` — `TeamLoad`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Program.cs` — プロセス起動、設定読込、DI登録、Middleware、Endpoint、Graceful Shutdownを構成する。
- `src/Dip.Web/Routes.razor` — `Routes`画面または再利用可能なBlazor UIコンポーネントを実装する。
- `src/Dip.Web/Services/ApiClient.cs` — BlazorからAPI、認証、Dashboard、SignalR更新を利用するクライアントサービスを実装する。
- `src/Dip.Web/Services/AuthenticationService.cs` — BlazorからAPI、認証、Dashboard、SignalR更新を利用するクライアントサービスを実装する。
- `src/Dip.Web/Services/ProjectDashboardService.cs` — BlazorからAPI、認証、Dashboard、SignalR更新を利用するクライアントサービスを実装する。
- `src/Dip.Web/Services/RealtimeUpdateService.cs` — BlazorからAPI、認証、Dashboard、SignalR更新を利用するクライアントサービスを実装する。
- `src/Dip.Web/State/ProjectState.cs` — 画面横断で共有するProject／User状態を管理し、変更通知を提供する。
- `src/Dip.Web/State/UserState.cs` — 画面横断で共有するProject／User状態を管理し、変更通知を提供する。
- `src/Dip.Web/appsettings.json` — 接続先、ログレベル、認証、Queue、Storage、外部サービスなど非秘密設定を書く。
- `src/Dip.Web/wwwroot/css/app.css` — DashboardのLayout、Color、Typography、Responsive表示、状態表現を定義する。
- `src/Dip.Web/wwwroot/css/dashboard.css` — DashboardのLayout、Color、Typography、Responsive表示、状態表現を定義する。
- `src/Dip.Web/wwwroot/favicon.png` — Web Dashboardで使用するFavicon画像を配置する。
- `src/Dip.Web/wwwroot/images/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `src/Dip.Web/wwwroot/js/charts.js` — Chart連携、ブラウザAPI、k6負荷試験などJavaScriptが必要な処理を実装する。
- `src/Dip.Worker/Dip.Worker.csproj` — .NETプロジェクトのSDK、Target Framework、参照、NuGet依存、Analyzer、生成物設定を定義する。
- `src/Dip.Worker/Handlers/CollectorReportHandler.cs` — Webhook、Collector Report、Quality Gate結果などのMessage処理を実装する。
- `src/Dip.Worker/Handlers/PullRequestWebhookHandler.cs` — Webhook、Collector Report、Quality Gate結果などのMessage処理を実装する。
- `src/Dip.Worker/Handlers/QualityGateResultHandler.cs` — Webhook、Collector Report、Quality Gate結果などのMessage処理を実装する。
- `src/Dip.Worker/Handlers/WorkflowRunWebhookHandler.cs` — Webhook、Collector Report、Quality Gate結果などのMessage処理を実装する。
- `src/Dip.Worker/Program.cs` — プロセス起動、設定読込、DI登録、Middleware、Endpoint、Graceful Shutdownを構成する。
- `src/Dip.Worker/Scheduling/DailySummaryScheduler.cs` — 日次要約、Health Score再計算などの定期処理を登録する。
- `src/Dip.Worker/Scheduling/HealthRecalculationScheduler.cs` — 日次要約、Health Score再計算などの定期処理を登録する。
- `src/Dip.Worker/Workers/AnalysisRequestWorker.cs` — `AnalysisRequestWorker`としてQueue消費、定期実行、再試行、Dead Letter連携を実装する。
- `src/Dip.Worker/Workers/DailySummaryWorker.cs` — `DailySummaryWorker`としてQueue消費、定期実行、再試行、Dead Letter連携を実装する。
- `src/Dip.Worker/Workers/DeadLetterWorker.cs` — `DeadLetterWorker`としてQueue消費、定期実行、再試行、Dead Letter連携を実装する。
- `src/Dip.Worker/Workers/GitHubWebhookWorker.cs` — `GitHubWebhookWorker`としてQueue消費、定期実行、再試行、Dead Letter連携を実装する。
- `src/Dip.Worker/Workers/NotificationWorker.cs` — `NotificationWorker`としてQueue消費、定期実行、再試行、Dead Letter連携を実装する。
- `src/Dip.Worker/Workers/PipelineProcessingWorker.cs` — `PipelineProcessingWorker`としてQueue消費、定期実行、再試行、Dead Letter連携を実装する。
- `src/Dip.Worker/Workers/QualityEvaluationWorker.cs` — `QualityEvaluationWorker`としてQueue消費、定期実行、再試行、Dead Letter連携を実装する。
- `src/Dip.Worker/appsettings.json` — 接続先、ログレベル、認証、Queue、Storage、外部サービスなど非秘密設定を書く。
- `tests/Dip.Application.Tests/Dip.Application.Tests.csproj` — .NETプロジェクトのSDK、Target Framework、参照、NuGet依存、Analyzer、生成物設定を定義する。
- `tests/Dip.Application.Tests/Health/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `tests/Dip.Application.Tests/PullRequests/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `tests/Dip.Application.Tests/Quality/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj` — .NETプロジェクトのSDK、Target Framework、参照、NuGet依存、Analyzer、生成物設定を定義する。
- `tests/Dip.Domain.Tests/Entities/.gitkeep` — `.gitkeep`の識別子、状態、関連、ドメイン上の不変条件、状態変更メソッドを実装する。
- `tests/Dip.Domain.Tests/Rules/.gitkeep` — 品質ゲート、閾値判定、差分劣化、チーム負荷などの純粋なドメインルールを実装する。
- `tests/Dip.Domain.Tests/ValueObjects/.gitkeep` — `.gitkeep`を不変オブジェクトとして実装し、生成時検証、等価比較、正規化を持たせる。
- `tests/Dip.EndToEndTests/Dip.EndToEndTests.csproj` — .NETプロジェクトのSDK、Target Framework、参照、NuGet依存、Analyzer、生成物設定を定義する。
- `tests/Dip.EndToEndTests/Scenarios/DailySummaryScenario.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `tests/Dip.EndToEndTests/Scenarios/PullRequestPipelineScenario.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `tests/Dip.EndToEndTests/Scenarios/QualityGateFailureScenario.cs` — ファイル名が示す機能、設定、試験、Documentationを実装する。
- `tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj` — .NETプロジェクトのSDK、Target Framework、参照、NuGet依存、Analyzer、生成物設定を定義する。
- `tests/Dip.Infrastructure.Tests/GitHub/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `tests/Dip.Infrastructure.Tests/Persistence/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `tests/Dip.Infrastructure.Tests/Queue/.gitkeep` — Redisを利用するQueue、Message Serialization、重複排除、再試行、Dead Letter処理を実装する。
- `tests/Dip.IntegrationTests/Analysis/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `tests/Dip.IntegrationTests/Api/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `tests/Dip.IntegrationTests/Database/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `tests/Dip.IntegrationTests/Dip.IntegrationTests.csproj` — .NETプロジェクトのSDK、Target Framework、参照、NuGet依存、Analyzer、生成物設定を定義する。
- `tests/Dip.IntegrationTests/Redis/.gitkeep` — 空DirectoryをGitで維持するためのPlaceholder。
- `tests/load/k6/dashboard-load.js` — Chart連携、ブラウザAPI、k6負荷試験などJavaScriptが必要な処理を実装する。
- `tests/load/k6/pipeline-load.js` — Chart連携、ブラウザAPI、k6負荷試験などJavaScriptが必要な処理を実装する。
- `tests/load/k6/webhook-load.js` — Chart連携、ブラウザAPI、k6負荷試験などJavaScriptが必要な処理を実装する。

---

## 25. 最初に実装するファイル

最初の実装対象は次に限定します。

```text
src/Dip.Domain/Entities/Project.cs
src/Dip.Domain/Entities/PipelineRun.cs
src/Dip.Domain/Entities/Metric.cs
src/Dip.Domain/Entities/QualityIssue.cs

src/Dip.Infrastructure/Persistence/DipDbContext.cs
src/Dip.Infrastructure/Persistence/Configurations/*
src/Dip.Api/Controllers/CollectorsController.cs
src/Dip.Api/Program.cs

src/Dip.Web/Pages/ProjectDashboard.razor
schemas/collector-report.schema.json
docker-compose.yml
```

この最短経路が完成するまで、OAuth、AI、Unity、Maya、C++ Collectorへ広げません。
