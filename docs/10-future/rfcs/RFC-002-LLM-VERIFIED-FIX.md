# RFC-002: LLM修正提案の実API実証と定量評価

**Status: Proposed** | **Area: Game Development QA / Security**

## 問題

LLM対応コード、レスポンスのスタブテスト、パッチ検証器だけでは、実際のLLMが信頼できる修正候補を出せるか分からない。AI提案と実行で検証された根拠の区別を維持しつつ、効果を測る必要がある。

## 提案フロー

`許可済みEvidence -> 機密データ審査 -> LLM案生成 -> JSON/ID/Path Validation -> Disposable Worktree -> Baseline -> Patch + Regression -> Re-run -> Human Review`

- モデル応答の見た目に関係なく、検証工程が失敗したらReady/Verifiedにしない。
- APIキー、内部ソース、未公開アセットを無断で外部送信しない。
- データセットを正常ケース・注入した不具合・実案件に近い難しいケースに分ける。
- 同じテストケースでdeterministic baselineとLLM enabledを比較する。
- 生成されたテストが不正に条件を緩和していないか確認する。

## 既存実装との接点

- `src/Dip.Infrastructure/P2Intelligence/OpenAiCompatibleFixProposalAgent.cs`
- `src/Dip.Application/Features/QualityAutomation/AutonomousDebugService.cs`
- `src/Dip.Infrastructure/QualityAutomation/VerifiedFixService.cs`
- `docs/09-portfolio/GENERATIVE_QA.md`

## 受け入れ条件

- [ ] opt-in設定で実APIを用い、モデル名・設定・対象改訂版を記録したEvidenceがある。
- [ ] 正常ケースに対する不必要な修正提案の割合を測定する。
- [ ] Patch Apply成功と、Regressionを含むVerified Fix成功を別指標にする。
- [ ] 実行不能やAPI障害を成功へ読み替えない。
- [ ] 禁止ファイル・秘密情報・テスト抑制の負例を検証する。
- [ ] 人によるレビューを経ずにmainへ自動マージしない。

## 代替案と未決定事項

ローカルモデル／APIモデルの選択、コスト予算、許可する言語・拡張子、モデルがコード全体を見ない制約による精度低下、外部送信ポリシー。
