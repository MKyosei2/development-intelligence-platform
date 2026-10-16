# QA-001: ゲーム・ソフトウェアQAのカバレッジ設計

**Status: Proposed Coverage Policy** | **注意:** この表は対応済み一覧ではない。

## 1. 技術分野別の検査設計

| QA領域 | 検査例 | Evidence例 | 実施条件／限界 |
|---|---|---|---|
| 静的解析・ビルド | 構文、型、依存、ビルドエラー | コンパイラ診断、ルールID、対象SHA | 言語・ツール別カバレッジが異なる |
| ユニット／統合／E2E | テスト結果、回帰、APIシナリオ | JUnit等、失敗再現手順 | 実行・失敗の区別が必要 |
| メモリ・並行性 | ASan/UBSan、リーク、競合、デッドロック | sanitizerログ、スタック、設定 | OS/ビルド条件に依存 |
| 画像・レンダリング | Golden画像差、SSIM、シェーダー、HDR | 元画像、差分画像、キャプチャ | GPU/ドライバ差、色空間、許容閾値 |
| アセット・シーン | Missing Reference、Mesh、Texture、Material | Engine Collector report | 実Editor検証と静的解析を分離 |
| CPU/GPU/メモリ | フレーム時間p95/p99、GPU時間、VRAM | テレメトリ、ハード条件 | 実測と合成値を分離 |
| 負荷・Soak | 長時間利用、GC、メモリ増加、スループット | 時系列と異常時間帯 | 外部負荷・温度等の制御 |
| ネットワーク／マルチ | 遅延、損失、同期、desync | 同期ハッシュ、tick、リプレイ | 実複数クライアントの確認 |
| ゲームプレイ／物理 | 衝突、入力、セーブ、決定的リプレイ | リプレイ、期待アサーション | プロジェクト固有のオラクルが必要 |
| 音声・動画 | 音ズレ、音量、時間軸リグレッション | PCM差、フレーム差 | 人間の主観品質までは代替しない |
| アクセシビリティ | 操作・字幕・色覚・入力支援 | 対象規範、検査記録 | 自動化不能項目は人のレビュー |
| ローカライズ | 表示崩れ、禁則、文字コード | スクリーンショット、文字列差分 | 言語依存の人手確認 |
| 対応機種／認証 | Device Matrix、SDK、認証チェック | 実機記録、チェックリスト | ベンダー基準・実機が必要 |
| セキュリティ | 依存・秘密・外部入力 | スキャン結果と修正情報 | SASTだけで安全は保証できない |
| リリース | CI、SBOM、Provenance、承認 | 署名、対象SHA、artifact hash | 同じリビジョンか確認する |

## 2. カバレッジの表現

`Not Implemented / Contract Only / Implemented / Tested / Runtime Verified / NOT_EVALUATED`をQA分野・ターゲット環境ごとに記載する。単一の「全QA対応」という表現を避ける。

## 3. テスト設計

各領域に次のセットを設ける。

1. 正常なケース（False Positiveの検証）
2. 注入した既知不具合（Recallの検証）
3. 実データに近い境界ケース（過学習／過検知の検証）
4. 実行依存の欠損（NOT_EVALUATEDの正確性）
5. 修正後の再発防止（Regression）

## 4. 測定

Recall = TP/(TP+FN)、Precision = TP/(TP+FP)、False Positive Rate = FP/(FP+TN)。母数、ケース選定、除外ケース、同一ハッシュの重複を記録する。Synthetic Bug Zooの100%を未知の実案件へ一般化しない。

## 5. 参照

[実装状況](../../IMPLEMENTATION_STATUS.md) / [three-pillar contract](../../09-portfolio/THREE_PILLAR_PRECISION_CONTRACT.md) / [verification strategy](../verification/VERIFICATION-STRATEGY.md)
