# リリース証拠・SHA一致の負例検証仕様

> **文書ステータス:** Proposed acceptance suite / 既存3柱契約に整合  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 基本ルール
ポートフォリオの「FINAL PORTFOLIO EVIDENCE: PASS」は、Required CI matrix、exact SHA benchmark、QA Bug Zoo、Project Truth expected-failure scenarioの同一DIP SHAを要求する。Target Game SHAは別の意味を持つため、混同しない。

## 2. 負例
| Case | 注入内容 | 期待判定 |
|---|---|---|
| REL-01 | CI 11本のうち1本がfailure | 最終PASS不可 |
| REL-02 | Benchmarkが別DIP SHA | 最終PASS不可 |
| REL-03 | Bug Zooが一部language未実行 | Coverage不足、最終PASS不可 |
| REL-04 | Project TruthがCriticalのCompleteをConfirmed | シナリオ失敗 |
| REL-05 | `metrics=[]`, `issues=[]` | 不明をPASSへ読み替えない |
| REL-06 | 古い実Unityログを挿入 | 最新Engine PASS不可 |
| REL-07 | Artifact digest破損 | Integritiy invalid / Ready不可 |
| REL-08 | SBOMが別Commit | 少なくともWarning（現行）／外部Release案ではNoGo |
| REL-09 | Network/Runner不在 | Blocked/NotEvaluated |
| REL-10 | 署名なし旧Evidenceを大量に追加 | 現在の強い真正性の代わりにしない |

## 3. 証拠ファイル
検証対象のSHA、各CI workflow run ID、Artifact digest、期待Verdict、実際Verdict、発見した設定差異を含める。最終ファイルを生成するツール自身のコミットを固定する。

## 4. 実行判定
1. 成功系と失敗系を**同一ツール・同一Revision規則**で評価する。
2. 失敗ケースは不正なPASSが生まれないことを検証し、そのテスト自体がPASSしたと明記する。
3. Tool内部エラーや入力欠損は、不適切な`NoGo`だけでなく診断可能な失敗理由を残す。
4. Formal Artifactを公開する前にGit working tree、SHA、必須Artifactのハッシュを確認する。

## 参照する実装・既存資料

- `docs/09-portfolio/THREE_PILLAR_PRECISION_CONTRACT.md`
- `docs/09-portfolio/QA_EVIDENCE_CASE_STUDY.md`
- `tools/finalize_portfolio_evidence.py`
- `tools/verify_final_ci_matrix.py`
