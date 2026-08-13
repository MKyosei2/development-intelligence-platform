# Development Intelligence Platform

**Development Intelligence Platform（DIP）** は、ソフトウェア・ゲーム開発に散在する情報を一つに集約し、**プログラムや専門ツールに詳しくないPM・プロデューサー・ディレクター・プランナー・リードアーティストなどでも、プロジェクトの現状を把握し、合理的な判断を行えるようにする開発マネジメント支援プラットフォーム**です。

GitHub、CI/CD、タスク、マイルストーン、QA、ゲームエンジン、アセット、性能計測、Excel / Google Sheetsなどの情報を統合し、

* 今どこまで進んでいるか
* どの工程が詰まっているか
* 何がリリースを遅らせているか
* どのチームが過負荷か
* どの問題を優先して解決すべきか
* 仕様変更が納期にどの程度影響するか
* どの判断を誰が行う必要があるか
* 現在のプロジェクトが安全な状態か

を、コードを直接読まなくても理解できる形へ変換します。

---

## 目的

ソフトウェア開発では、プロジェクトの状態を判断するための情報が複数の場所へ分散します。

例えば、

* GitHubにはコード変更
* CI/CDにはBuild結果
* Issue Trackerにはタスク
* Excelにはスケジュール
* Google Sheetsには企画・進捗
* QAツールにはテスト結果
* Unity / Unreal Engineにはゲーム固有情報
* アーティスト用ツールにはアセット情報

が存在します。

その結果、管理職がプロジェクト全体を把握するためには、それぞれのツールを確認し、さらに専門的な情報を理解する必要があります。

DIPではこれらを自動的に関連付け、

```text
Development Data
        ↓
Development Graph
        ↓
Management Intelligence
        ↓
Management Decision
```

という形に変換します。

DIPの中心はQAシステムではありません。

QAは、

```text
Git
CI/CD
QA
Assets
Performance
Incidents
Work Items
```

などと同じく、**プロジェクト状態を判断するためのEvidence Providerの一つ**として扱います。

---

# 想定利用者

主な利用者は、開発プロジェクト全体を判断・管理する立場です。

### Project Manager

スケジュール、進捗、依存関係、リスク、リソース配分を確認します。

### Producer / Director

プロジェクト全体のHealth、Scope、Release可能性、重要判断を確認します。

### Planner

Requirement、Feature、Milestone、Scope Changeを管理します。

### Lead Engineer

技術的な変更影響、依存関係、Review、CI、Release Riskを確認します。

### Lead Artist

Asset制作工程、Workload、Blocker、依存工程を確認します。

### QA Lead

品質情報を管理しますが、QA結果自体ではなくFeatureやReleaseへの影響として扱います。

---

# DIPが解決する課題

## 1. 本当の進捗が分からない

単純な「80%完了」という自己申告だけではなく、

```text
Reported Progress
80%

Evidence-backed Progress
61%
```

のように、実際のEvidenceと照合します。

---

## 2. チームのどこが詰まっているか分からない

Team / Workstream単位でCapacityを分析します。

```text
Gameplay      82%
Character     91%
Online       124%  OVER CAPACITY
QA            76%
```

個人の生産性スコアではなく、**工程・チーム単位の負荷**を扱います。

---

## 3. 遅延理由が分からない

Task、Blocker、Dependency、Critical Pathを関連付け、

```text
Server API
    ↓
Client Integration
    ↓
Multiplayer Test
    ↓
Release
```

のように、何がReleaseを止めているかを表示します。

---

## 4. 仕様変更の影響が分からない

Change Requestから、

```text
Engineering +120h
Art          +40h
QA           +32h

Schedule Impact
+6 days

Risk
MEDIUM → HIGH
```

のように影響を分析します。

---

## 5. 技術情報を管理職が理解しにくい

例えば、

```text
heap-use-after-free
PlayerEntity.cpp:483
```

をそのまま表示するのではなく、

```text
重大な実行時エラー

影響:
Battle機能でクラッシュする可能性があります。

Release Impact:
BLOCKER

推奨:
修正完了までRelease候補から除外
```

のような管理判断向け情報へ変換します。

---

# 主な機能

## Management Intelligence

### Team Workload / Capacity

Team / WorkstreamごとのCapacity、予定工数、残工数、過負荷状態を分析します。

### Task / Workstream Management

Project → Workstream → Taskという構造で開発工程を管理します。

### Schedule / Gantt

TaskやMilestoneを時系列で管理し、予定と予測完了日を比較します。

### Critical Path

どのTaskの遅延がプロジェクト全体の遅延につながるかを分析します。

### Blocker Management

作業停止理由、停止期間、影響Task、影響Milestoneを管理します。

### Dependency Management

Task、Workstream、Repository、Feature間の依存関係を管理します。

### Milestone Management

Alpha、Beta、Release Candidateなどの達成可能性を分析します。

### Requirement / Scope Management

何を作る予定なのか、RequirementとScopeを管理します。

---

# Risk / Decision Management

## Risk Register

まだ発生していない問題を、

* Probability
* Impact
* Trigger
* Mitigation
* Owner
* Review Date

として管理します。

## Decision / Action Management

問題発見後の、

```text
Problem
 ↓
Decision
 ↓
Action
 ↓
Owner
 ↓
Deadline
 ↓
Result
```

を記録します。

