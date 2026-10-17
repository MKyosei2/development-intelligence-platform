# グラフィックス・GPU・画像差分 QA 実測設計

> **文書ステータス:** PNG比較のAs-Isコード確認 / 実GPU試験は未実証  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 領域を分離する
- **画像回帰:** PNGの色差、マスク、タイルSSIM、解像度違い。
- **描画セマンティクス:** Shader compile、Materials/Assets、render pass/captureの整合性。
- **GPU性能:** 実GPU由来のFrame Time p50/p95/p99、VRAM、draw calls、hitches等。
- **プロフェッショナルGPU Capture:** Captureが本当に生成され、対象SHA/環境に適合し、ツールが読み出せること。

## 2. 現行コードの接点
`PortablePngVisualComparator`はRGBAのChannel Tolerance=8、8x8タイルSSIM、Mask、寸法不一致、全ピクセルMaskを扱う。`GpuPerformanceExperimentRunner`は`DIP_GPU_METRIC`を使ったTelemetry契約と最小Sample数を扱う。これらが存在することは**実GPU結果を取得した証拠ではない**。

## 3. 実験条件の固定
| 必須記録 | 例 |
|---|---|
| Target build + SHA | Git commit + executable digest |
| Rendering stack | API、driver、shader compiler、GPU vendor/model |
| Display / image | resolution、HDR/SDR、color space、AA、VSync |
| Performance | power mode、thermal、background load、warm-up、sample count |
| Comparison | baseline SHA、tolerance、mask、reference approval、diff digest |

## 4. ケース
| ケース | 期待結果 |
|---|---|
| 同一PNG | Differenceなし／誤検知なし |
| 既知に変更したオブジェクト | 対応する領域で差分を検知 |
| カメラ位置が違う | 比較条件不一致で誤判定を避ける |
| 画像寸法が異なる | 有効な比較として扱わず理由提示 |
| 全画素Mask | 評価不能 |
| GPU計測が0件/少数 | 実測成功にしない |
| FrameTime退行 | p95/p99閾値による失敗判定 |
| Captureが古いSHA | 最新のVerifiedに使わない |

## 5. 測定と評価
正常シーン10+、意図的な視覚差分、非決定的描画（粒子/時間依存）を別グループで用意する。Precision/Recall、絶対色差、SSIM、False Positiveを比較し、Maskが不具合を隠していないかレビューする。GPUは複数回測定し中央値だけでなく分散・ヒッチ数を出す。

## 6. 判定境界
画像差分のPASSは描画の意味的正しさを保証せず、GPU Telemetry PASSは画質を保証しない。いずれもCPU側Frame Time、GPU側Frame Time、実際の描画再現を分けて表現する。

## 参照する実装・既存資料

- `src/Dip.Application/Features/QualityAutomation/VisualQa/VisualComparator.cs`
- `src/Dip.Infrastructure/P2Intelligence/GpuPerformanceExperimentRunner.cs`
- `src/Dip.Infrastructure/QualityAutomation/ProductionVerificationQaPack.cs`
- `docs/10-future/rfcs/RFC-003-GPU-VISUAL-VERIFICATION.md`
