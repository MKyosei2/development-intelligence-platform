# DB移行・整合性・保持期間・復元設計

> **文書ステータス:** As-Isの既存マイグレーション方針 / 実運用手順は提案  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 移行の基本契約
EF Core Migrationを採用する。`202608120001_InitialBaseline`と`202608120002_AddDurablePlatformIntelligence`などの版は、実際のMigrationツリーを正本とする。旧`EnsureCreated`系DBの自動採用は、検証に失敗する場合は拒否する。

## 2. 環境と段階
| 段階 | 作業 | 検証 / 次へ進む条件 |
|---|---|---|
| 事前 | DB URI、所有者、機密の取扱、空き容量 | 権限・接続・バックアップ確認 |
| Backup | DB Dump＋Artifactの整合Snapshot | Digest検証、別環境でRestore |
| Dry run | 一時DBへマイグレーション | Migration exit成功＋行数/制約比較 |
| Deploy | 現行アプリと互換なExpand変更 | 旧/新Appの両方が動くこと |
| Cutover | Worker停止/Drainingが必要なら適用 | Job滞留・書込整合性が保たれる |
| Contract | 未使用列削除など | 旧Appが存在しないこと、rollback計画 |

## 3. 危険なケース
- 同時に複数API/WorkerがMigrationを実行して二重DDLを発行する。
- 大規模表へNOT NULL列を無検証追加し、長時間ロックする。
- Artifactの削除がEvidenceの履歴参照を破壊する。
- バックアップと現行DBを違う時点で採取して参照先が欠落する。
- 復旧後のJobが重複再実行され、外部通知等の副作用を再発生させる。

## 4. データ種別別の提案保持方針
| 種別 | 保存・削除の論点 |
|---|---|
| Evidence / Claim | 監査要件、修正履歴、訂正記録、法的保持要請 |
| 生ログ | Secret/PIIを可能な限り保存しない、短い保持期間を検討 |
| スクリーンショット/GPU capture | 未公開IPを含む、アクセス管理・暗号化・期限 |
| AI Prompt/Response | ソース漏えい、顧客同意、外部API保持ポリシー |
| Artifacts | digest、所有権、参照元がある間はGC防止 |
| Jobs | 最終結果と中断履歴を保持、機密Payloadを期限付き削除 |

現時点で数値や期間を勝手に確定せず、製品化時に契約・法務・運用費用で決める。

## 5. 必須試験
クリーンDB・旧DBからMigration後にAPIを起動する。Migration途中kill、重複実行、互換性違反、バックアップ復元後のEvidenceのSHA・Hash検証まで行う。記録にはDBバージョン、DIP SHA、移行開始/終了、Restoreの所要時間を残す。

## 参照する実装・既存資料

- `docs/03-database/migration-policy.md`
- `docs/09-portfolio/runtime-verification.md`
- `src/Dip.Infrastructure/Persistence/`
- `infrastructure/scripts/migrate-database.ps1`

## レビュー・受け入れチェック

- [ ] Migration実行前の復元可能なバックアップがある
- [ ] Schema expand/contractの互換ウィンドウが記録された
- [ ] 復元後にArtifactとEvidenceの参照整合を検証した
