# Quality

## 役割
Quality ruleとquality gateを再現可能な判定ロジックとして評価するApplication Feature。

## 主な読み始め
- `src/Dip.Application/Features/Quality/QualityRuleEvaluator.cs`
- `src/Dip.Application/Features/Quality/QualityGateEvaluator.cs`
- `tests/Dip.Application.Tests/Quality/QualityRuleEvaluatorTests.cs`
- `tests/Dip.Application.Tests/Quality/QualityGateEvaluatorTests.cs`

## 読み順
1. `QualityRuleEvaluator.cs`で個別ルールの判定を見る。
2. `QualityGateEvaluator.cs`でgate statusとscoreへの集約を見る。
3. DomainのQuality型を確認する。
4. testsで閾値と失敗条件を確認する。

未実装のCommand / Query / DTO placeholderはレビュー入口として扱わない。
