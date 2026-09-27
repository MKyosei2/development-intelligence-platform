# 変更履歴

Development Intelligence Platformの主な変更をここに記録します。

このプロジェクトは [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) の構成に従います。Semantic Versioningは最初のPublic VersionをReleaseする段階から適用します。

## [Unreleased]

### 追加

- GUI Phase 1: GitHub App repository connection、Repository Picker、GitHub-to-DIP import、scoped ProjectState、redesigned Project Overview、Repository / CI / QA GUI、shared loading/error/empty states、SignalR project events、Playwright GUI E2E。
- Upgrade8-based Development Intelligence expansion: Intelligence Dashboard、deterministic game-session replay、target performance budgets、Unreal Engine collector、real-device hardware backtesting、Change Risk、automatic regression triage、Release Readiness gate、GitHub Issue work-item integration、release provenance / CycloneDX SBOM、OpenTelemetry observability、multi-repository dependency impact。
- Expansion persistence modelとglobal performance-budget presets向けEF Core migration `20260812000300_AddDevelopmentIntelligenceExpansion`。
- Domain、Application、Infrastructure、API、Worker、Shared contracts、Blazor Webの初期Layered Architecture。
- Collector report ingestionとvalidationの基盤。
- PostgreSQL、Redis、MinIO、Docker Compose、Quality Gate、Dashboardの基盤。
- Repository全体のDevelopment、Security、Contribution、Formatting、Ignore Policy。

### 修正

- 残っていた0 byteのWeb state placeholderを削除し、Project Import成功後のoptional GitHub workspace sync failureをnon-fatal化。
- 0 byteだったRoot Configuration Fileを有効なXML / JSON / Textへ置換。
- 空の `Directory.Build.props`、`Directory.Build.targets`、`Directory.Packages.props` が原因のMSBuild Failureを防止。
- `global.json` により有効な.NET 8 SDK selection policyを追加。

### セキュリティ

- Private vulnerability reporting policyを追加。
- Local secrets、environment files、certificates、generated output向けIgnore Ruleを追加。

[Unreleased]: https://github.com/MKyosei2/development-intelligence-platform/commits/main
