# RFC-004: Production Verification契約を実検査へ接続する

**Status: Proposed** | **Area: Release Operations / Game QA**

## 背景

DIPの`ProductionVerificationQaPack.cs`には、ローカライズ、アクセシビリティ、入力デバイス、セーブ互換、認証、デバイスファーム、GPUキャプチャ、意味的グラフィックス、マルチプレイヤー、長期運用、競合、アセット、シェーダーなどの契約・外部検査の受け入れ経路がある。

ただし「外部コマンドを実行し、必要な証拠が出た」という契約と、「DIP自体が専門的な意味検査を自律的に実施した」は別である。

## 提案

- `.dip/production-verification.json`のサンプル（秘密・私有アセットなし）を用意する。
- 契約カテゴリごとに「実際の検査器」「外部委譲」「静的確認」「未評価」を分類する。
- 各検査の出力にschema、対象SHA、計測環境、Artifact hash、Coverage、tool versionを要求する。
- exit codeや単なるファイル存在だけでなく、内容の妥当性、発行主体、改ざん検知を確認する。
- 独立した正常・異常fixtureと環境不可ケースを提供する。

## 既存コード参照

- `src/Dip.Infrastructure/QualityAutomation/ProductionVerificationQaPack.cs`
- `src/Dip.Infrastructure/QualityAutomation/AutonomousQaExperimentDispatcher.cs`

## 受け入れ条件

- [ ] 代表的な公開サンプル設定と動作説明を整備。
- [ ] すべてのカテゴリで、実装の深さ・依存する外部ツール・検証環境を明示。
- [ ] 必須Evidenceが古い／不正／空の場合は合格にしない。
- [ ] 少なくとも1カテゴリで実Toolの入力からRelease ReadinessまでE2E検証。
- [ ] その他の未実行カテゴリを`NOT_EVALUATED`と表示。

## 変更上の注意

既存のQA APIやRelease Gateとの互換性を保ち、必要ならschema versionと移行方針を定義する。サンプル設定を作るだけで本番対応済みとは言わない。
