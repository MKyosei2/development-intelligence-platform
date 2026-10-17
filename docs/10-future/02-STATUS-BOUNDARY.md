# 現在の実装と将来計画の境界

**Status:** Documentation Policy (proposed adoption)

## 1. 状態の定義

| 状態 | 必要な根拠 | 対外的な説明 |
|---|---|---|
| Proposed | 提案文書、議論 | 構想・検討中 |
| Accepted | 設計判断の承認記録（例：ADR） | 設計方針が確定、実装とは別 |
| In Development | 進行中のIssue / PR | 開発中 |
| Implemented | 対象機能のソース | コード上は実装済み |
| Tested | 対象SHAの適切なテスト | 該当するテストの範囲に限り検証済み |
| Runtime Verified | 実対象環境・対象SHAの実行結果 | 実機・実エンジンなど具体的な環境で確認 |
| Performance Verified | 実行環境、負荷条件、対象SHAを伴う測定 | 測定範囲に限る性能実績 |
| Released | 完了条件と成果物・タグを確認 | リリース版として公開済み |
| Production Deployed | デプロイ、稼働、監視・復旧の証拠 | 定義済み本番運用条件で稼働実証 |
| Superseded | 後継設計へのリンク | 新設計に置換 |

状態は**仕様**と**機能**と**検証結果**のそれぞれに付く。たとえば「RFC Accepted」「コード Implemented」「Unity Runtime NOT_EVALUATED」は両立する。

## 2. ソースが存在する領域（実動作を無条件に保証しない）

既存リポジトリには、Project Truth、Evidenceモデル、各種言語QA、Quality Gate、AI候補生成の接続、隔離修正検証、Unity/Unreal連携、性能試験、Release Readinessなどのソースやテストがある。実装状況の詳細は[現行の実装状況文書](../IMPLEMENTATION_STATUS.md)を参照する。

次を混同しないこと。

- Unity/UnrealのCollector・契約テストと、**実Unity/Unreal Editor上の実動作**
- GPUテレメトリの解析コードと、**実GPUによるキャプチャ・描画品質の網羅的検証**
- LLM対応コードと、**実APIを用いた修正成功の再現可能な実証**
- CI成功と、**商用24時間365日運用・障害復旧の実証**
- 合成QAケースの成功と、**実際の商用ゲームの障害を検出した実績**

## 3. Evidenceに要求する最低限のメタデータ

`project / source revision (SHA) / target revision / environment / tool and version / command or procedure / start and end time / exit or evaluation status / artifact hash or URI / limitation / provenance`を可能な範囲で記録し、欠損時は未評価または制約ありとして扱う。

## 4. 動的な状態を固定文書に書き込まない

「最新SHAでCIがすべて成功」「GPUでFPSがXX」「商用運用済み」などは、根拠リンクと計測日時がない限り記載しない。現況はGitHub Actions・対象リリース・最新の検証成果物を確認する。

## 5. 関連資料

- [three-pillar precision contract](../09-portfolio/THREE_PILLAR_PRECISION_CONTRACT.md)
- [runtime-verification](../09-portfolio/runtime-verification.md)
- [QA case study](../09-portfolio/QA_EVIDENCE_CASE_STUDY.md)
- [Release acceptance](verification/RELEASE-ACCEPTANCE.md)