## Resource Allocation

Workstream間のCapacity移動を分析します。

## Change Request / Scope Change

仕様変更による工数、Schedule、Riskへの影響を追跡します。

## Notification / Escalation

長期間放置されたBlocker、期限超過Action、過負荷、重大Riskなどを自動的にManagement Alertへ変換します。

---

# Project Health

DIPではプロジェクト全体を、

```text
PROJECT HEALTH

Schedule        🟡
Scope           🟢
Capacity        🔴
Dependencies    🟡
Blockers        🔴
Risk            🟡
Quality         🟢
```

のように表示します。

単一のブラックボックスScoreだけではなく、

```text
Why?

- Online Workstream: 124% load
- Critical Blocker: 2
- Beta forecast: +6 days
- Scope increase: +14%
```

のように理由を表示します。

---

# Development Graph

DIPの中心となるデータモデルです。

```text
Project
 ├─ Repository
 ├─ Workstream
 ├─ Task
 ├─ Feature
 ├─ Requirement
 ├─ Milestone
 ├─ Risk
 ├─ Build
 ├─ Test
 ├─ Asset
 ├─ Incident
 └─ Release
```

これらを、

```text
DependsOn
Blocks
Affects
Impacts
TestedBy
VerifiedBy
BuiltBy
Requires
Implements
```

などのEdgeで接続します。

これにより、

**Change → Impact → Risk → Schedule → Release → Decision**

を同じデータモデル上で分析できます。

---

# Spreadsheet Integration

ExcelとGoogle Sheetsを、単なるExport先ではなく、**非技術職がDIPを操作するための入力インターフェース**として扱います。

## Excel

`.xlsx`ファイルを生成・再取込できます。

主な編集シート:

```text
Teams
Workstreams
Tasks
Milestones
Blockers
Requirements
Dependencies
Risks
Decisions
Actions
ChangeRequests
ResourceAllocations
```

DIPが自動生成するシート:

```text
Executive
Capacity
Workload
Schedule
Gantt
Scope
ResourceRecommendations
Escalations
ManagementSummary
```

## Google Sheets

Google Sheets REST APIを利用し、DIPとの双方向同期を行います。

---

# Development / Technical Intelligence

Management判断を支えるために、開発側のデータも取得します。

## GitHub

* Commit
* Branch
* Pull Request
* Review
* Workflow
* Release
* Deployment

をDevelopment Graphへ取り込みます。

## Perforce

Perforce Changelistを取得し、変更履歴として扱います。

## Jenkins / External CI

外部CIのBuild結果をDevelopment Graphへ統合します。

## Change Impact / Blast Radius

コード変更が、

* Feature
* Test
* Asset
* Repository
* Release

へ与える影響を分析します。

## Review Intelligence

Pull RequestのReview状態、変更量、Test変更、Asset変更などを判断材料へ変換します。

## Asset Intelligence

AssetのImport Failure、参照切れ、大容量Asset、重複Assetなどを分析します。

---

# QA

現在のQA対象言語は、

* Python
* C++
* C#

です。

QAはManagement Intelligenceの補助Evidenceとして利用します。

例えば、

```text
QA Failure
 ↓
Affected Feature
 ↓
Affected Milestone
 ↓
Release Risk
 ↓
Management Action
```

へ変換します。

---

# Release Readiness

Release可能性を、

* Management Risk
* Milestone
* Requirement
* Blocker
* Action
* CI
* QA
* Incident
* Performance
* Asset
* Evidence

から総合的に評価します。

---

# Architecture

```text
GitHub / Perforce
CI / Jenkins
Excel / Google Sheets
Unity / Unreal Engine
Native Collector
QA / Performance
        │
        ▼
┌────────────────────────────┐
│        Dip.Api             │
│      ASP.NET Core          │
└─────────────┬──────────────┘
              │
              ▼
┌────────────────────────────┐
│      Dip.Application       │
│                            │
│ Management Intelligence    │
│ Development Graph          │
│ Risk / Decision            │
│ Project Control            │
│ QA / Release Intelligence  │
└─────────────┬──────────────┘
              │
              ▼
┌────────────────────────────┐
│     Dip.Infrastructure     │
│                            │
│ PostgreSQL                 │
│ Redis                      │
│ GitHub / Perforce          │
│ Jenkins                    │
│ Google Sheets              │
│ Artifact Storage           │
└─────────────┬──────────────┘
              │
              ▼
┌────────────────────────────┐
│          Dip.Web           │
│      Blazor Dashboard      │
└────────────────────────────┘
```

---

# Technology

| 分類               | 技術                            |
| ---------------- | ----------------------------- |
| Backend          | C# / ASP.NET Core             |
| Frontend         | Blazor                        |
| Database         | PostgreSQL                    |
| Queue            | Redis                         |
| ORM              | Entity Framework Core         |
| Analysis         | Python / FastAPI              |
| Native Collector | C++20                         |
| Spreadsheet      | Excel `.xlsx` / Google Sheets |
| Container        | Docker                        |
| CI/CD            | GitHub Actions / Jenkins      |
| Version Control  | GitHub / Perforce             |

---

# Solution構成

