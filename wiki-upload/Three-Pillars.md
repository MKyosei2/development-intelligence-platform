# DIPの三つの柱

## 1. Project Truth / Evidence

開発上の「完了」という申告と、コード・QA・CI等の観測された根拠を分けて管理します。重大な品質問題が残るときに、理由のない完了申告を無条件に採用しないための考え方です。

## 2. Game Development QA

C#/C++/Pythonなどのソース解析に加え、ゲームエンジン、アセット、視覚的な差異、性能、ネットワーク、回帰テストなどの異なる品質確認を共通のProject Evidenceとして扱うことを目指します。

**重要:** 分野によって対応の深さは異なります。契約・解析コードがあるだけで実機での完全な検証を達成したとは表現しません。

## 3. Verification / Release Operations

テスト、ベンチマーク、セキュリティ、品質ゲート、プロジェクトの進捗根拠を整合する改訂版へ結び付けます。古いPASS、実行不能、未評価を、現在のリリース準備完了へ読み替えない方針です。

## 三つがつながる例

```text
Critical QA Finding
       |
       v
Quality Gate: Failed
       |
       +---- 進捗申告: Complete / 100%
                         |
                         v
               Project Truth: Contradicted
                         |
                         v
               Release Readiness: 検証条件を評価
```

これはDIP自身の合成QAテストの代表例であり、実在する商用ゲームの障害報告を意味しません。

[詳しい例](Project-Truth-and-Evidence) / [ゲームQA](Game-Development-QA) / [リリース](Verification-and-Release)
