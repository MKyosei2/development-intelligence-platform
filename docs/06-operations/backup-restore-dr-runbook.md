# DB・Artifact・ジョブのバックアップ復元Runbook

> **文書ステータス:** 復元設計案 / 実データ破壊を伴う手順を自動実行しない  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 前提と安全境界
このRunbookは**操作設計**。商用環境で無確認のコマンドを実行してはならない。DB名、host、credential、backup location、backup暗号化Keyを環境ごとに検証し、既存顧客データを直接上書きしない。

## 2. データ群
| 対象 | バックアップ項目 | 復元時の注意 |
|---|---|---|
| PostgreSQL | Schema、Entity、Job、Outbox、Audit | 参照・権限・Migration版 |
| Artifact store | Raw log、画像、Capture、Release bundle | Object digest、メタデータ整合 |
| Config / Secret references | 設定版、Secretの参照ID | Secret値をBackupへ混在させない |
| Queue / Workers | JobStatus、Lease、Retry | 二重実行を防ぐ回復ポリシー |

## 3. 操作チェックリスト
- [ ] Restore先は本番と異なる**隔離されたDB**である。
- [ ] 取得Backup SHA256と復元前のファイルSHA256が一致する。
- [ ] PostgreSQL client (`psql`, `pg_dump`, `pg_restore`)の対象バージョン互換を確認した。
- [ ] Artifact一覧、件数、object hashが一致した。
- [ ] 復元直後はWorkerの消費を停止し、古いLeaseの再試行処理を検討する。
- [ ] 代表ProjectのClaim/Evidence/QA Runが整合し、別Projectへ越境できない。
- [ ] RTO/RPOは実測して記録した（未計測ならTBD）。

## 4. 代表的な障害別方針
| シナリオ | 優先度 | 先にすること | 復旧確認 |
|---|---|---|---|
| DB破損 | 高 | 書込停止、バックアップ検証 | Referential integrity |
| Artifact欠損 | 高 | 対象Evidenceを未検証扱い | Digest再検証 |
| Job重複 | 高 | Queue停止、Idempotency検証 | 副作用1回 |
| Secret漏えい | 最高 | Secret無効化・回転 | 旧token拒否 |
| Migration失敗 | 高 | 新旧アプリ互換確認 | Smoke test + DB check |

## 5. Evidenceと承認
Restore Date、Operators、BackupID、Backup/Artifact digest、DB version、DIP SHA、対応するIncident、復旧後のQA/Truth検証結果を記録する。**バックアップが存在するだけではRestore Verifiedではない。**

## 参照する実装・既存資料

- `docs/06-operations/backup-and-restore.md`
- `docs/09-portfolio/runtime-verification.md`
- `infrastructure/scripts/migrate-database.ps1`
