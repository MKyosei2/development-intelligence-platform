# ChangeRisk

## 役割

変更量、直接テストカバレッジ、関連incident、regression、性能・security・schema変更、cross-repository impactを材料にdeterministicなchange riskを評価する。

## ソース位置

- Application: `src/Dip.Application/Features/ChangeRisk/`

## 主な読み始め

- `src/Dip.Application/Features/ChangeRisk/ChangeRiskIntelligenceService.cs`

## 読み順

1. 上記のService / Command / Query / ContractからFeatureの入口とデータ境界を確認する。
2. 依存する`Dip.Application.Abstractions`を確認し、外部I/Oとの境界を把握する。
3. 対応するDomain型とtestsを確認し、状態・判定規則・失敗条件を追う。
4. Infrastructure実装がある場合は最後に確認し、DB・GitHub・filesystem・runtime等の具体I/Oを追う。

## ChangeRiskIntelligenceServiceの処理

- 入力: commit SHA、変更file/line数、直接test coverage、incident/regression件数、performance/security/schema変更フラグ、source repository情報。
- 評価: 各risk factorを加点し、cross-repository impactがある場合はそのlevelも加点する。
- 正規化: scoreを0〜100へclampし、Critical / High / Medium / Lowへ分類する。
- 出力・保存: reasonsとaffected repositoriesを含むassessmentをrepositoryへ保存し、DTOとして返す。
- `GetLatestAsync`は最新assessmentを読み出し、保存済みJSONをDTOへ戻す。

