# Management

## 役割

Project managementのdashboard/operations/portfolio/producer/spreadsheet/work-management情報を統合し、PM向けのread modelと操作境界を提供する。

## ソース位置

- Application: `src/Dip.Application/Features/Management/`

## 主な読み始め

- `src/Dip.Application/Features/Management/ProjectManagementCommandCenterService.cs`
- `src/Dip.Application/Features/Management/ManagementOperationsService.cs`
- `src/Dip.Application/Features/Management/ManagementAnalyticsService.cs`
- `src/Dip.Application/Features/Management/PortfolioManagementService.cs`
- `src/Dip.Application/Features/Management/WorkManagementService.cs`

## 読み順

1. 上記のService / Command / Query / ContractからFeatureの入口とデータ境界を確認する。
2. 依存する`Dip.Application.Abstractions`を確認し、外部I/Oとの境界を把握する。
3. 対応するDomain型とtestsを確認し、状態・判定規則・失敗条件を追う。
4. Infrastructure実装がある場合は最後に確認し、DB・GitHub・filesystem・runtime等の具体I/Oを追う。

## ProjectManagementCommandCenterServiceの処理

- `IProjectRepository`、`ManagementDashboardService`、`ManagementOperationsService`、`CommercialQualityOperationsService`から既存データを集約する。
- dashboard / cockpit / approvals / RACI / qualityを並列取得し、milestone、schedule、workstream、risk、blocker、decision、actionをPM向けread modelへまとめる。
- quality gate、dependency cycle、overdue approvals等をrecommended actionsへ反映する。
- `DeriveOverallStatus`でmanagement status、quality gate、critical blocker/risk、delay日数からcommand-center上のstatusを決める。
- このServiceは新しいproject modelを作るのではなく、既存のdurable management dataを集約するread modelとして機能する。

