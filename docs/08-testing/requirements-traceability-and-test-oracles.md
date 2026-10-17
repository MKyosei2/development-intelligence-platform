# 要求・リスク・テスト・Evidenceの追跡表とオラクル設計

> **文書ステータス:** Proposed Verification Governance  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. なぜ必要か
多数のQA機能が存在しても、どの製品要件を検査しているか不明ならCoverageを証明できない。ゲームの「期待される動作」はプログラムの実行結果だけでは自明でなく、**オラクル（期待結果を決める基準）**を明示する必要がある。

## 2. Traceability表の形式
| Requirement | Risk | Test ID | Oracle | Target | Evidence | Verdict |
|---|---|---|---|---|---|---|
| 完了申告を過信しない | Critical見逃し | QA-TRUTH-01 | QA Failed, Claim Contradicted | DIP SHA | E2E run ID | 実測 |
| GPUのFrame Time規格 | p99 hitch | GPU-PERF-01 | 事前に固定した閾値 | 実Game SHA | GPU logs | 未評価 |
| Save互換 | 古いセーブ破損 | SAVE-01 | 期待serialized state | Engine/Build SHA | Save test log | 未評価 |
| QA Runner分離 | 秘密流出 | SEC-EXE-01 | 禁止領域へアクセス不可 | Runner version | Attestation | 未評価 |

上表の「未評価」は例示状態であり、実際の最新Runtime判定結果を自動取得したものではない。

## 3. オラクルの分類
- **Compiler/Static:** 期待される診断ID・ファイル・Line/Rule。
- **Runtime:** 終了状態、Assertion、Crash fingerprint、Memoryの不変条件。
- **Visual:** 承認済みGolden、Tolerance、Mask、色空間、許容条件。
- **Performance:** 固定Hardware、温度・電源、Sample数、p95/p99、予算。
- **Game replay:** 固定Seed/Input/Tick、決定的StateHash、許容誤差。
- **AI fix:** Ground Truth、既存回帰、Red→Green、禁止ファイル変更なし。
- **Operational:** Backup Restore、Lease整合、Tenant拒否、可観測性。

## 4. Coverageの正確な定義
実装済みのRule数を対象のCoverageと同一視しない。必要Requirementsのうち`Executed and Validated`、`NotEvaluated`、`Unsupported`、`Failed`の比率を分離。Synthetic caseの成功率は未知のゲームへ外挿しない。

## 5. QA Review Gate
仕様変更があればRequirement ID → Test ID → 期待Oracle → 最終Evidenceの参照を更新する。テストが削除・抑制されたときにゲートが弱くなったかをレビューし、同じFailing caseをテスト変更で見逃していないかを再実行する。

## 参照する実装・既存資料

- `docs/01-requirements/acceptance-criteria.md`
- `docs/10-future/qa/QA-001-COVERAGE-MATRIX.md`
- `docs/08-testing/qa-capability-assessment.md`
