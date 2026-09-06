# BackupとRestore

`scripts/Backup-Dip.ps1`と`scripts/Restore-Dip.ps1`を使用する。Backupはnative PostgreSQL toolと通常filesystem artifactを使用し、SHA-256 validationとatomic publicationを行う。Restoreは`-ConfirmRestore`を必須とし、既定でsafety backupを作成する。破壊的操作の前にarchive/hash integrityを検証し、失敗時にはautomatic rollbackを試みる。

Disposable database + artifact restore drillには`scripts/Verify-DisasterRecovery.ps1`を使用する。
