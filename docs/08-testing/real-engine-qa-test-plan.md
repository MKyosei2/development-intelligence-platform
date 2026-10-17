# Unity / Unreal実エンジン検証計画

> **文書ステータス:** Proposed test plan / 実Editor実行結果なし  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 目的
Collector/Adapterのコードや契約テストと、**実エンジンを起動してゲームを検証すること**を分ける。Unity/Unrealの双方に実装経路はあるが、本書は最新SHAで実Editor成功を報告しない。

## 2. 最小の対象マトリクス
| Engine | Scenario | 正常系 | 異常系 | 記録 |
|---|---|---|---|---|
| Unity | EditMode test | 全テスト合格 | 既知の失敗を注入 | Editor version、JUnit、stdout |
| Unity | PlayMode test | scene正常起動 | null/missing reference | Editor、Player設定、再現ログ |
| Unity | Scene Smoke | 必須scene読込 | Missing Script | Scene ID、Editorログ、Artifact |
| Unity | Player Build | Build成功 | 意図的compile error | Build Target/CI/ExitCode |
| Unreal | .uproject検証 | 正常descriptor | JSON不正 | Descriptor hash |
| Unreal | Blueprint compile | 成功 | compile不具合 | UnrealEditor-Cmd版とログ |
| Unreal | Automation | 期待Assertion | known failure | TestRun report |
| Unreal | Cook / Package | 成功 | missing asset / target | Tool版、成果物digest |

## 3. 実行手順
1. 許諾済みの小規模ゲームを別Gitリポジトリで管理し、テスト用正常SHAと故障SHAを固定。
2. DIP SHA、Target SHA、OS、エンジン、SDK、パッケージlock、Player target、起動コマンドを固定。
3. 空のWorkspaceから取得・実行。既存`Logs/`は新しい実行ログと混同しない。
4. Adapterが正しいTool選択・カバレッジ・エラーを返すか確認。
5. `Finding → Evidence → Project Truth → Release`を同一対象SHAでE2E検証。
6. Runner不在、Editor不在、License問題を製品バグではなく適切な失敗分類にする。

## 4. 注意点
- Unity AdapterはEditorが存在しないと`NOT_EVALUATED`として明示するコード経路がある。
- Unreal Adapterは`UnrealEditor-Cmd`不在時にUnavailableを返すが、既存ログを読む処理もある。**過去ログを現行実行のPASSと誤認しない**検証が必須。
- `GameProjectQaPack`からUnreal Adapterへ委譲する経路と、Registryの専用Adapter選択との重複実行・Finding重複を確認する。
- Engine license、CI runner、GPU、OS依存は検証環境要件として記録する。

## 5. 完了証拠
実行ログ（発生時刻付き）、Command、Tool version、DIP SHA、Target SHA、screenshot/trace、原本digest、成功/失敗の期待値、実測結果。片方のエンジン成功で他方をVerifiedにしない。

## 参照する実装・既存資料

- `src/Dip.Infrastructure/QualityAutomation/UnityProjectQaAdapter.cs`
- `src/Dip.Infrastructure/QualityAutomation/UnrealProjectQaAdapter.cs`
- `src/Dip.Infrastructure/QualityAutomation/GameProjectQaPack.cs`
- `collectors/unity-collector/`
- `collectors/unreal-collector/`
- `docs/10-future/rfcs/RFC-001-REAL-ENGINE-QA.md`
