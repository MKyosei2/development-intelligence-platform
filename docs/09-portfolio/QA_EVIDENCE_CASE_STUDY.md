# QA Evidence Case Study | 「進捗100%」でも、Criticalが残れば完成としない

> **30秒概要**  
> ゲーム制作で「実装完了・進捗100%」と報告された一方、QAで重大な不具合が残っていた場合、DIPは何を根拠に完了を拒否できるのか。**意図的に作ったCritical QA情報**をDIPへ入力し、**Quality Gate = Failed**、**Complete申告 = Contradicted** となることを確認する再現可能なテストを設計しました。  
> **これは実在の商用ゲーム障害の発見事例ではなく、品質判断ロジックの検証事例です。**

## 1. 背景と解決する課題

ゲーム制作では、担当者の進捗報告、CIの成功、QAの不具合報告、ゲームエンジン上の実際の状態が、別々の場所で管理されがちです。

たとえば、Unityプロジェクトでシーン内にMissing Referenceが残っているのに、タスク管理には「完成」と登録される可能性があります。ただし、**このケーススタディの自動テストは実Unity Editorを起動しません**。実際に投入するのは**Native/Universal QAのCriticalに相当する、決定的に再現できる合成レポート**です。

目的は、特定の実ゲームの問題を再現することではなく、**QAの根拠と進捗申告が矛盾する場合のDIPの判断**を確認することです。

### 先に結論：何が成功で、何が失敗か

| 判定対象 | 期待される結果 | 意味 |
|---|---|---|
| QAの製品品質ゲート | **Failed** | Criticalを見逃していない |
| 制作進行のComplete / 100%申告 | **Contradicted** | 根拠のない完成扱いを拒否した |
| 検証済み進捗 | **申告進捗より低い** | 申告値を検証値へ無条件に昇格させない |
| 矛盾の記録 | **1件以上** | 何が矛盾しているか記録されている |
| シナリオ検証スクリプト | **PASS** | 上記の「期待された失敗」が正しく生じた |

**製品品質ゲートがFAILEDで、テスト自体がPASS**というのが正しい結果です。品質に問題があるのにゲートがPassedになる場合、むしろテストは失敗すべきです。

## 2. 入力から判定まで：再現可能な8ステップ

```text
[1] DIP上に検証専用Projectを作成
           |
[2] Native/Universal QA相当のCritical Findingを合成して送信
           |
[3] Collector/APIがFindingを受け取りProjectのEvidenceへ
           |
[4] Quality Gate = Failed を確認
           |
[5] 同じProjectに Complete / 100% のClaimを登録
           |
[6] Project TruthのDashboardとClaim判定を取得
           |
[7] Claim = Contradicted / Verified progress < Reported progress
           |
[8] contradictionCount >= 1 を確認し、検証結果を保存
```

この構成で重要なのは、**申告を保存する機能**と**品質を判定する機能**を分離している点です。進捗管理側の数字を優先するのでも、単に警告を表示するだけでもなく、申告と根拠の間に明示的な矛盾状態を作ります。

### 実際の入力データの性質

[実行スクリプト](../../tools/publish_qa_scenario_evidence.py)は、以下の性質を持つQA情報を合成して送信します。

| 項目 | 内容 |
|---|---|
| `collectorType` | `native-qa-scenario` |
| 問題カテゴリ | `memory-safety` |
| 重大度 | `Critical` |
| 固有ルール | `DIP-SCENARIO-NATIVE-CRITICAL-001` |
| ソース改訂版 | `--expected-dip-sha` で指定した40桁SHA |
| 申告 | `claimedStatus: Complete`、100% |
| 位置付け | **deterministic test fixture（実運用の障害ではない）** |

レポートが収集経路を通ることと、判定結果が正しいことは別に確認します。

## 3. 何を自動アサートしているか

[E2Eテスト](../../tests/Dip.EndToEndTests/Scenarios/GameDevelopmentEvidenceScenario.cs)は、実Unity/Unreal Editorを必要とせず、DIPのAPI／判定の結合動作を確認します。

[API実行シナリオ](../../tools/publish_qa_scenario_evidence.py)は、レポート投入後に非同期処理の反映を待機し、DashboardおよびClaimの検査結果から次の条件を確認します。

| assertion | 確認内容 |
|---|---|
| `qualityGateFailed` | `qualityGateStatus == Failed` |
| `completionClaimContradicted` | Claimの `verificationStatus == Contradicted` |
| `verifiedProgressBelowReported` | `verifiedProgress < reportedProgress` |
| `contradictionRecorded` | `contradictionCount > 0` |

**すべて成立しなければシナリオはPASSにしません**。非同期処理が反映されず待機上限を超えた場合も、成功と誤認せず失敗として記録します。

