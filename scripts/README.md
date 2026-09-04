# DIP operational scripts — Native-first

The supported runtime is host-native. Docker Desktop/Engine and an external queue daemon are not prerequisites.

## Start / stop

```powershell
.\scripts\Start-DIP.ps1
.\scripts\Stop-DIP.ps1 -KeepData
```

`Start-DIP.ps1` checks .NET, Python and PostgreSQL client tools; verifies the configured PostgreSQL database; creates the Python venv; builds; starts Analysis/API/Worker/Web; records PID/log files in `.dip/runtime`; and waits for readiness.

`Stop-DIP.ps1` only stops PIDs that DIP recorded. It intentionally leaves the PostgreSQL OS service and database running.

## GUI runtime evidence

```powershell
.\scripts\Verify-GuiRuntime.ps1 -RunPlaywright
```

Evidence is written to `artifacts/gui-runtime-verification/<timestamp>/`.

## Backup / restore

```powershell
.\scripts\Backup-Dip.ps1
.\scripts\Restore-Dip.ps1 -BackupDirectory <path> -ConfirmRestore
.\scripts\Verify-DisasterRecovery.ps1
```

All three use `pg_dump`/`pg_restore`/`psql` directly. Artifact storage is a normal directory. Production may require `DIP_BACKUP_OFFSITE_DIRECTORY`.

## Migration verification

```powershell
.\scripts\Verify-DatabaseMigrations.ps1
```

A temporary PostgreSQL database is created and destroyed for the check.

## Performance evidence

```powershell
.\scripts\Run-PortfolioBenchmarks.ps1
```

This preserves project-list, collector, burst, Metric-scale and soak/memory scenarios while using a temporary native PostgreSQL database.
