# [QA Research] 生成AI QAと既存検査の比較ベンチマーク

> Type: Verification / Research | Priority: P0 | Pillar: Game QA

## 背景

少数の合成バグ検出結果を、未知のゲーム開発全般でのAIの精度とみなすことはできない。

## 作業

正常ケース・バグ注入・曖昧な現象・多言語ケースを用意し、LLM有効／無効で同じ制約の下に比較する。

## Acceptance criteria

- [ ] DatasetのID、Ground truth、サンプル数と対象SHAを確定。
- [ ] Precision/Recall/FPRを正常対照群込みで算出。
- [ ] Root cause正解率、Patch apply率、Verified Fix率、新規Regressionを区別。
- [ ] 時間・費用・利用モデルと再試行回数を記録。
- [ ] 未実行・利用不可を0件成功として扱わない。
- [ ] 合成ケースの適用限界を報告。

## 関連設計

`docs/10-future/qa/QA-004-LLM-EVALUATION.md`
