# Project Truth / Claim判定の詳細仕様

> **文書ステータス:** As-Isの判断コードを確認 / 完全な評価表はレビュー対象  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 入力の型と区別
- **Claim:** 人間が報告する状態（Complete/Ready/InProgress等）。真偽の断定や人物評価ではない。
- **Evidence:** 観測されたTool/CI/QA/管理履歴。Revision、時間、Integrity、Confidenceによって適用性を制限。
- **Decision:** `Confirmed`, `PartiallyConfirmed`, `Unverified`, `Contradicted`, `Stale`等。列挙語彙は`ClaimVerificationStatus`とDTOを正とする。

## 2. 現行Evaluatorで確認できる処理
`ClaimVerificationEvaluator`はSubjectKeyで観測を絞り、申告時刻の24時間前以降の上位50件を候補にし、Pipeline Revision Mapとcurrent revisionを解決する。適用Evidenceを競合ポリシーで絞り、支持・矛盾・中立を判定する。**現行仕様の正確な境界はテストとコードが正本**であり、以下の表は主要ケースを整理したもの。

| Claim | Evidence（同一revision） | 条件 | 期待する判断の方向 |
|---|---|---|---|
| Complete / 100% | Critical QA failure | 高信頼・鮮度内・権威あり | Contradicted |
| Complete | 独立した複数の支持観測 | 少なくとも2種類の強い支持、強いWarningなし | Confirmed候補 |
| Complete | 古いSHAだけPASS | current証拠なし | Confirmed不可 |
| InProgress | 対象プロジェクトに一部成功 | 根拠が限定的 | PartiallyConfirmed/Unverifiedの判定条件をコードで確認 |
| Ready | 未評価の必須Engine検査 | 必須Coverageがない | 安易なConfirmed不可 |
| 完了否定（未完了） | 不具合がある | 失敗観測は未完了を支持し得る | 矛盾と誤認しない |
| 任意 | 期限切れ・別Subject・別Revision | 適用性がない | 現在の肯定根拠に使わない |

## 3. 判定順序の不変条件
1. 文言正規化では否定表現を肯定的な`Complete`より先に扱う。
2. 現在リビジョンを解決し、過去の支持を現在の支持に持ち込まない。
3. 競合解決は生データ削除ではなく、どのEvidenceを代表としたか説明可能にする。
4. 高信頼な矛盾がある場合は支持数が多いだけで消さない。
5. 不明・曖昧・矛盾した状況を無理に数値の正確さで隠さない。

## 4. 判定トレースの最低要件
`claimId`, `subjectKey`, normalized claim status, currentRevision, applicableEvidenceIds, excludedEvidenceIds, exclusionReason, contradictionIds, assessmentTimestamp, policyVersion`を出力設計の基準とする。フィールド名は設計案であり現行APIと一致するとは限らない。

## 5. モデル検証ケース
- 否定形・多言語ステータス、古いEvidence、複数PipelineRunのSHA、同一Source再送。
- 強いFailureと多数の弱いSuccess、Stageごとの新旧Revisionの交錯。
- 現在の失敗からDerived Riskへ影響し、以前のブロッカーだけで新RevisionをBlockedにしない。
- `Quality Gate Failed`なのにシナリオテストがPASSする例を製品PASSと混同しない。

## 6. 既知の設計質問
Evidenceの正確な独立性の定義、LegacyUnsignedをconfidenceへ含めるポリシー、異なるツールの競合時の優先権、Clock skew、Evidence windowの妥当性、異なるTarget SHAの混在。これらはRFC/ADRの採否対象。

## 参照する実装・既存資料

- `src/Dip.Application/Features/ProjectTruth/ClaimVerificationEvaluator.cs`
- `src/Dip.Application/Features/ProjectTruth/ClaimStatusNormalizer.cs`
- `src/Dip.Application/Features/ProjectTruth/EvidenceConfidenceEvaluator.cs`
- `tests/Dip.Application.Tests/ProjectTruth/ThreePillarPrecisionTests.cs`
- `docs/09-portfolio/QA_EVIDENCE_CASE_STUDY.md`
