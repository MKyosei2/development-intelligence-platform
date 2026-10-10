# Engineering Journey | 個別ツールから、根拠に基づく開発基盤へ

> この文書は機能一覧や成功談ではありません。**何を難しいと感じ、何を作り直し、どこまで検証できたか**を、コード・設計資料・検証手順と対応させた開発記録です。  
> 要約：以前は「コードが存在する／手元で動く」ことに重きを置いていました。DIPの統合・改善を通じて、**「どの改訂版を、何の根拠で、どの環境で検証したのか」まで設計対象にする**ようになりました。

入口：[採用担当者向け概要](reviewer-brief.md) / [QA Case Study](QA_EVIDENCE_CASE_STUDY.md) / [アーキテクチャ](ARCHITECTURE_OVERVIEW.md)

## 1. 出発点：なぜDIPが幅広いのか

DIPは、2026年7月にすべてをゼロから書き始めたシステムではありません。作者には以下の開発経験・既存資産がありました。

- 大学4年間のゲーム制作で、**作品ごとに約8本のデバッグシステムを構築**。手動検査と自動検査を使い分け、コード・挙動・グラフィックスの問題発見から修正までを担当。
- 前回の就職選考期間中から、**友人に依頼された制作進行系システム**を開発。完成前のものも存在。
- 個人的な関心から、用途の異なる小さなツールやソースコードを個別に制作。

この時点では、機能やデータは分散しており、同じプロジェクトの「作業の申告」と「品質検査の結果」を突き合わせるための共通基盤はありませんでした。

**前回の選考後の挑戦**は、これらを単に一つのリポジトリへ移すのではなく、**制作進行・QA・Git・CIなどの情報を同じプロジェクト情報に結び付けられる製品アーキテクチャへ発展させること**です。新しいAI QAや検証機能もこの段階で開発・統合しました。

> **事実の扱い**：大学授業のデバッグシステムと友人からの依頼内容は作者本人の経験に基づく説明で、元コードはここでは公開されていません。後述のテストやCIは**DIPの公開実装**を示すものであり、非公開の約8本を技術的に証明するものではありません。統合前のすべての資産が完成品だったとも主張しません。

## 2. 成長の要点：7つの設計判断

| 改善の対象 | 最初の考え方・課題 | 変更した設計 | 評価可能な根拠 |
|---|---|---|---|
| システムの範囲 | 進捗、QA、解析が個別機能 | Project Contextを軸に統合し、情報源ごとの責務を分離 | [Architecture](ARCHITECTURE_OVERVIEW.md) |
| 品質の判定 | 「完了した」という報告と検査結果が並存 | ClaimとEvidenceを別モデルにし矛盾を判定 | [QA Case Study](QA_EVIDENCE_CASE_STUDY.md) |
| AIの使い方 | 修正候補のもっともらしさだけでは安全性を判定できない | Hypothesis→Candidate→Isolated Verification→Human Review | [README](../../README.md) |
| テスト結果の鮮度 | 以前のPASSが最新コードの保証に見える | 同一SHAに結び付くEvidenceだけを当該版の根拠に | [CI matrix verifier](../../tools/verify_final_ci_matrix.py) |
| QA環境の失敗 | Timeout・実行環境制約が製品不具合と混ざる | Product FailureとInfrastructure Failureを区別 | [最新Evidence方針](evidence/latest-revision.md) |
| ゲームエンジン連携 | 連携コードが存在すれば動作済みと見なされかねない | 契約テストと実Editorの検証を分離 | [Game Development Scenario](GAME_DEVELOPMENT_SCENARIO.md) |
| リリース判定 | 個別検証が成功しても結果の改訂版が揃わない | CI・benchmark・QA・Project Truthを同一SHAで束ねる | [Finalizer](../../tools/finalize_portfolio_evidence.py) |

### 2.1 「完成した」と「完成を裏付けられる」は異なる

ゲーム制作のデバッグでは、不具合を修正して再確認する経験を積みました。しかし複数プロジェクト・複数検査系を扱う場合、**誰が何をもって完了と判断したか**も不具合の要因になります。

たとえば、制作進行側が `Complete / 100%` と報告しても、QA側が `Critical` を検出していれば、その申告はそのままでは採用できません。DIPではClaimとEvidenceを分離し、品質ゲートとProject Truthによって矛盾を記録します。

```text
Before: 「進捗100%」 ──> 完了という表示だけが残るおそれ
After : 「進捗100%」 ──┐
                       ├──> Evidence照合 ──> Contradicted / Quality Gate Failed
         Critical QA ──┘
```

この挙動は**意図的に作ったCriticalデータ**を使った再現可能なE2E・実APIシナリオで検証します。実運用上の欠陥を検出したという主張ではありません。

コード：[E2Eシナリオ](../../tests/Dip.EndToEndTests/Scenarios/GameDevelopmentEvidenceScenario.cs) / [APIによるシナリオ実行](../../tools/publish_qa_scenario_evidence.py)

### 2.2 「AIが直せそう」では修正完了にしない

AIを活用するほど、AIが出した修正案と**実際に検証された修正**を分ける必要があります。DIPは `Hypothesis → Candidate → Isolated Verification → Human Review` という責務を分離し、提案そのものをテスト成功やリリース許可と取り違えない方針です。

これは「AIによる完全自動修復を実現した」という意味ではありません。**提案と安全性の判定を分離する設計**が成長点です。具体的な適用範囲はソースと各検証記録で評価してください。

