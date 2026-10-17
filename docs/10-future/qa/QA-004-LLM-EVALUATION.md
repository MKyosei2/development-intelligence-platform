# QA-004: AI QAの評価指標と安全性

**Status: Proposed Evaluation Standard**

## 1. AIの役割を限定する

`Evidence -> 調査仮説 -> 修正候補 -> 隔離実行 -> テスト -> Human Review` の順に処理する。生成AIの解答をPASS判定やリリース可否の根拠として単独採用しない。

## 2. 比較する条件

- Baseline A: 既存のルールベース・静的検査のみ
- Baseline B: 非生成型の自動デバッグ／再現支援
- Experiment: 許可されたLLMによるEvidence-grounded提案＋既存のVerifiedFix経路

同じケース・制約・タイムアウト・試行回数で比較し、学習データとの重なりや手動ヒントを可能な範囲で記録する。

## 3. 測定指標

| 指標 | 定義例 | 注意 |
|---|---|---|
| Detection Recall / Precision | 既知の不具合に対する検出精度 | 独立した正常サンプルも必要 |
| Root Cause Accuracy | 提示された根本原因が参照解答と一致する割合 | 単なるログ引用は別分類 |
| Patch Applies | 候補パッチの適用が成功した割合 | 実際の修復と同義ではない |
| Verified Fix Rate | red→green、回帰、新規重大問題なしを満たす割合 | 事後テストの質が重要 |
| Regression Introduction Rate | 提案によって新たな不具合が発生した割合 | 負例や非同期挙動も測定 |
| Time to Verified Candidate | 成功候補の確認に必要な時間 | 人間の作業時間を区別 |
| Cost per Case | 推論＋Runner等の概算費用 | サービス料金は変更され得る |
| Unsafe Suggestion Rate | 秘密流出、危険コマンド、検査無効化を含む割合 | セキュリティ負例を用意 |

## 4. データセット設計

実用的な言語／ゲームQA領域別にclean、既知バグ、曖昧な症状、依存関係不備を用意する。Case ID、target SHA、ground truth、出題・採点分離、モデル設定、プロンプト版、匿名化状況、反復回数を保存する。

## 5. 安全性

外部APIへ送信する前にアクセス許可、秘密情報、個人情報、未公開IPを検査する。Redactorだけで漏えいを防げると断定しない。モデル出力によるファイル操作・シェル実行を禁止／隔離し、パッチの変更パス・サイズ・テスト改ざんの有無を検査する。

## 6. Evidence

AI APIが無効な環境のテストは`LLM runtime NOT_EVALUATED`と記録。実際に有効化した実行でのみ、provider識別情報（秘密は除外）、モデル、対象SHA、各候補、検証ログ、承認の状態を保存する。

既存：[GENERATIVE_QA.md](../../09-portfolio/GENERATIVE_QA.md)。
