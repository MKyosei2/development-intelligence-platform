# Formal Runtime Verification

Formal evidence for a revision is generated only by a fail-closed run on the target Windows machine:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-PortfolioFormal.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe"
```

The runner requires a clean Git working tree, Windows, .NET, Docker, a real Unity Editor, full solution verification, Universal QA Bug Zoo, Project Control/P2 checks and portfolio benchmarks. It records the exact source commit. Old P0/Unity/benchmark evidence is never promoted to a new revision.

QA Governance alone can be verified with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-QaGovernance.ps1
```

The formal portfolio runner invokes this verification and will not create its PASS artifact when governance domain/persistence/diagnostic checks fail.

## Governance hardening formal checks

Run these from a clean Git checkout on Windows with .NET 8 and Docker available:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-DatabaseMigrations.ps1 -Configuration Release
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-DisasterRecovery.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Run-GovernanceLoadTest.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-QaGovernance.ps1 -Configuration Release
```

For the full exact-revision proof, including a real Unity Editor and the portfolio benchmark:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-PortfolioFormal.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe"
```

Operational Docker Compose backup/restore:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Backup-Dip.ps1 -ComposeProject dip
powershell -ExecutionPolicy Bypass -File .\scripts\Restore-Dip.ps1 -BackupDirectory <backup-folder> -ComposeProject dip -ConfirmRestore
```
