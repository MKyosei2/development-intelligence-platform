# Migration方針

- MigrationはDip.Infrastructureへ置く
- 本番起動時に自動適用しない
- Deployment工程で明示実行する
- Column削除には移行期間を設ける
- 大規模変更はLock時間を検証する
