# DIPの環境別配置・デプロイ・ロールバック設計

> **文書ステータス:** 現行構成を元にした手順案 / 商用本番稼働は未実証  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 配置モデル
| 環境 | 位置付け | 必要な確認 |
|---|---|---|
| Local Development | Source変更、ルール検証 | .NET/Python/PostgreSQL、開発用credential |
| CI/E2E | 変更SHAに対する自動検査 | Clean checkout、test DB、test artifacts |
| Portfolio demo | 説明・再現 | デモ用データと実データの区別 |
| Production Candidate | 将来提供する構成 | TLS、Backup、Runner隔離、Auth、監視、DR |

## 2. 本番候補の論理構成
`Browser → TLS Termination → Web / API → PostgreSQL & Artifact Store → Durable Worker → QA Runner / Python Analysis`。監視はOpenTelemetry/Prometheus/Grafanaなど既存資産を利用候補とし、実際の構成・バージョン・開放Portは別の運用環境情報と照合する。

## 3. デプロイ前ゲート
- 必須GitHub Actionsの対象SHAがGreen、DB MigrationのDryRun成功。
- Secretsが例示値でない。`ProductionSecurityGuard`のProduction要件を満たす。
- Artifact保存先、Backup/Restore、Runnerが必要なQAの隔離・権限・ネットワークを確認。
- エンジン・GPUが無ければ該当項目を`NOT_EVALUATED`として明示。

## 4. Deploy / Rollback提案手順
1. アプリとDBの変更点、依存関係、互換性、影響範囲をManifestに記録。
2. DBバックアップとArtifact snapshotを取得し、別環境Restoreを確認。
3. 新コードをstagingで起動し、Auth・Project/QA・EvidenceのSmoketestを行う。
4. 必要ならWorkerをDrainしてMigration実行、以後新ワーカーを起動。
5. カナリア/段階的切替を検討。失敗時はDNS/LBまたはprocess versionを戻す。
6. DBが非互換になる場合、アプリのrollbackだけで回復できない。Expand/Contractを前提に復旧計画を定義。

## 5. 故障の例
`DB unavailable`、`Worker Job stuck`、`Artifact unavailable`、`Auth provider down`、`LLM quota exceeded`、`Sandbox not configured`を個別に可観測化し、復旧手順を定義する。

## 6. 記録するEvidence
DIP SHA、Deployment ID、Migration set、artifact digest、Operator（匿名化ID）、切替時刻、失敗時の復旧ポイント、smoke test結果、監視ウィンドウの異常数。

## 参照する実装・既存資料

- `docs/WEB_DEPLOYMENT.md`
- `docs/09-portfolio/runtime-verification.md`
- `docs/02-architecture/deployment-diagram.md`
- `src/Dip.Api/Authorization/ProductionSecurityGuard.cs`
- `infrastructure/monitoring/`
