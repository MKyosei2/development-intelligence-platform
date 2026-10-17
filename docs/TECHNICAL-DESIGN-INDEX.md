# DIP 詳細技術設計書ナビゲーション

> **文書ステータス:** 追加資料のインデックス / 設計レビュー対象  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 対象と読み方
この追加パックは既存 `README.md` と `docs/00-overview/` 〜 `docs/09-portfolio/` を変更せず、足りなかった詳細仕様を補う**新規ファイルのみ**で構成する。実装の「存在」は `Implemented`、テストの成功は `Tested`、実ターゲットの記録がある場合のみ `Runtime Verified`、顧客へ提供できる条件を満たす場合のみ `Production Ready`。`Proposed` を完成実績として扱わない。

## 現行システムの構造を説明する文書
| 読みたい内容 | ファイル |
|---|---|
| 全体構成・責務・信頼境界 | [システム詳細構成](02-architecture/architecture-detailed-current.md) |
| 成功／異常を含む一連の処理 | [E2Eシーケンス](02-architecture/end-to-end-sequences.md) |
| AI QAの仮説〜修正検証 | [AI QA内部設計](02-architecture/ai-qa-internal-design.md) |
| QAジョブの状態遷移 | [QA実行ライフサイクル](02-architecture/qa-job-lifecycle.md) |
| QA Adapterと結果の共通化 | [QA Adapter契約](02-architecture/qa-adapter-contract.md) |
| Evidenceの来歴・SHA・完全性 | [Evidence契約](02-architecture/evidence-provenance-contract.md) |
| Claim評価の条件 | [Project Truth判定表](02-architecture/project-truth-decision-table.md) |
| Releaseの判断・阻止条件 | [Release Gate](02-architecture/release-gate-decision-model.md) |
| DBの論理モデル・移行 | [DB論理設計](03-database/evidence-and-qa-data-model.md) / [移行・保持](03-database/schema-migration-and-data-lifecycle.md) |
| API・権限 | [API契約](04-api/api-boundaries-and-errors.md) / [認可表](04-api/project-authorization-matrix.md) |
| QA不信頼コードの脅威 | [隔離・脅威](07-security/untrusted-qa-threat-model.md) |
| テナント・知財保護 | [セキュリティ境界](07-security/tenant-and-privacy-boundaries.md) |

## 検証計画・運用資料
| 内容 | ファイル |
|---|---|
| QA機能別の実装深度 | [QA能力評価表](08-testing/qa-capability-assessment.md) |
| LLM性能の測り方 | [AI QA実験プロトコル](08-testing/ai-qa-evaluation-protocol.md) |
| 実Unity/Unreal | [エンジン検証](08-testing/real-engine-qa-test-plan.md) |
| 画像・GPU・性能・マルチ | `08-testing/` の各検証計画 |
| 運用・監視・復旧 | `06-operations/` の追加Runbook |
| 将来商用化の詳細設計案 | `10-future/architecture/`、`10-future/operations/` に `PROD-*` を追加 |

## 作業原則
1. **As-Is:** リポジトリで確認した責務・API・データ。動作実測がない箇所は「実装経路を確認」と表現する。
2. **To-Be:** 導入提案と選択肢。未決定事項、トレードオフ、検証ゲートを併記。
3. **Evidence:** DIP SHA、対象プロジェクトSHA、環境、入力、実際の出力、再現可能なログ、Artifactハッシュを併記。
4. **技術図:** Mermaidは論理図であり、未実装の通信・インフラを実装済みに見せない。
5. **正式化:** 設計採用時は既存ADRへ追記／新規ADR作成し、Issue→PR→同一SHAのCIを通す。

## Wikiとの役割分担
`wiki-upload/` に追加される6ファイルは**技術説明の入口**。正式な仕様・判断表・Runbookは `docs/` を正本とする。Wikiは別Gitリポジトリへ公開する。`docs/10-future/`は現時点で作業ブランチのみのため、mainへマージするまでWikiのリンクを作業ブランチ向けにする。

## 参照する実装・既存資料

- `docs/09-portfolio/ARCHITECTURE_OVERVIEW.md`
- `docs/09-portfolio/THREE_PILLAR_PRECISION_CONTRACT.md`
- `docs/IMPLEMENTATION_STATUS.md`
