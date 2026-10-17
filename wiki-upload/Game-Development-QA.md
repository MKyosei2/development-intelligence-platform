# Game Development QA

DIPのGame QAは、ソースコードだけでなく、ゲームエンジン、描画、性能、アセット、ネットワーク、ゲームプレイ固有の検査までを対象として考える領域です。

## 分野

| 分野 | 具体例 | 注意点 |
|---|---|---|
| Code QA | 静的解析・ビルド・ユニット・Sanitizer | 言語ごとに検査深度が異なる |
| Engine QA | Unity/Unreal EditMode、PlayMode、Build等 | 契約テストと実Editor検証は別 |
| Visual QA | Golden画像、差分、SSIM、シェーダー | 誤検知・色空間・GPU差がある |
| Performance QA | Frame time、GPU time、VRAM、Soak | 実機と実条件が必要 |
| Gameplay QA | Replay、Physics、入力、Save互換 | 作品固有の期待値が必要 |
| Multiplayer QA | Loss、Latency、Jitter、Desync | 実複数クライアントを使った検証が必要 |
| Release QA | Platform要件、セキュリティ、Evidence | プラットフォームごとに制約が異なる |

## AIによる支援

AIに入力したEvidenceから仮説と修正候補を提案させ、隔離したワークスペースでテストし、最終的に人がレビューします。生成AI自体に品質の合否を決めさせないのが基本方針です。

## 詳細設計

- [QA Coverage Matrix](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/10-future/qa/QA-001-COVERAGE-MATRIX.md)
- [Visual/GPU設計](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/10-future/qa/QA-002-VISUAL-AND-GPU.md)
- [Gameplay/Multiplayer設計](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/10-future/qa/QA-003-GAMEPLAY-MULTIPLAYER.md)
- [AI QA評価](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/10-future/qa/QA-004-LLM-EVALUATION.md)
- [現行Generative QAの説明](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/09-portfolio/GENERATIVE_QA.md)

**対象環境で測っていない部分は未評価とします。**
