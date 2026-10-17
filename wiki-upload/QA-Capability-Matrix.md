# ゲーム・ソフトウェアQAの対応範囲

> **文書種別:** 技術解説への入口。本文中の将来計画は実装・実機検証・商用運用の達成を意味しません。

## 「対応している」の4段階
| レベル | 意味 |
|---|---|
| 未実装 | 実行経路が存在しない |
| 簡易実装 | 契約・静的検査・少数のケースを扱える |
| 本格実装 | 処理経路、異常処理、検証ロジックや回帰テストがある |
| 製品実装 | 安全性、権限、監視、障害対応、実案件での検証も揃う |

DIPではコード解析、AI修正候補、画像比較、GPUテレメトリ、負荷・Soak、ゲームエンジン、マルチプレイなど、複数領域の実装が確認できます。ただし、**すべての機能を実ゲーム・実GPUで検証したという意味ではありません**。

## 詳細な対応表
- [分野ごとの暫定評価と必要なEvidence](https://github.com/MKyosei2/development-intelligence-platform/blob/work/future-product-documentation/docs/08-testing/qa-capability-assessment.md)
- [Unity / Unreal実検証計画](https://github.com/MKyosei2/development-intelligence-platform/blob/work/future-product-documentation/docs/08-testing/real-engine-qa-test-plan.md)
- [Graphics / GPU実測計画](https://github.com/MKyosei2/development-intelligence-platform/blob/work/future-product-documentation/docs/08-testing/graphics-gpu-validation-plan.md)
- [負荷・Soak検証計画](https://github.com/MKyosei2/development-intelligence-platform/blob/work/future-product-documentation/docs/08-testing/performance-load-soak-test-plan.md)
- [Multiplayer / Game Replay計画](https://github.com/MKyosei2/development-intelligence-platform/blob/work/future-product-documentation/docs/08-testing/multiplayer-replay-and-gameplay-plan.md)

[AI QA](AI-QA-Architecture) / [設計書一覧](Technical-Decision-Index)