| Project                       | 役割                                 |
| ----------------------------- | ---------------------------------- |
| `Dip.Domain`                  | Entity、Enum、Value Objectなど中核Domain |
| `Dip.Shared`                  | API・Worker・Collector間で共有するContract |
| `Dip.Application`             | Management Intelligenceや各種Use Case |
| `Dip.Infrastructure`          | PostgreSQL、Redis、外部サービス実装          |
| `Dip.Api`                     | ASP.NET Core HTTP API              |
| `Dip.Worker`                  | Background Processing              |
| `Dip.Web`                     | Blazor Dashboard                   |
| `services/python-analysis`    | Python分析サービス                       |
| `collectors/native-collector` | Native/C++解析                       |
| `collectors/unity-collector`  | Unity情報取得                          |
| `collectors/unreal-collector` | Unreal Engine情報取得                  |

---

# Source Guide

以下は主要ソースファイルの役割です。

---

## `Dip.Application`

DIPの中心となるApplication Layerです。

### Management

| File                              | 内容                                            |
| --------------------------------- | --------------------------------------------- |
| `ManagementContracts.cs`          | Management機能で使用するCommand・DTO・Resultを定義        |
| `WorkManagementService.cs`        | Team、Workstream、Task、Blocker、Requirementなどを管理 |
| `ManagementAnalyticsService.cs`   | Capacity、Schedule、Risk、Project Healthなどを分析    |
| `ManagementSpreadsheetService.cs` | Excel / Google SheetsとのManagement Data同期      |
| `ManagementXlsxCodec.cs`          | Management Workbookの`.xlsx`生成・読込              |

### Dashboard

| File                                | 内容                                       |
| ----------------------------------- | ---------------------------------------- |
| `ManagementDashboardService.cs`     | 管理職向けDashboard情報を生成                      |
| `ProjectControlDashboardService.cs` | Project Control全体の状態を集約                  |
| `IntelligenceDashboardService.cs`   | Development Intelligence情報をDashboard用に集約 |
| `DashboardQueryService.cs`          | Dashboardの基本Queryを処理                     |

### Development Graph

| File                                 | 内容                                   |
| ------------------------------------ | ------------------------------------ |
| `DevelopmentGraphService.cs`         | Development GraphのNode / Edgeを管理     |
| `DevelopmentGraphContracts.cs`       | Development Graph用DTOを定義             |
| `ProjectControlTestImpactService.cs` | Test Impact結果をDevelopment Graphへ関連付け |

### Change / Risk

| File                               | 内容                          |
| ---------------------------------- | --------------------------- |
| `ChangeImpactAnalysisService.cs`   | 変更のBlast Radiusと影響範囲を分析     |
| `ChangeRiskIntelligenceService.cs` | Change Riskを評価              |
| `WhatIfSimulationService.cs`       | ScopeやCapacity変更時のWhat-if分析 |

### Milestone / Feature

| File                              | 内容                                |
| --------------------------------- | --------------------------------- |
| `MilestoneIntelligenceService.cs` | MilestoneとCritical Pathを分析        |
| `FeatureReadinessService.cs`      | Feature完成度とAcceptance Evidenceを評価 |

### Decisions

| File                    | 内容                 |
| ----------------------- | ------------------ |
| `DecisionLogService.cs` | 意思決定理由とEvidenceを記録 |

### Repository

| File                             | 内容                    |
| -------------------------------- | --------------------- |
| `MultiRepositoryGraphService.cs` | 複数Repository間の依存関係を管理 |

### GitHub / Integration

| File                                | 内容                            |
| ----------------------------------- | ----------------------------- |
| `GitHubWebhookProcessor.cs`         | GitHub Webhookを処理             |
| `GitHubDevelopmentGraphIngestor.cs` | GitHub情報をDevelopment Graphへ変換 |
| `VersionControlSyncService.cs`      | GitHub / Perforce変更情報を同期      |
| `ExternalCiIntegrationService.cs`   | Jenkins等の外部CI情報を同期            |
| `WorkItemService.cs`                | 外部Work ItemをDIPへ関連付け          |

### Release

| File                         | 内容                           |
| ---------------------------- | ---------------------------- |
| `ReleaseReadinessService.cs` | Release可能性を総合評価              |
| `ReleaseEvidenceService.cs`  | Release EvidenceとSBOM等を生成・保存 |

### Project Truth

| File                             | 内容                          |
| -------------------------------- | --------------------------- |
| `ProjectTruthService.cs`         | EvidenceからProject状態を生成      |
| `ProjectTruthEvaluator.cs`       | Project Truth全体を評価          |
| `ProgressEvaluator.cs`           | Evidence-backed Progressを評価 |
| `ProjectRiskEvaluator.cs`        | Project Riskを評価             |
| `ProjectBlockerEvaluator.cs`     | Project Blockerを評価          |
| `ClaimVerificationEvaluator.cs`  | ClaimとEvidenceを照合           |
| `EvidenceConfidenceEvaluator.cs` | Evidenceの信頼度を評価             |
| `PmEvidenceTranslator.cs`        | 技術EvidenceをPM向け表現へ変換        |
| `ProjectClaimService.cs`         | Project Claimを登録・管理         |
| `ProjectTruthModels.cs`          | Project Truth関連DTOを定義       |

### Review / Asset / Incident

