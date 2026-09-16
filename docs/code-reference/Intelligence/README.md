# Intelligence

## 役割

収集済みEvidenceやhardware情報等から分析・forecast・root-cause候補を生成するApplication処理群。

## ソース位置

- Application: `src/Dip.Application/Features/Intelligence/`

## 主な読み始め

- `src/Dip.Application/Features/Intelligence/P2OrchestrationService.cs`
- `src/Dip.Application/Features/Intelligence/P2AnalysisService.cs`
- `src/Dip.Application/Features/Intelligence/ForecastAndDecisionService.cs`
- `src/Dip.Application/Features/Intelligence/RootCauseIntelligenceService.cs`
- `src/Dip.Application/Features/Intelligence/AdaptiveQaPlanner.cs`

## 読み順

1. 上記のService / Command / Query / ContractからFeatureの入口とデータ境界を確認する。
2. 依存する`Dip.Application.Abstractions`を確認し、外部I/Oとの境界を把握する。
3. 対応するDomain型とtestsを確認し、状態・判定規則・失敗条件を追う。
4. Infrastructure実装がある場合は最後に確認し、DB・GitHub・filesystem・runtime等の具体I/Oを追う。

