# P2Intelligence

## 役割

P2系のEvidence-grounded root cause、hardware community、QA planning、project decision intelligence等をまとめる。

## ソース位置

- Application: `src/Dip.Application/Features/P2Intelligence/`

## 主な読み始め

- `src/Dip.Application/Features/P2Intelligence/P2TechnicalIntelligenceService.cs`
- `src/Dip.Application/Features/P2Intelligence/EvidenceGroundedRootCauseService.cs`
- `src/Dip.Application/Features/P2Intelligence/HardwareCommunityIntelligenceService.cs`
- `src/Dip.Application/Features/P2Intelligence/ProjectDecisionIntelligenceService.cs`
- `src/Dip.Application/Features/P2Intelligence/AdaptiveQaPlanner.cs`

## 読み順

1. 上記のService / Command / Query / ContractからFeatureの入口とデータ境界を確認する。
2. 依存する`Dip.Application.Abstractions`を確認し、外部I/Oとの境界を把握する。
3. 対応するDomain型とtestsを確認し、状態・判定規則・失敗条件を追う。
4. Infrastructure実装がある場合は最後に確認し、DB・GitHub・filesystem・runtime等の具体I/Oを追う。