### 2.3 同じコードの証拠しか、そのコードの証拠ではない

あるコミットで全テストが通っても、コードを変更した後に無条件で成功を継承すれば、最新状態を保証したことにはなりません。

そこで、各検証に**DIPソースの40桁SHA**を結び付け、CI matrix・benchmark・QA Bug Zoo・Project TruthのSHAが一致しなければFinal Evidenceを作らないようにしました。未実行・中断・環境都合の失敗も成功へ変換しません。

- [CI 11項目の同一SHA確認](../../tools/verify_final_ci_matrix.py)
- [4系統のEvidenceの統合判定](../../tools/finalize_portfolio_evidence.py)
- [現在の改訂版の証拠ポリシー](evidence/latest-revision.md)
- [公開前チェックリスト](PUBLICATION_CHECKLIST.md)

**検証済みと呼ぶ条件を厳しくすること**自体が、初期開発からの大きな考え方の変化です。

### 2.4 不具合と、検査できなかったことは異なる

C++ QAでは、CTestがタイムアウトしたという事実から、ただちに製品にCriticalなハングが存在すると断定することはできません。逆に、実行できなかった検査をPASSにもできません。

そのため、CTestを検査の制御情報として、sanitizer付きの実行経路をメモリ安全性の根拠として分け、timeout、exit code、stdout/stderr、メモリ使用量、sandboxなどの診断情報を残すようにしました。Windowsの実行制約など、環境特有のブロックも明示します。

この改善は、**検出精度の議論をできるようにするための前提条件**です。「失敗の原因が分からない」状態を、検証対象から除外するのではなく、原因を追える記録へ変えます。

- [QA determinism実行手順](../../scripts/DIP_QA_BUG_ZOO_DETERMINISM.ps1)
- [READMEのC++ QA境界](../../README.md#c-qa-determinism--timeout-evidence-boundary)

### 2.5 Unity/Unreal連携の「実装」と「実機検証」を切り分ける

ゲーム開発向けの連携ソースやcontract/unitのテストがあっても、**実際のUnity/Unreal Editorと対象ゲームプロジェクトで動いた証拠にはなりません**。

実対象プロジェクトがない版については `NOT_EVALUATED` と明記し、Editor実行結果がある場合のみ別途Evidenceを作る方針に変更しました。これにより、ポートフォリオの説明と検証結果を混同しなくなりました。

- [Game Development Scenario](GAME_DEVELOPMENT_SCENARIO.md)
- [最新revisionの検証条件](evidence/latest-revision.md)

### 2.6 CIを運用コストも含めて設計する

GitHub-hosted runnerの利用量を踏まえ、通常作業はローカル検証を基本とし、必要なときの手動実行・最終提出時のrequired workflowsを分けました。

テスト数を増やすだけでなく、**いつ、どの根拠を作るか**を管理する設計へ移行した点が重要です。詳細は[CI実行方針](CI_EXECUTION_POLICY.md)を参照してください。

### 2.7 Final Evidenceを一つの改訂版として束ねる

CIがGreenでも性能計測が古い場合、リリースの根拠は揃っていません。DIPでは、**CI / benchmark / QA Bug Zoo / Project Truth**の改訂版一致と必要なassertionを要求し、成立しなければ最終証拠を作成しないようにしています。

対応コード：[Finalizer](../../tools/finalize_portfolio_evidence.py) / [公開前Checklist](PUBLICATION_CHECKLIST.md)

## 3. どこまで実証できたか：時点を固定して述べる

**歴史的な検証例**：[`53e720f790c2d6e7243ee44b12ad6d5aae4f3db1`](https://github.com/MKyosei2/development-intelligence-platform/commit/53e720f790c2d6e7243ee44b12ad6d5aae4f3db1) では、11種類のGitHub Actionsワークフローの成功を確認しています。

ただし、後続コミットでドキュメントを変更した場合でも、**その新SHAのCIが自動的にPASSしたことにはなりません**。最新の実績を確認するには、[GitHub Actions](https://github.com/MKyosei2/development-intelligence-platform/actions)と[Evidencesの同一SHA判定手順](PUBLICATION_CHECKLIST.md)を参照してください。

| 区分 | 何を意味するか |
|---|---|
| Implemented | コードとして実装されている |
| Tested | 該当コード改訂版で関連テストが成功した |
| Runtime Verified | 該当改訂版を実対象環境で動かした記録がある |
| NOT_EVALUATED | 対象環境の検証を実施していない |
| Production Deployed | 実際の本番運用の証拠がある場合のみ主張可能 |

## 4. 今後の挑戦：開発基盤を実サービスにするには

DIPは将来的に、企業・個人クリエイターの**制作進行からQAまでを一貫して扱う、24時間365日稼働のサービス**として提供することを目指しています。

ただし、現在は個人開発・検証段階です。次のハードルとして、実ゲームプロジェクトを用いた検証、運用監視、障害復旧の実証、負荷・長期稼働の測定、利用者ごとの権限・データ保護などがあります。これらを達成済みとは書きません。

**前回からの成長を一文でまとめるなら**、ゲームごとの不具合を「発見して修正する」経験を土台に、**不具合・進捗・検証履歴をつなぎ、根拠のない完成判定を防ぐ仕組みを設計する段階へ進んだ**ことです。

---

関連：[Reviewer Brief](reviewer-brief.md) / [Case Study](QA_EVIDENCE_CASE_STUDY.md) / [2〜3分デモ](QA_EVIDENCE_DEMO.md)
