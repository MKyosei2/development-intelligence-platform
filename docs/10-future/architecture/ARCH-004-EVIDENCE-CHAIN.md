# ARCH-004: Revision-bound Evidenceと来歴の契約

**Status: Proposed Enhancement** | **基盤:** 既存のProject Truth / Evidence

## 1. 原則

DIPは主張（Claim）と観測（Evidence）を分け、改訂版・情報源・検証環境が適切でないものを現在の品質の裏付けへ昇格させない。

## 2. 最小の論理データモデル（新API確定ではない）

| 属性 | 説明 |
|---|---|
| projectId / subject | 対象プロジェクト・対象機能 |
| sourceRevision | 結果を生成したDIPのソースSHA |
| targetRevision | 検査対象ゲーム／ソフトウェアのSHA。DIP SHAと混同しない |
| observationId / parentIds | 収集・派生・再検証の来歴 |
| producer / environment | 実行したエンジン、工具、OS、Runnerなど |
| coverage / method | 何をどう検査したか |
| outcome | PASS / FAIL / NOT_EVALUATED / BLOCKED 等、語彙を規格化 |
| artifactRef / digest | 証拠本体の所在と改ざん検知用ハッシュ |
| timestamps / freshness | 観測期間と適用期限 |
| trust / signature | 情報源・署名検証の状態 |

## 3. 整合性ルール

- `metrics=[]`, `issues=[]`, 結果未受信を成功扱いしない。
- 合成テストのPASSはテスト期待値の成否であり、対象ゲームの品質PASSではない。
- 親Evidenceが欠損・破損・不適用のとき、派生データが無条件でVerifiedにならない。
- 必須の実行物に対しては、終了コードだけでなく必要な成果物、ハッシュ、schema、対象SHAを確認する。
- 同じジョブの複数の再試行を独立した検証者として重複カウントしない。
- 古い／署名なしEvidenceの扱いを明示し、セキュリティ属性と信頼度の説明を分ける。

## 4. 検証ケース

別SHAのログ差し替え、欠損Artifact、破損ハッシュ、旧版署名なしEvidence、異なるターゲットのスクリーンショット、空のQA結果、同一ソースの重複Evidenceを混ぜて拒否条件を検証する。

## 5. 既存契約との関係

[THREE_PILLAR_PRECISION_CONTRACT](../../09-portfolio/THREE_PILLAR_PRECISION_CONTRACT.md)を上書きしない。実際にデータモデル・ルールを変更するときは現行仕様と移行互換性を確認する。
