# DIP 日本語コードリファレンス

このディレクトリは、`src/Dip.Application/Features/` のFeature単位で「何を担当するコードか」を日本語で辿れるようにするための索引です。ソース内の `Purpose:` は短い英語、日本語での責務説明はここに分離します。

- 基準revision: `6c7ee85cd370288e1c7b8e9c89a3a5aaa6888c40`
- 対象: `src/Dip.Application/Features/` の現行Feature
- 注意: この文書はコード読解用です。Runtime Verifiedや現在revisionのCI成功を主張するEvidenceではありません。

## 読み方

1. 下表からFeatureを選ぶ。
2. Feature READMEで責務・主要ソース・読み順を確認する。
3. 実装詳細はリンク先ソースを読み、testsとEvidenceで挙動を確認する。

## Feature一覧

| Feature | 日本語での役割 |
|---|---|
| [Analysis](./Analysis/README.md) | 異常検知、日次サマリー、リリース予測などの分析要求とDTOをまとめるApplication Feature。 |
| [Artifacts](./Artifacts/README.md) | Artifactの保存・追跡・version管理に関するApplication処理をまとめる。 |
| [Assets](./Assets/README.md) | Assetに関する開発インテリジェンス処理をまとめる。 |
| [Authorization](./Authorization/README.md) | Project単位のアクセス判定とmutation境界をApplication層で扱う。 |
| [Benchmarks](./Benchmarks/README.md) | Benchmark実行の契約とオーケストレーションを扱う。 |
| [Capabilities](./Capabilities/README.md) | DIPが提供するCapabilityの登録・参照をまとめる。 |
| [ChangeImpact](./ChangeImpact/README.md) | 変更が及ぼす影響を分析するApplication処理を扱う。 |
| [ChangeRisk](./ChangeRisk/README.md) | 変更量、直接テストカバレッジ、関連incident、regression、性能・security・schema変更、cross-repository impactを材料にdeterministicなchange riskを評価する。 |
| [Collectors](./Collectors/README.md) | Collectorから届くreportの検証とApplication側の取り込み処理を扱う。 |
| [Dashboard](./Dashboard/README.md) | Project/Management/Intelligence向けの集約read modelを構築する。 |
| [Decisions](./Decisions/README.md) | Decision logの登録・参照に関するApplication処理を扱う。 |
| [DevelopmentGraph](./DevelopmentGraph/README.md) | 開発対象間のgraph、production projection、test impactなどの関係情報を扱う。 |
| [Enterprise](./Enterprise/README.md) | Enterprise向けのtenant、identity、retention、evidence integrity、distributed ingestion、agent policy等をまとめる。 |
| [Evidence](./Evidence/README.md) | Evidenceの入力契約、検証、永続化境界への受け渡しをApplication層で扱う。 |
| [ExternalEvents](./ExternalEvents/README.md) | 外部から取り込むincident/eventをDIPのApplication処理へ接続する。 |
| [FeatureControl](./FeatureControl/README.md) | Feature readinessや利用可否の判定を扱う。 |
| [Fleet](./Fleet/README.md) | Agent fleetの状態・操作をApplication層で扱う。 |
| [GitHub](./GitHub/README.md) | GitHub webhook、development graph ingestion、quality report formattingなどGitHub由来の処理を扱う。 |
| [GitHubIntegration](./GitHubIntegration/README.md) | GitHub repository連携で使うApplication側のmodelをまとめる。 |
| [Hardware](./Hardware/README.md) | Hardware計測、calibration、orchestration、backtestingに関するApplication処理を扱う。 |
| [Health](./Health/README.md) | Project healthの再計算、参照、score算出の要求と計算サービスをまとめる。 |
| [Incidents](./Incidents/README.md) | Incident情報の集約処理を扱う。 |
| [Integrations](./Integrations/README.md) | 外部integrationの接続、registry、version-control sync、外部CI連携とその契約を扱う。 |
| [Intelligence](./Intelligence/README.md) | 収集済みEvidenceやhardware情報等から分析・forecast・root-cause候補を生成するApplication処理群。 |
| [Jobs](./Jobs/README.md) | Durable jobとP2 job enqueueなど非同期処理の投入・状態管理を扱う。 |
| [Management](./Management/README.md) | Project managementのdashboard/operations/portfolio/producer/spreadsheet/work-management情報を統合し、PM向けのread modelと操作境界を提供する。 |
| [Milestones](./Milestones/README.md) | Milestoneに関するインテリジェンス処理を扱う。 |
| [Notifications](./Notifications/README.md) | Notification送信・retryのCommandとDTOをまとめる。 |
| [Operations](./Operations/README.md) | Application側の運用補助処理をまとめ、現在はOutbox書き込み責務を含む。 |
| [P2Intelligence](./P2Intelligence/README.md) | P2系のEvidence-grounded root cause、hardware community、QA planning、project decision intelligence等をまとめる。 |
| [Performance](./Performance/README.md) | Performance budgetの定義・評価に関するApplication処理を扱う。 |
| [PipelineRuns](./PipelineRuns/README.md) | Pipeline runの作成、完了、metric/quality issue追加、参照用Command/Query/DTOをまとめる。 |
| [Policies](./Policies/README.md) | Evidence/Project等のpolicy contract、profile、解決処理を扱う。 |
| [ProductionTools](./ProductionTools/README.md) | Production tool由来のevent/media情報とそのmapping・queryをApplication層で扱う。 |
| [ProjectIntelligence](./ProjectIntelligence/README.md) | Project source/planの取り込み、entity resolution、plan understanding、progress intelligenceを扱う。 |
| [Projects](./Projects/README.md) | Projectの作成・更新・削除・参照とdashboard取得に必要なCommand/Query/DTO/Serviceをまとめる。 |
| [ProjectTruth](./ProjectTruth/README.md) | ClaimとEvidenceを用いたProject Truth、confidence、risk、progress、blocker等の評価をまとめる。 |
| [PullRequests](./PullRequests/README.md) | Pull Request同期とrisk再計算、参照用Command/Query/DTOをまとめる。 |
| [Quality](./Quality/README.md) | Quality issue/trend、quality gate/ruleの評価と関連Command/Query/DTOをまとめる。 |
| [QualityAutomation](./QualityAutomation/README.md) | Asset QA、Visual QA、branch QA、commercial quality、autonomous debugなど自動QAのApplication処理をまとめる。 |
| [QualityGovernance](./QualityGovernance/README.md) | QA governanceのcore engine、administration、query、run processingと契約を扱う。 |
| [Realtime](./Realtime/README.md) | Project realtime eventのApplication側DTOを定義する。 |
| [Release](./Release/README.md) | Release EvidenceとRelease Readinessの集約・判定を扱う。 |
| [Replay](./Replay/README.md) | Game replay実行・結果連携に関するApplication処理を扱う。 |
| [Repositories](./Repositories/README.md) | 複数repository間のgraph/impactを扱うApplication処理をまとめる。 |
| [Review](./Review/README.md) | Reviewに必要なインテリジェンス処理を扱う。 |
| [Scenarios](./Scenarios/README.md) | What-if simulationなどscenarioベースの評価処理を扱う。 |
| [SystemStatus](./SystemStatus/README.md) | System statusを外部へ返すためのApplication DTOをまとめる。 |
| [TeamLoad](./TeamLoad/README.md) | Team/member workload参照に必要なQueryとDTOをまとめる。 |
| [Triage](./Triage/README.md) | Regression triageのApplication処理を扱う。 |
| [WorkItems](./WorkItems/README.md) | Work itemの登録・参照・外部連携に必要なApplication処理を扱う。 |
