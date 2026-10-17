# マルチテナント分離の選定判断書

> **文書ステータス:** Decision: 未決定 / 比較用  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 決めるべき問い
未公開ゲームコードとQA成果物を預かるサービスで、他社のビルド・ログ・GPU Runner・AI Promptが混ざらない分離方式をどう構築するか。

## 2. 選択肢比較
| 方式 | データ分離 | 実行分離 | 運用費用 | 適する用途 |
|---|---|---|---|---|
| テナント別インスタンス | 強め | 強め | 高 | Enterprise・機密が強い案件 |
| 共有API＋Project Row分離 | Application依存 | Runner別途 | 低〜中 | 個人向け低コスト構成 |
| 共有Control＋専用Runner/Storage | 混合 | 強めに設計可能 | 中〜高 | ゲーム/QAとSaaSの折衷 |

## 3. 選定に必要なエビデンス
- IDOR拒否、DB Query漏れ、Artifact URLの期限・署名、キャッシュのTenant key、Logの秘匿。
- Runnerが他テナントのmount/プロセス/ネットワークへアクセスできないこと。
- BackupのTenant別復元と削除、Auditが記録できること。
- ソースの量・同時User・Job負荷・価格条件における実コスト。

## 4. 推奨の進め方
最初は単一テナント＋専用Runnerで実運用に近い試験を行い、クロステナントの脅威テストが完了してから共有API方式と比較する。仕様を確定する際は、分離方式、許す残留リスク、障害時の影響境界、条件をADRへ記録する。

## 5. 不可逆な変更
データスキーマやArtifactのURLにTenantIdを追加すると移行負担がある。旧顧客データや古いキャッシュの扱いを事前に決め、段階移行とrollback可能性を評価する。

## 参照する実装・既存資料

- `docs/10-future/architecture/ARCH-002-MULTI-TENANCY.md`
- `docs/07-security/tenant-and-privacy-boundaries.md`
