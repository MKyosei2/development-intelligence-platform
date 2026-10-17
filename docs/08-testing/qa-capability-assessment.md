# ゲーム・ツールQA対応深度マトリクス

> **文書ステータス:** コードと一部テストの存在確認 / 製品・実機実証は未確認  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 評価の基準
| レベル | 必須条件 |
|---|---|
| **未実装** | 動作する実行経路がコードから確認できない |
| **簡易実装** | 設定・契約検証・パーサ・静的検査・限定サンプルが中心 |
| **本格実装** | 対象を実際に処理する経路・明示的なエラー・回帰テスト・証拠の保存手段がある。ただし商用実証を含まない |
| **製品実装** | 権限/隔離/監視/運用/顧客利用/継続的実証が揃い、リリース基準を満たす |

**評価は「現行コードで確認できる到達レベルの暫定判定」であり、実機実験や網羅的コードレビューの結果ではない。** `Runtime Verified`は別のフラグであり、"本格実装"と同義ではない。

## 2. 分野別の暫定評価
| QA領域 | 暫定レベル | 理由（現存ソース） | 必要な追加Evidence |
|---|---|---|---|
| C#/C++/Pythonコード検査 | 本格実装（限定スコープ） | `CSharpQaEngine`, `CppQaEngine`, `PythonQaEngine`、Bug Zoo | 実案件Precision/Recall |
| 汎用言語パック/構文解析 | 簡易実装〜本格実装（言語別） | `DeepLanguagePackQaAdapter`, `UniversalSemanticAnalyzer` | 言語別tool実行と偽陽性測定 |
| QAオーケストレーション | 本格実装（コード経路） | Planner、Adapter Registry、Result Materializer | 複数プロジェクト並行運用 |
| LLMによる仮説・修正案 | 本格実装（コード経路） | OpenAI-compatible agent + parser + verified fix | 実APIプロバイダー使用の再現記録・精度 |
| 自律パッチのRed→Green | 本格実装（限定） | `VerifiedFixService`、制約付き実験 | 悪意あるテスト・多案件評価 |
| Unity実Editor/Player QA | 簡易実装〜本格実装（環境依存） | AdapterでEditMode/PlayMode/Buildの実行経路 | 固定対象ゲームの実Editor証拠 |
| Unreal Blueprint/Automation/Cook | 簡易実装〜本格実装（環境依存） | 専用AdapterとStatic検査 | UnrealEditor-Cmd実行とArtifact |
| アセット・シーン QA | 本格実装（ルール限定） | `AssetQaRuleEngine`, Unity/Maya/Unreal連携 | 実ゲームの正常/異常対比 |
| PNG画像回帰 | 本格実装（PNG比較） | `PortablePngVisualComparator`, tiled SSIM, masks | 実レンダ画像・画質評価 |
| Shader/Graphics semantics | 簡易実装〜本格実装（契約別） | Game/Production Verification pack | 実GPU/レンダAPIごとの正解照合 |
| GPUフレーム/VRAM解析 | 本格実装（Telemetry契約） | `GpuPerformanceExperimentRunner` | 実GPUからの連続計測 |
| CPU/RAM負荷、Soak | 本格実装（対象・環境限定） | `Soak`、Benchmark関連の実験経路 | 長時間・複数条件の再現性 |
| Multiplayer state/desync | 本格実装（Telemetry契約） | `MultiplayerQaExperimentRunner` | 実複数クライアント/遅延・損失 |
| Game play/physics | 簡易実装（契約・ケース依存） | Game regression/replay契約 | 実ゲームの期待結果Oracle |
| 音声/映像回帰 | 簡易実装〜本格実装（形式別） | Media Regression / Visual compare | 実素材の時間軸と主観レビュー |
| Localization/accessibility/input/save | 簡易実装（manifest契約） | `ProductionVerificationQaPack` | 実対象・自動＋手動検証 |
| Device farm/console certification | 簡易実装（契約中心） | Production Verification Manifest | 許諾のある実機・認証エビデンス |
| 商用テナント別の安全なQA実行 | 製品実装未達（基盤コードあり） | Sandbox/Role/Jobのコード | クロステナント攻撃・障害試験 |

## 3. 評価の注意
Production Verification Packは`.dip/production-verification.json`がある場合に適用される。これは各領域の検証契約を提供するもので、指定対象がなければ実ゲーム上の成功が確認できるわけではない。実際のサンプルmanifest・Runner・Artifact・同一SHAを揃え、欠落は`NOT_EVALUATED`とする。

## 4. 個別評価の証拠フォーム
`Category / Adapter / Tool available / Source SHA / Target SHA / Environment / Dataset / TP FP FN TN / Reproduction / RuntimeVerified / Known Limits / Owner / Next Action`。実測していない率や処理速度を推測で記載しない。

## 5. 次の優先順位
1. 実LLM APIを使ったQAの客観的評価。
2. 公開許諾済みUnityサンプルと、実EditorでのEditMode/PlayMode/Build。
3. 実GPUでのFrameTime・VRAM・画像比較。
4. 異常時の本物のSandbox拒否と別Projectの隔離試験。
5. Multiplayer/Soak/Audioなどの分野別実機マトリクス。

## 参照する実装・既存資料

- `src/Dip.Infrastructure/QualityAutomation/ProductionVerificationQaPack.cs`
- `src/Dip.Infrastructure/QualityAutomation/GameProjectQaPack.cs`
- `src/Dip.Infrastructure/P2Intelligence/GpuPerformanceExperimentRunner.cs`
- `src/Dip.Infrastructure/P2Intelligence/MultiplayerQaExperimentRunner.cs`
- `src/Dip.Application/Features/QualityAutomation/VisualQa/VisualComparator.cs`
- `docs/10-future/qa/QA-001-COVERAGE-MATRIX.md`

## レビュー・受け入れチェック

- [ ] 分野別の実機Evidence有無を埋めた
- [ ] 暫定レベルを一次テストにより更新した
- [ ] 一部ツールの契約存在を製品実装済みと書いていない