具体的なコードの入口：
- [E2Eテスト：NativeQaCriticalEvidence_ContradictsCompletedClaim_AndFailsQualityGate](../../tests/Dip.EndToEndTests/Scenarios/GameDevelopmentEvidenceScenario.cs)
- [実API投入・検証：publish_qa_scenario_evidence.py](../../tools/publish_qa_scenario_evidence.py)
- [Project Truth assertionを最終Evidenceに要求：finalize_portfolio_evidence.py](../../tools/finalize_portfolio_evidence.py)

## 4. 実際に再現する手順

以下は**ローカルでDIPのAPI・DB・Workerを動かせる環境向け**です。採用担当者は実行せず、上記のコード・GitHub Actions・資料だけを確認しても構いません。

必要な環境や実行時の依存条件は[README](../../README.md)と[ローカルアーキテクチャ](ARCHITECTURE_OVERVIEW.md)を参照してください。

**ターミナルA：DIPの起動**

```powershell
# リポジトリのルートで実行
.\Start-DIP.ps1
```

**ターミナルB：現在のソース改訂版を指定してシナリオを実行**

```powershell
# リポジトリのルートで実行
$sha = (git rev-parse HEAD).Trim()

python tools/publish_qa_scenario_evidence.py `
  --api-base http://127.0.0.1:5080 `
  --expected-dip-sha $sha `
  --output artifacts/qa-project-truth
```

**生成される検証結果**

```text
artifacts/qa-project-truth/project-truth-evidence.json
```

`status = PASS`、`dipSourceCommit`、`scenario`、各assertionの真偽、およびProject Truth / Claimデータを確認します。実際に出力されたJSONを確認するまでは、この文書の例を「その改訂版での成功実績」とは扱いません。

## 5. なぜコミットIDへ結び付けるのか

検査結果は、**コードのどの改訂版に対するものか**が分からなければ、最新コードの品質保証として使えません。

このため、DIPの検証は40桁のソースSHAを記録します。最終集約では次の4系統のEvidenceが**同じDIP SHA**に対応していることを要求します。

```text
同一DIPソースSHA
    ├── Required CI matrix
    ├── Benchmark / Soak
    ├── QA Bug Zoo
    └── Project Truth expected-failure scenario
              |
              v
        Final Evidence
  SHA不一致・必須assertion不成立 -> 作成しない（fail closed）
```

- [同一SHA CIの検証コード](../../tools/verify_final_ci_matrix.py)
- [統合判定・ハッシュ生成コード](../../tools/finalize_portfolio_evidence.py)
- [公開前チェックリスト](PUBLICATION_CHECKLIST.md)

**参考として確認された過去のCI状態**：[`53e720f790c2d6e7243ee44b12ad6d5aae4f3db1`](https://github.com/MKyosei2/development-intelligence-platform/commit/53e720f790c2d6e7243ee44b12ad6d5aae4f3db1) において、11種類のGitHub Actionsが成功しています。ただし、**この文書を更新した後続のSHAや、このローカルQAシナリオの実行成功まで、その事実だけで証明できるわけではありません**。

## 6. どこまで言えるか／言えないか

| 言えること | そのままでは言えないこと |
|---|---|
| Critical QA情報を投入し、完了申告の矛盾を検証するシナリオを実装した | 実際のゲーム会社の不具合を発見・解決した |
| E2EテストがQAと進捗の判断経路を検証する | Unity/Unreal Editor上でこのシナリオを実機検証した |
| SHAを指定した検証結果を出力する仕組みを実装した | すべての後続コミットで同じ検証が成功した |
| 証拠不一致やassertion不成立でFinalizerを失敗させる設計とした | 24時間365日の商用本番運用が可能と実証した |
| AI提案と検証されたEvidenceを区別する方針である | AIによるすべての不具合の完全自動修復に成功した |

Unity向けCollectorや連携コードは存在しますが、**実対象ゲームでのEditorランタイム検証は別のEvidenceが必要**です。実プロジェクトを用いていない版は `NOT_EVALUATED` として扱います。

## 7. この事例から分かる設計力

このケースの技術的な核心は、「不具合を検知した」だけでなく、**見つかった不具合と制作進行の完了申告を突き合わせ、根拠のない成功判定を防ぐ**ことにあります。

開発者、QA、TA、進行管理担当者が別々のツールを使っていても、**同じコード改訂版・同じプロジェクトに紐付いた根拠**を参照して意思決定できることを目指しています。

この設計は、大学でゲームごとに手動・自動のデバッグを行ってきた経験を、個別作品から開発基盤全体へ発展させたものです。ただし、大学時代の非公開システム自体の実証とは分けて評価してください。

---

関連資料：[採用担当者向け概要](reviewer-brief.md) / [設計の成長記録](ENGINEERING_JOURNEY.md) / [ゲーム制作への適用例](GAME_DEVELOPMENT_SCENARIO.md) / [2〜3分のデモ構成](QA_EVIDENCE_DEMO.md)
