# 生成AI QAの再現可能な評価実験仕様

> **文書ステータス:** Proposed Experiment Protocol / 実測値なし  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 主張したい内容を明確にする
「AIを組み込んだ」「AIがバグを発見した」「修正できた」「安全に繰り返し使える」は別の主張である。各項目をデータセット・入力・モデル版・実行ログ・採点基準で別評価する。

## 2. 実験群
| Group | 内容 | 比較意義 |
|---|---|---|
| A | Deterministic QAのみ | 基準の検出力・時間 |
| B | ローカル非LLMの仮説/修正候補 | LLM以外の自動化の価値 |
| C | 実LLM（opt-in）＋VerifiedFix | LLMによる付加価値 |
| D | LLM提案＋検証なし（評価専用、merge禁止） | Verificationを外した時の危険性 |

## 3. データセット分割
- C#/C++/PythonのBug Zoo（既知問題）、正常ケース、未見の独立リポジトリ、ゲームエンジン由来ケースを**別群**にする。
- 問題をモデルへ公開する前にGround Truth、期待修正、失敗・合格の判定テストを固定。
- 生成器がテストを無効化する抜け道を採点者が別検出する。
- 各ケースには`CaseId`, target SHA, language, bug type, severity, private/public status, reuse restrictionsを記録。

## 4. 計測項目
| 指標 | 定義 | 禁止する近道 |
|---|---|---|
| Detection Precision | TP/(TP+FP) | 異常例のみで計算 |
| Detection Recall | TP/(TP+FN) | 既知の成功ケースだけで測る |
| Root Cause Accuracy | Ground Truth一致件数 / 評価可能ケース数 | Evidenceの引用だけを正解扱い |
| Patch Applies | `git apply`が成功した候補比率 | 修復成功と同じとみなす |
| Verified Fix Rate | Red→Green、全required実験成功、重大退行なし | テストを弱めてGreen |
| Unsafe Proposal Rate | 禁止操作を提案した候補比率 | Rejectされた候補を分母から除く |
| Time / Cost | 試行・トークン・Runner費用 | 人手時間や再試行を除外 |
| Regression Rate | 修正で新たに生じた故障割合 | 未評価をゼロ件にする |

## 5. 実験ログ最小項目
`runId`, `caseId`, `dipSHA`, `targetSHA`, `provider`, `model`, `promptVersion`, `temperature等`, `maxTokens`, `requestDigest`, `responseDigest`, `hypothesisId`, `proposalDigest`, `sandboxReport`, `baseline/RED/GREEN`, `toolExit`, `artifactDigest`, `finalDecision`。APIキーや未公開ソース本文を公開ログへ残さない。

## 6. プロトコル例
1. 固定データセットをSHA付きでクローン。
2. 各群で同じTimeout・Runner・試行上限を設定。
3. プロンプト注入（ログ内の悪意ある指示）や未知Evidence IDなどの負例を含める。
4. Red→Greenの判定は独立した品質ゲートが行う。
5. 率に対して母数と信頼区間を示し、モデル版の変化を記録。
6. ケースの少なくとも一部をダブルレビューし、主観ラベルを監査する。

## 7. 成功判定
**現時点では閾値未決定。** 実験開始前にAcceptance Criteriaと統計的母数を固定し、結果を見てから閾値を後付けしない。外部API未使用なら「LLM Runtime NOT_EVALUATED」。

## 参照する実装・既存資料

- `docs/10-future/qa/QA-004-LLM-EVALUATION.md`
- `docs/09-portfolio/GENERATIVE_QA.md`
- `src/Dip.Infrastructure/P2Intelligence/OpenAiCompatibleFixProposalAgent.cs`
- `tests/Dip.Infrastructure.Tests/P2Intelligence/OpenAiCompatibleFixProposalAgentTests.cs`
