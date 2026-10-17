# QA Adapter・ツール結果の詳細契約

> **文書ステータス:** As-Is 接点確認 / 新規共通仕様は提案  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 目的
言語・ゲームエンジン・解析ツールが異なっても、同じDIPのQA Runへ集約できるよう、検査の選択・実行・正規化・Coverage判定を分離する。**「検査対象が無かった」「ツールが無かった」「検査して問題が無かった」を混同しない。**

## 2. 役割と既存コード
| 層 | クラス | 契約 |
|---|---|---|
| Workspace | `IQaWorkspaceResolver`, `QaExecutionPlanner` | 検査先を許可されたパスとSHAに固定 |
| 技術検出 | `IRepositoryTechnologyDetector` | 言語/エンジン等の特性を返す |
| 選択 | `IQaAdapterRegistry`, `QaAdapterExecutionCoordinator` | 対応するAdapterとCrossCuttingPackを選ぶ |
| 実行 | `ProcessExecutor` | timeout、stdout/stderr、隔離Report、exit codeを扱う |
| 正規化 | `UniversalDiagnosticNormalizer`, `QaFindingProcessor` | Tool結果を統一Findingにする |
| 証拠 | `QaResultMaterializer`, `QaPostRunProcessor` | ArtifactとDIP記録へ反映 |

## 3. 結果の論理契約（実DTOではない）
| 項目 | 説明 | 不正入力時 |
|---|---|---|
| `tool`, `version` | 実行器の識別と版 | 不明の場合version unknown |
| `requested`, `available`, `executed` | 検査の必要性・利用可否・実行有無 | 未評価扱い |
| `status` | Success / Failure / Blocked / NotEvaluated | 不明状態をPASSにしない |
| `exitCode`, `timedOut` | 実行終了の事実 | 不在のとき成功を推定しない |
| `findingIds` | 重複排除可能なFinding | 空だけで完全カバレッジではない |
| `artifactDigest` | stdout、レポートの完全性 | 破損は無効 |
| `subjectSHA`, `dipSHA` | 対象・DIPのRevision | current verified不可 |
| `environment` | OS、エンジン、compiler、GPU | 実機比較に必須 |

## 4. 代表的な負例
`Unknown language`、コンパイラ不在、空Report、ヘッダだけのJUnit、既存の古いEditorログ、複数Adapterの重複出力、部分的に成功したバッチ、異なるSHAのTool成功。

## 5. エンジン別の境界
`GameProjectQaPack`はUnityの静的検査・Godotの限定検査を扱い、Unreal実行を内部で`UnrealProjectQaAdapter`へ委譲する経路を持つ。別途登録された同Adapterの実行との重複や、過去ログ由来の偽陽性をE2Eで検査する。Unity Editorが無い場合には、`UnityProjectQaAdapter`がEditMode/PlayMode/Buildを未評価として扱う。

## 6. 正式化の条件
1. 対象言語／エンジン単位で必要なToolと任意Toolを宣言する。
2. 必須Toolなし、Tool失敗、Finding0、Tool未実行を区別する。
3. `QaAdapterResult`、`QaToolExecution`の実装とスキーマ版を一対一で管理する。
4. カバレッジの欠損をQuality Gateへ伝播する結合テストを持つ。

## 参照する実装・既存資料

- `src/Dip.Infrastructure/QualityAutomation/GameProjectQaPack.cs`
- `src/Dip.Infrastructure/QualityAutomation/UnityProjectQaAdapter.cs`
- `src/Dip.Infrastructure/QualityAutomation/UnrealProjectQaAdapter.cs`
- `src/Dip.Application/Features/QualityAutomation/IQaAdapter.cs`
- `tests/Dip.Application.Tests/QualityAutomation/QaAdapterExecutionCoordinatorTests.cs`