| File                            | 内容                       |
| ------------------------------- | ------------------------ |
| `ReviewIntelligenceService.cs`  | Pull Request Review状態を分析 |
| `AssetIntelligenceService.cs`   | Asset状態・Import・依存関係を分析   |
| `IncidentAggregationService.cs` | 複数の障害情報をIncidentとして集約    |
| `RegressionTriageService.cs`    | Regressionの分類と優先順位付け     |

### QA

| File                        | 内容                            |
| --------------------------- | ----------------------------- |
| `QaOrchestrationService.cs` | Python / C# / C++ QAを統括       |
| `QaContracts.cs`            | QAのCommand / DTOを定義           |
| `QaFingerprint.cs`          | QA Findingの重複識別Fingerprintを生成 |
| `QualityGateEvaluator.cs`   | Quality Gate判定                |
| `QualityRuleEvaluator.cs`   | Quality Ruleを評価               |

### Intelligence

| File                              | 内容                                      |
| --------------------------------- | --------------------------------------- |
| `ForecastAndDecisionService.cs`   | Completion ForecastとDecision Supportを生成 |
| `P2OrchestrationService.cs`       | P2 Intelligence処理を統括                    |
| `P2AnalysisService.cs`            | P2分析を実行                                 |
| `RootCauseIntelligenceService.cs` | Root Cause候補を分析                         |
| `CommunityHardwareService.cs`     | Hardware情報を集約                           |
| `AdaptiveQaPlanner.cs`            | リスクに応じたQA計画を生成                          |
| `TestStrengthContracts.cs`        | Test Strength評価モデルを定義                   |
| `P2Contracts.cs`                  | P2 Intelligence DTOを定義                  |
| `P2Abstractions.cs`               | P2 Intelligence Interfaceを定義            |

### P2 Intelligence

| File                                      | 内容                                          |
| ----------------------------------------- | ------------------------------------------- |
| `ProjectDecisionIntelligenceService.cs`   | Project Decision向けTechnical Intelligenceを生成 |
| `P2TechnicalIntelligenceService.cs`       | Test Impact等のTechnical Intelligenceを生成      |
| `P2EvidencePolicies.cs`                   | Evidence採用ポリシーを定義                           |
| `AdaptiveQaPlanner.cs`                    | P2向けQA計画を生成                                 |
| `HardwareCommunityIntelligenceService.cs` | Community Hardware情報を分析                     |
| `EvidenceGroundedRootCauseService.cs`     | Evidenceに基づくRoot Cause分析                    |
| `P2Contracts.cs`                          | P2関連Contractを定義                             |

### Performance / Hardware

| File                              | 内容                               |
| --------------------------------- | -------------------------------- |
| `PerformanceBudgetService.cs`     | PlatformごとのPerformance Budgetを評価 |
| `HardwareBacktestingService.cs`   | Hardware予測結果を実測値で検証              |
| `HardwareOrchestrationService.cs` | Hardware分析全体を統括                  |
| `HardwareContracts.cs`            | Hardware分析DTOを定義                 |

### Replay

| File                   | 内容                        |
| ---------------------- | ------------------------- |
| `GameReplayService.cs` | Game Regression Replayを管理 |

### Benchmark

| File                               | 内容                |
| ---------------------------------- | ----------------- |
| `BenchmarkOrchestrationService.cs` | Benchmark実行と比較を統括 |
| `BenchmarkContracts.cs`            | Benchmark DTOを定義  |

### Artifact

| File                 | 内容                            |
| -------------------- | ----------------------------- |
| `ArtifactService.cs` | Build、QA、Release等のArtifactを保存 |

### Collector

| File                          | 内容                       |
| ----------------------------- | ------------------------ |
| `CollectorReportService.cs`   | Collectorから送られたReportを処理 |
| `CollectorReportValidator.cs` | Collector Reportを検証      |

### Project

| File                | 内容             |
| ------------------- | -------------- |
| `ProjectService.cs` | Project基本情報を管理 |

### Job

| File                     | 内容                |
| ------------------------ | ----------------- |
| `DurableJobs.cs`         | 再実行可能な長時間Jobを定義   |
| `P2JobEnqueueService.cs` | P2処理をJob Queueへ登録 |

### Application共通

| File                            | 内容                           |
| ------------------------------- | ---------------------------- |
| `OutboxWriter.cs`               | Outbox Messageを書き込む          |
| `ValidationBehavior.cs`         | Request Validation Pipeline  |
| `LoggingBehavior.cs`            | Application Logging Pipeline |
| `TransactionBehavior.cs`        | Transaction管理                |
| `ApplicationMappingProfile.cs`  | Application Object Mapping   |
| `DependencyInjection.cs`        | Application ServiceをDI登録     |
| `DependencyInjection.GitHub.cs` | GitHub関連ServiceをDI登録         |

---

## `Dip.Infrastructure`

Applicationで定義したInterfaceの実装を持ちます。

### Spreadsheet

| File                           | 内容                         |
| ------------------------------ | -------------------------- |
| `GoogleSheetsRestConnector.cs` | Google Sheets REST APIとの同期 |

### Version Control / CI

| File                                 | 内容                       |
| ------------------------------------ | ------------------------ |
| `GitHubVersionControlConnector.cs`   | GitHubからCommit / PR情報を取得 |
| `PerforceVersionControlConnector.cs` | Perforce Changelistを取得   |
| `JenkinsCiConnector.cs`              | Jenkins Build情報を取得       |
| `ExternalIntegrationOptions.cs`      | 外部Integration設定          |

