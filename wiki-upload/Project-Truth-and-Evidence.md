# Project Truth / Evidence

## ClaimとEvidenceを分ける

**Claim:** 「この機能は完成」「進捗100%」など、人や管理システムによる申告。  
**Evidence:** QA結果、ビルド、実行ログ、計測、レビューなどの観測結果。

DIPはClaimをそのまま確定した真実として扱わず、Evidenceとの整合性を確認する方針です。データには対象プロジェクト、改訂版、収集元、時点を結び付け、古い検証を最新ソースへ流用しないことを重視します。

## 代表的な検証シナリオ

意図的に作ったCritical QAレポートと「Complete/100%」の申告を入力したとき、Quality Gateが`Failed`になり、Claimが`Contradicted`と判定されることを検証します。

これは**DIPの判定能力を試す合成テスト**であり、実際のゲーム会社で発見した不具合の実績ではありません。

## 何を信頼するか

- SourceがあるだけではRuntime Verifiedではない。
- Evidenceが存在するだけで、その出所・改訂版・改ざんの有無が確認できるとは限らない。
- 失敗した検査や実行環境の不足を`PASS`にしない。
- AIの仮説はEvidenceではなく、検証すべき提案として扱う。

実装と検証：[QA Evidence Case Study](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/09-portfolio/QA_EVIDENCE_CASE_STUDY.md)。

将来設計：[Evidence Chain](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/10-future/architecture/ARCH-004-EVIDENCE-CHAIN.md)。
