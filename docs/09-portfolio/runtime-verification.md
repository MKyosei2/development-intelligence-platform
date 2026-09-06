# 正式なRuntime検証

RevisionのFormal Evidenceは、target Windows machine上でfail-closed runを実行した場合にのみ生成する。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-PortfolioFormal.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe"
```

Runnerはclean Git working tree、Windows、.NET、Docker、実Unity Editor、full solution verification、Universal QA Bug Zoo、Project Control/P2 check、portfolio benchmarkを要求し、exact source commitを記録する。古いP0/Unity/benchmark Evidenceを新revisionへ昇格させない。

QA Governance単体は次で検証できる。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-QaGovernance.ps1
```

Formal portfolio runnerはこのverificationを呼び出し、governance domain/persistence/diagnostic checkが失敗した場合はPASS artifactを作らない。

## Governance Hardening正式検証

Clean Git checkout、Windows、.NET 8、Dockerがある環境で実行:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-DatabaseMigrations.ps1 -Configuration Release
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-DisasterRecovery.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Run-GovernanceLoadTest.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-QaGovernance.ps1 -Configuration Release
```

実Unity Editorとportfolio benchmarkを含むfull exact-revision proof:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-PortfolioFormal.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe"
```

Operational Docker Compose backup/restore:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Backup-Dip.ps1 -ComposeProject dip
powershell -ExecutionPolicy Bypass -File .\scripts\Restore-Dip.ps1 -BackupDirectory <backup-folder> -ComposeProject dip -ConfirmRestore
```