### GitHub

| File                       | 内容                  |
| -------------------------- | ------------------- |
| `GitHubWebhookVerifier.cs` | GitHub Webhook署名を検証 |
| `GitHubOptions.cs`         | GitHub接続設定          |

### Quality Automation

| File                            | 内容               |
| ------------------------------- | ---------------- |
| `PythonQaEngine.cs`             | Python QAを実行     |
| `CSharpQaEngine.cs`             | C# QAを実行         |
| `CppQaEngine.cs`                | C++ QAを実行        |
| `ProcessExecutor.cs`            | 外部Processを安全に実行  |
| `QaParsing.cs`                  | QA Toolの出力を解析    |
| `QaToolSupport.cs`              | QA Tool利用可否を判定   |
| `QaWorkspaceResolver.cs`        | QA対象Workspaceを解決 |
| `QaExecutionOptions.cs`         | QA実行設定           |
| `EnvironmentSnapshotService.cs` | QA実行環境を記録        |
| `ReproductionCoordinator.cs`    | 問題再現処理を統括        |

### Persistence

| File                                     | 内容                                   |
| ---------------------------------------- | ------------------------------------ |
| `DipDbContext.cs`                        | Entity Framework Core DbContext      |
| `DbInitializer.cs`                       | Database初期化                          |
| `P0SchemaBootstrapper.cs`                | 初期Schemaを準備                          |
| `LegacyMigrationAdopter.cs`              | 旧Migration状態を新Schemaへ移行              |
| `DevelopmentGraphRepository.cs`          | Development Graph永続化                 |
| `ProjectTruthRepository.cs`              | Project Truth永続化                     |
| `EvidenceRepository.cs`                  | Evidence Record永続化                   |
| `ClaimEvidenceLinkRepository.cs`         | ClaimとEvidenceのRelation永続化           |
| `QualityIssueRepository.cs`              | Quality Issue永続化                     |
| `MetricRepository.cs`                    | Metric永続化                            |
| `PipelineRunRepository.cs`               | Pipeline Run永続化                      |
| `ProcessedWebhookRepository.cs`          | GitHub Webhook冪等性管理                  |
| `ProjectClaimRepository.cs`              | Project Claim永続化                     |
| `OutboxRepository.cs`                    | Outbox Message永続化                    |
| `AuditLogRepository.cs`                  | Audit Log永続化                         |
| `DashboardReadRepository.cs`             | Dashboard向けRead Query                |
| `DevelopmentIntelligenceRepositories.cs` | Development Intelligence関連Repository |
| `PlatformIntelligenceRepositories.cs`    | Platform Intelligence関連Repository    |
| `P1Repositories.cs`                      | P1機能Repository群                      |
| `P2Repositories.cs`                      | P2機能Repository群                      |
| `P2LegacyStores.cs`                      | P2旧形式Data Store互換処理                  |
| `Repositories.cs`                        | 共通Repository実装                       |

### Intelligence

| File                             | 内容                      |
| -------------------------------- | ----------------------- |
| `MinimumHardwareRunner.cs`       | Minimum Hardware条件を実測   |
| `RegressionRunner.cs`            | Regression Testを実行      |
| `ReliabilityRunner.cs`           | Reliability Testを実行     |
| `FuzzRunner.cs`                  | Fuzz Testを実行            |
| `MutationRunner.cs`              | Mutation Testを実行        |
| `IntelligenceExecutionSafety.cs` | Intelligence処理の実行安全性を管理 |

### Hardware

| File                              | 内容                              |
| --------------------------------- | ------------------------------- |
| `PerformanceSimulationService.cs` | Hardware PerformanceをSimulation |
| `HardwareCalibrationService.cs`   | Simulationを実測値でCalibration      |
| `HardwareConstraintRunner.cs`     | Hardware Constraint下でTestを実行    |

### Replay

| File                    | 内容                |
| ----------------------- | ----------------- |
| `GameReplayExecutor.cs` | Game Replayを実際に実行 |
| `GameReplayOptions.cs`  | Replay実行設定        |

### P2 Intelligence

| File                                 | 内容                             |
| ------------------------------------ | ------------------------------ |
| `TestImpactAnalyzer.cs`              | 変更に必要なTestを分析                  |
| `NativeSymbolicator.cs`              | Native Stack TraceをSymbolicate |
| `P2ExperimentRunner.cs`              | P2 Experimentを実行               |
| `MediaRegressionRunner.cs`           | Image / Media Regressionを検出    |
| `OpenAiCompatibleRootCauseClient.cs` | OpenAI互換APIによる補助Root Cause分析   |
| `P2IntelligenceOptions.cs`           | P2 Intelligence設定              |

### Artifact

| File                      | 内容                |
| ------------------------- | ----------------- |
| `LocalArtifactStore.cs`   | ArtifactをLocalへ保存 |
| `MinioArtifactStore.cs`   | ArtifactをMinIOへ保存 |
| `ArtifactStoreOptions.cs` | Artifact Store設定  |

### Provenance

| File                            | 内容                    |
| ------------------------------- | --------------------- |
| `ReleaseProvenanceCollector.cs` | Release Provenanceを収集 |
| `ReleaseProvenanceOptions.cs`   | Provenance収集設定        |

### Messaging

