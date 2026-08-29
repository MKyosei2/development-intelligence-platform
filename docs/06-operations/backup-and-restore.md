# Backup and restore

Use `scripts/Backup-Dip.ps1` and `scripts/Restore-Dip.ps1`. Backups use native PostgreSQL tools and ordinary filesystem artifacts, with SHA-256 validation and atomic publication. Restore requires `-ConfirmRestore`, creates a safety backup by default, validates archive/hash integrity before destructive operations and attempts automatic rollback on failure.

Use `scripts/Verify-DisasterRecovery.ps1` for a disposable database+artifact restore drill.
