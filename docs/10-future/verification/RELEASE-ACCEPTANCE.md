# Release Acceptance: 将来の製品リリース判定

**Status: Proposed Policy** | **対象:** 将来の正式リリース。現在のポートフォリオ用11-workflow契約を置き換えない。

## 1. Release候補の情報

- ソースリポジトリ・DIP SHAと、製品のversion/tag
- 配布対象OS/CPU/Engine/Runtimeの明確な範囲
- 実行したCI／テスト／QAの対象SHAと結果
- SBOM、Provenance、Artifact digest、署名・権限
- 未解決の重大リスクと例外承認者、Rollback／復旧手順
- 不適用の機能と未評価の機能の一覧

## 2. 判定（案）

| Gate | No-Goにする条件例 |
|---|---|
| Revision binding | 必須ArtifactのSHAが異なる |
| Test coverage | 必須テストが不明、skipped、実行不可 |
| Quality | 未承認のCritical blocker |
| Security | 秘密漏えい・既知の重大脆弱性・隔離不能 |
| Provenance | 必須の署名／ハッシュ／出所が確認できない |
| Compatibility | 必須ターゲットの移行・Buildテスト未完了 |
| Rollback | 復旧手順が未定義、復元検証が欠落 |

例外ルールは別途承認者と理由を記録する。必須の改ざん検証など、例外を認めない項目も定義する。

## 3. 判定カテゴリ

`GO`、`CONDITIONAL GO`、`NO-GO`に加え、評価できない要件を明確に表示する。数値の合計点だけで安全性を上書きしない。

## 4. 完了条件

実際の候補SHAを用い、正常ケース・Critical不具合・誤SHA・欠損SBOM・環境不可・古いProject Truth snapshotを含むテストで期待通りの判定が出ること。

## 5. 現行DIPとの関係

現行の[ポートフォリオ最終検証条件](../../09-portfolio/THREE_PILLAR_PRECISION_CONTRACT.md)は別の規則。将来の商用製品としてのリリース基準は、これを満たしただけでは達成できない。