| File                   | 内容             |
| ---------------------- | -------------- |
| `RedisQueueClient.cs`  | Redis Queueを操作 |
| `RedisQueueOptions.cs` | Redis接続設定      |

### Benchmark

| File                   | 内容                   |
| ---------------------- | -------------------- |
| `BenchmarkExecutor.cs` | Benchmark Processを実行 |

### Infrastructure共通

| File                            | 内容                           |
| ------------------------------- | ---------------------------- |
| `SystemClock.cs`                | 現在時刻を提供                      |
| `DipOperationalMetrics.cs`      | DIP自身のOperational Metricsを収集 |
| `GitHubWorkItemConnector.cs`    | GitHub IssueをWork Itemとして取得  |
| `DependencyInjection.cs`        | Infrastructure ServiceをDI登録  |
| `DependencyInjection.GitHub.cs` | GitHub InfrastructureをDI登録   |

---

## `Dip.Domain`

DIPの中心データモデルです。

### Development Graph

| File                      | 内容                         |
| ------------------------- | -------------------------- |
| `DevelopmentGraphNode.cs` | Development GraphのNode     |
| `DevelopmentGraphEdge.cs` | Node間のRelation             |
| `ProjectControlEnums.cs`  | Graph、Risk、Readiness等のEnum |

### Project

| File                      | 内容                       |
| ------------------------- | ------------------------ |
| `Project.cs`              | Project本体                |
| `ProjectClaim.cs`         | Projectに対する状態申告          |
| `ProjectTruthSnapshot.cs` | Evidenceから算出されたProject状態 |
| `ProjectForecast.cs`      | Project完了予測              |
| `DecisionTrace.cs`        | 意思決定履歴                   |
| `AuditLog.cs`             | 操作監査履歴                   |

### Evidence

| File                   | 内容                      |
| ---------------------- | ----------------------- |
| `EvidenceRecord.cs`    | Project判断に利用するEvidence  |
| `ClaimEvidenceLink.cs` | ClaimとEvidenceのRelation |

### Quality

| File                   | 内容                  |
| ---------------------- | ------------------- |
| `QaRun.cs`             | QA実行単位              |
| `QaFinding.cs`         | QAで発見した問題           |
| `QualityIssue.cs`      | 継続管理する品質Issue       |
| `QualityGateResult.cs` | Quality Gate結果      |
| `Metric.cs`            | Performance等のMetric |

### Management / Intelligence

| File                          | 内容                  |
| ----------------------------- | ------------------- |
| `P2IntelligenceRecord.cs`     | Intelligence分析結果    |
| `ChangeRiskAssessment.cs`     | Change Risk結果       |
| `Incident.cs`                 | Incident本体          |
| `IncidentEvent.cs`            | Incident Event      |
| `IncidentOccurrence.cs`       | Incident発生記録        |
| `ReleaseReadinessSnapshot.cs` | Release Readiness結果 |
| `RegressionTriage.cs`         | Regression Triage結果 |

### Hardware / Performance

| File                             | 内容                        |
| -------------------------------- | ------------------------- |
| `HardwareProfile.cs`             | Hardware Profile          |
| `HardwareCalibration.cs`         | Hardware予測Calibration     |
| `HardwareCommunitySample.cs`     | Community Hardware Sample |
| `CommunityHardwareSample.cs`     | Hardware Community Data   |
| `HardwareBacktest.cs`            | Hardware Backtest結果       |
| `PerformanceSimulationRun.cs`    | Performance Simulation結果  |
| `PerformanceBudgetProfile.cs`    | Performance Budget設定      |
| `PerformanceBudgetEvaluation.cs` | Budget評価結果                |

### Repository / Work

| File                       | 内容                       |
| -------------------------- | ------------------------ |
| `RepositoryNode.cs`        | Repository Graph Node    |
| `RepositoryDependency.cs`  | Repository間依存            |
| `CrossRepositoryImpact.cs` | Repository横断影響           |
| `ExternalWorkItemLink.cs`  | 外部Issue / Work Item Link |

### Replay / Benchmark

| File                     | 内容               |
| ------------------------ | ---------------- |
| `GameReplaySession.cs`   | Replay Scenario  |
| `GameReplayRun.cs`       | Replay実行         |
| `GameReplayMetric.cs`    | Replay Metric    |
| `BenchmarkDefinition.cs` | Benchmark定義      |
| `BenchmarkRun.cs`        | Benchmark実行      |
| `BenchmarkMetric.cs`     | Benchmark Metric |
| `BenchmarkComparison.cs` | Benchmark比較      |

### Job / Artifact

| File                       | 内容                      |
| -------------------------- | ----------------------- |
| `DurableJob.cs`            | Durable Background Job  |
| `ArtifactBlob.cs`          | Artifact Binary情報       |
| `ArtifactRecord.cs`        | Artifact Metadata       |
| `ReleaseEvidenceBundle.cs` | Release Evidence Bundle |
| `OutboxMessage.cs`         | Outbox Pattern Message  |
| `ProcessedWebhook.cs`      | 処理済みWebhook             |

### Value Object / Utility

| File                         | 内容                         |
| ---------------------------- | -------------------------- |
| `CommitSha.cs`               | Commit SHA Value Object    |
| `HealthScore.cs`             | Health Score Value Object  |
| `QualityIssueFingerprint.cs` | Quality Issue識別Fingerprint |
| `DomainException.cs`         | Domain Rule違反Exception     |

