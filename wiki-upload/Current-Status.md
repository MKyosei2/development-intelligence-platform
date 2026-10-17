# 現在の実装・検証状況と、将来構想との区別

## このWikiの読み方

「ソースコードがある」「契約を検査した」「ゲームエンジンで実動作を確認した」「本番で運用した」は異なる事実です。Wikiに構想が書いてあっても、その機能が実装・検証済みであることを意味しません。

## 特に区別する領域

| 領域 | 確認すべき証拠 |
|---|---|
| Project Truth | 実装、ユニット/E2E、対象SHAの実データ経路 |
| Generative QA | opt-inを有効化した**実LLM API**の結果と、その後の隔離検証 |
| Unity/Unreal | 実EditorやPlayerによる対象SHAの実行記録 |
| GPU/Graphics | 実GPU・ドライバ・API・解像度・画像差異などの記録 |
| 24/365の商用運用 | 本番監視、障害復旧、運用体制、実測のEvidence |

## 最新情報を確認する場所

- [実装状況](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/IMPLEMENTATION_STATUS.md)
- [GitHub Actions](https://github.com/MKyosei2/development-intelligence-platform/actions)
- [検証契約](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/09-portfolio/THREE_PILLAR_PRECISION_CONTRACT.md)
- [将来の状態定義](https://github.com/MKyosei2/development-intelligence-platform/blob/main/docs/10-future/02-STATUS-BOUNDARY.md)

**注意:** Wikiは常に最新のCI状態を自動取得するページではありません。「Current Revision PASS」は、対象SHAの証拠を確認してから判断してください。
