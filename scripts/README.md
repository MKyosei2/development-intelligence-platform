# DIP 運用スクリプト — Native-first

DIP が正式にサポートする実行方式は、ホストOS上で直接動作させる **native-first** 構成です。Docker Desktop / Docker Engine や外部Queue daemonは必須ではありません。

## 起動 / 停止

```powershell
.\scripts\Start-DIP.ps1
.\scripts\Stop-DIP.ps1 -KeepData
```

`Start-DIP.ps1` は .NET、Python、PostgreSQL client tools を確認し、設定済みPostgreSQL databaseへの接続を検証します。その後、Python venvの作成、Build、Analysis / API / Worker / Webの起動、`.dip/runtime` へのPID・Log記録を行い、各Serviceがreadyになるまで待機します。

`Stop-DIP.ps1` が停止するのは、DIP自身が記録したPIDだけです。PostgreSQLのOS ServiceおよびDatabaseは意図的に停止しません。

## GUI Runtime Evidence（実行証拠）

```powershell
.\scripts\Verify-GuiRuntime.ps1 -RunPlaywright
```

Evidenceは次の場所へ出力されます。

```text
artifacts/gui-runtime-verification/<timestamp>/
```

## バックアップ / 復元

```powershell
.\scripts\Backup-Dip.ps1
.\scripts\Restore-Dip.ps1 -BackupDirectory <path> -ConfirmRestore
.\scripts\Verify-DisasterRecovery.ps1
```

3つのScriptはいずれも `pg_dump` / `pg_restore` / `psql` を直接使用します。Artifact storageは通常のDirectoryです。本番環境では `DIP_BACKUP_OFFSITE_DIRECTORY` の設定が必要になる場合があります。

## Migration検証

```powershell
.\scripts\Verify-DatabaseMigrations.ps1
```

検証時には一時PostgreSQL databaseを作成し、終了後に削除します。

## Performance Evidence（性能証拠）

```powershell
.\scripts\Run-PortfolioBenchmarks.ps1
```

一時的なnative PostgreSQL databaseを利用しつつ、Project list、Collector、burst、Metric-scale、soak / memoryの各Scenarioを実行します。