その他の`Enums/`ファイルは、Severity、Status、Evidence Confidence、QA Language、RiskなどのDomain状態を定義します。

---

## `Dip.Shared`

| File                       | 内容                               |
| -------------------------- | -------------------------------- |
| `CollectorReport.cs`       | Collector → API間のReport Contract |
| `CollectorRunStatus.cs`    | Collector実行Status                |
| `GitHubWebhookReceived.cs` | GitHub Webhook Event Contract    |

---

## `Dip.Api`

### Project Management API

| File                                 | 内容                                             |
| ------------------------------------ | ---------------------------------------------- |
| `ProjectControlController.cs`        | Management / Graph / Risk / Spreadsheet等の主要API |
| `ProjectsController.cs`              | Project管理API                                   |
| `ProjectClaimsController.cs`         | Project Claim API                              |
| `DashboardController.cs`             | 基本Dashboard API                                |
| `IntelligenceDashboardController.cs` | Intelligence Dashboard API                     |
| `ReleaseReadinessController.cs`      | Release Readiness API                          |
| `WorkItemsController.cs`             | Work Item API                                  |
| `RepositoryGraphController.cs`       | Repository Graph API                           |
| `ChangeRiskController.cs`            | Change Risk API                                |

### Technical API

| File                              | 内容                        |
| --------------------------------- | ------------------------- |
| `QaController.cs`                 | QA実行API                   |
| `QaArtifactsController.cs`        | QA Artifact取得             |
| `MetricsController.cs`            | Metric API                |
| `PipelineRunsController.cs`       | Pipeline Run API          |
| `CollectorsController.cs`         | Collector Report受付        |
| `WebhooksController.cs`           | GitHub Webhook受付          |
| `IncidentsController.cs`          | Incident API              |
| `PerformanceBudgetsController.cs` | Performance Budget API    |
| `HardwareController.cs`           | Hardware Intelligence API |
| `HardwareBacktestsController.cs`  | Hardware Backtest API     |
| `GameReplayController.cs`         | Game Replay API           |
| `RegressionTriageController.cs`   | Regression Triage API     |
| `BenchmarksController.cs`         | Benchmark API             |
| `ArtifactsController.cs`          | Artifact API              |
| `ReleaseEvidenceController.cs`    | Release Evidence API      |
| `JobsController.cs`               | Durable Job API           |
| `P2JobsController.cs`             | P2 Job API                |
| `P2IntelligenceController.cs`     | P2 Intelligence API       |
| `IntelligenceController.cs`       | Intelligence操作API         |
| `AnalysisController.cs`           | Analysis Service連携API     |
| `NotificationsController.cs`      | Notification API          |

### API共通

| File                             | 内容                         |
| -------------------------------- | -------------------------- |
| `Program.cs`                     | ASP.NET Core Application構成 |
| `Policies.cs`                    | Authorization Policy       |
| `ExceptionHandlingMiddleware.cs` | API Exceptionを共通処理         |
| `RawBodyBufferingExtensions.cs`  | Webhook等のRaw Bodyを保持       |
| `AuthController.cs`              | Authentication API         |

---

## `Dip.Worker`

| File                                 | 内容                      |
| ------------------------------------ | ----------------------- |
| `Program.cs`                         | Worker Application起動    |
| `OutboxWorker.cs`                    | Outbox MessageをQueueへ送信 |
| `CollectorReportProcessedWorker.cs`  | Collector Report後処理     |
| `GitHubChangeRiskWorker.cs`          | GitHub変更のRisk分析         |
| `AutomaticRegressionTriageWorker.cs` | Regressionを自動Triage     |
| `DurableJobWorker.cs`                | Durable Jobを実行          |
| `DurableJobHandlers.cs`              | Job種類別Handler           |
| `DurableJobRecoveryWorker.cs`        | 中断JobをRecover           |
| `ArtifactRetentionWorker.cs`         | Artifact保存期限を管理         |
| `WorkerMetricsEndpoint.cs`           | Worker Metrics Endpoint |
| `WorkerOptions.cs`                   | Worker設定                |

---

## `Dip.Web`

| File                        | 内容                                              |
| --------------------------- | ----------------------------------------------- |
| `Program.cs`                | Blazor Web Application起動                        |
| `App.razor`                 | Blazor Root Component                           |
| `Routes.razor`              | Page Routing                                    |
| `MainLayout.razor`          | Dashboard共通Layout                               |
| `Home.razor`                | Home Page                                       |
| `ProjectDashboard.razor`    | Project基本Dashboard                              |
| `ProjectIntelligence.razor` | Management / Development Intelligence Dashboard |
| `QaRunDetails.razor`        | QA実行詳細                                          |
| `ApiClient.cs`              | Dip.ApiとのHTTP通信                                 |
| `ProjectState.cs`           | 選択Project状態                                     |
| `UserState.cs`              | User状態                                          |
| `_Imports.razor`            | Razor共通Import                                   |

---

# Collectors

## Native Collector

| File                    | 内容                           |
| ----------------------- | ---------------------------- |
| `main.cpp`              | Native Collector Entry Point |
| `FileScanner.cpp`       | Fileを走査                      |
| `AssetHasher.cpp`       | Asset Hashを計算                |
| `DuplicateDetector.cpp` | 重複Assetを検出                   |
| `LogAnalyzer.cpp`       | Logを解析                       |
| `ReportWriter.cpp`      | Collector Reportを生成          |
| `CollectorOptions.hpp`  | Collector設定                  |
| `CollectorTypes.hpp`    | Collector Data Type          |
| `FileScanner.hpp`       | File Scanner Interface       |
| `AssetHasher.hpp`       | Asset Hasher Interface       |
| `DuplicateDetector.hpp` | Duplicate Detector Interface |
| `LogAnalyzer.hpp`       | Log Analyzer Interface       |
| `ReportWriter.hpp`      | Report Writer Interface      |

## Unity Collector

| File                        | 内容                     |
| --------------------------- | ---------------------- |
| `UnityCollectorCli.cs`      | Unity Collector CLI    |
| `UnityCollectorSettings.cs` | Unity Collector設定      |
| `SceneAnalyzer.cs`          | Unity Sceneを解析         |
| `AnimationAnalyzer.cs`      | Animation情報を解析         |
| `BuildSettingsAnalyzer.cs`  | Build Settingsを解析      |
| `PerformanceProbe.cs`       | Runtime Performanceを計測 |
| `ReplayProtocolProbe.cs`    | Replay用Runtime情報を取得    |

## Unreal Collector

| File                | 内容                            |
| ------------------- | ----------------------------- |
| `collector.py`      | Unreal Project、Log、Crash情報を収集 |
| `test_collector.py` | Unreal Collector Unit Test    |

## Native Support Agents

| File                                 | 内容                     |
| ------------------------------------ | ---------------------- |
| `crash-watchdog/src/main.cpp`        | Crash監視Agent           |
| `hardware-probe/src/main.cpp`        | Hardware情報取得Agent      |
| `hardware-stress-agent/src/main.cpp` | Hardware Stress実行Agent |
| `capture-agent/src/main.cpp`         | Capture処理Agent         |

---

# Python Analysis Service

| File                                      | 内容                                    |
| ----------------------------------------- | ------------------------------------- |
| `services/python-analysis/app/main.py`    | FastAPIによるPython分析Service Entry Point |
| `services/python-analysis/pyproject.toml` | Python Package・Dependency設定           |

---

# Database

Entity Framework Core Migrationを利用します。

主なMigration:

```text
202608120001_InitialBaseline
202608120002_AddDurablePlatformIntelligence
202608120003_AddDevelopmentIntelligenceExpansion
202608120004_AddProjectControlGraph
```

`202608120004_AddProjectControlGraph`では、Management IntelligenceやChange Impactなどの基盤となるDevelopment Graphを追加しています。

---

# Processing Flow

```text
1. Data Collection
       ↓
GitHub / CI / Spreadsheet / Collector / QA

2. Normalize
       ↓
DIP共通Data Modelへ変換

3. Development Graph
       ↓
Task / Feature / Repository / Build / Test / Asset等を接続

4. Analysis
       ↓
Schedule
Capacity
Risk
Blocker
Critical Path
Change Impact
Release Readiness

5. Management Translation
       ↓
専門的な結果を管理判断向け表現へ変換

6. Output
       ↓
Blazor Dashboard
Excel
Google Sheets
Management Alerts
Decision Brief
```

---

# Setup

## Requirements

* .NET SDK 8.0以上
* Git
* Docker Desktop / Docker Engine + Compose V2
* PowerShell 5.1以上
* PostgreSQL
* Redis
* CMake / C++20対応Compiler（Native Collector使用時）
* Python 3.12（Python / Unreal関連機能使用時）

---

## Clone

```powershell
git clone https://github.com/MKyosei2/development-intelligence-platform.git
cd development-intelligence-platform
```

---

## Restore

```powershell
dotnet restore DevelopmentIntelligencePlatform.sln
```

---

## Build

```powershell
dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
```

---

## Unit Test

```powershell
dotnet test tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj -c Release
dotnet test tests/Dip.Application.Tests/Dip.Application.Tests.csproj -c Release
dotnet test tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj -c Release
```

---

## Docker

```powershell
docker compose up -d
```

---

## Run

API:

```powershell
dotnet run --project src/Dip.Api/Dip.Api.csproj
```

Worker:

```powershell
dotnet run --project src/Dip.Worker/Dip.Worker.csproj
```

Dashboard:

```powershell
dotnet run --project src/Dip.Web/Dip.Web.csproj
```

---

# Verification

Project Control / Management Intelligence:

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-ProjectControl.ps1"
```

Full Solution:

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-Solution.ps1"
```

---

# Development Policy

DIPでは、単純なCommit数、コード行数、作業時間などから個人の能力や生産性を評価しません。

評価対象は、

* Workstream
* Project Progress
* Capacity
* Dependency
* Risk
* Evidence
* Release State

です。

目的は個人監視ではなく、**プロジェクトのボトルネックを早期に発見し、チーム全体の意思決定を改善すること**です。

---

# Current Status

Development Intelligence Platformは現在開発・検証中です。

実装済み機能であっても、最新CommitでBuild、Unit Test、Integration Test、E2E Testが成功するまでは検証完了とは扱いません。

実際の状態については、

```text
docs/IMPLEMENTATION_STATUS.md
```

を参照してください。

---

# License

本リポジトリはルートの`LICENSE`に従います。