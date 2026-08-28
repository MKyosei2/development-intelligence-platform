[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupDirectory,
    [string]$ComposeProject = 'dip',
    [string]$Database = 'dip',
    [string]$DatabaseUser = 'dip',
    [switch]$ConfirmRestore,
    [switch]$SkipSafetyBackup,
    [switch]$AllowCrossRevisionRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-SafeIdentifier([string]$Value, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') {
        throw "$Name must start with a letter or digit and contain only letters, digits, underscore, or hyphen."
    }
}

function Assert-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name is required."
    }
}

if (-not $ConfirmRestore) {
    throw 'Restore is destructive. Re-run with -ConfirmRestore after verifying the target compose project.'
}

Assert-SafeIdentifier $ComposeProject 'ComposeProject'
Assert-SafeIdentifier $Database 'Database'
Assert-SafeIdentifier $DatabaseUser 'DatabaseUser'
Assert-Command 'docker'
Assert-Command 'git'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$backup = (Resolve-Path $BackupDirectory).Path
$dbDump = Join-Path $backup 'database.dump'
$artifactArchive = Join-Path $backup 'artifacts.tar.gz'
$manifestPath = Join-Path $backup 'backup-manifest.json'

foreach ($file in @($dbDump, $artifactArchive, $manifestPath)) {
    if (-not (Test-Path $file -PathType Leaf)) {
        throw "Backup component missing: $file"
    }
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schema -notin @('dip-backup-v1', 'dip-backup-v2')) {
    throw "Unsupported backup manifest schema: $($manifest.schema)"
}

$dbHash = (Get-FileHash $dbDump -Algorithm SHA256).Hash
$artifactHash = (Get-FileHash $artifactArchive -Algorithm SHA256).Hash
if ($dbHash -ne $manifest.databaseSha256) {
    throw 'Database dump hash does not match backup manifest.'
}
if ($artifactHash -ne $manifest.artifactsSha256) {
    throw 'Artifact archive hash does not match backup manifest.'
}

if ($manifest.schema -eq 'dip-backup-v2') {
    if ([int64]$manifest.databaseBytes -ne (Get-Item $dbDump).Length) {
        throw 'Database dump size does not match backup manifest.'
    }
    if ([int64]$manifest.artifactsBytes -ne (Get-Item $artifactArchive).Length) {
        throw 'Artifact archive size does not match backup manifest.'
    }
}

$currentSha = (& git -C $root rev-parse HEAD | Out-String).Trim()
if (-not $AllowCrossRevisionRestore
    -and $manifest.sourceCommit
    -and $manifest.sourceCommit -ne $currentSha) {
    throw "Backup was created from commit $($manifest.sourceCommit), but current HEAD is $currentSha. Re-run with -AllowCrossRevisionRestore only after validating migration compatibility."
}

$restoreId = [Guid]::NewGuid().ToString('N')
$pg = $null
$api = $null
$artifactVolume = $null
$validationContainer = $null
$restoreContainer = $null
$servicesStopped = $false
$restoreSucceeded = $false

Push-Location $root
try {
    $pg = (& docker compose -p $ComposeProject ps -q postgres | Out-String).Trim()
    if (-not $pg) {
        throw "Postgres service for compose project '$ComposeProject' is not running."
    }

    $api = (& docker compose -p $ComposeProject ps -q api | Out-String).Trim()
    if (-not $api) {
        throw "API service for compose project '$ComposeProject' is not running."
    }

    $artifactVolume = (& docker inspect $api --format '{{range .Mounts}}{{if eq .Destination "/data/dip-artifacts"}}{{.Name}}{{end}}{{end}}' | Out-String).Trim()
    if (-not $artifactVolume) {
        throw 'Could not resolve the Local artifact Docker volume.'
    }

    # Preflight the database archive with the target PostgreSQL tooling before any
    # service is stopped or any target data is destroyed.
    $containerDump = "/tmp/dip-restore-$restoreId.dump"
    & docker cp $dbDump "${pg}:$containerDump"
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not copy database dump into PostgreSQL container for validation.'
    }
    & docker exec $pg pg_restore --list $containerDump *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Database dump failed pg_restore validation.'
    }

    # Preflight the artifact archive independently from the production volume.
    $validationContainer = "dip-restore-validate-$restoreId"
    & docker create --name $validationContainer alpine:3.22 sh -c 'tar -tzf /tmp/artifacts.tar.gz >/dev/null' *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not create artifact validation container.'
    }
    & docker cp $artifactArchive "${validationContainer}:/tmp/artifacts.tar.gz"
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not copy artifact archive into validation container.'
    }
    & docker start -a $validationContainer *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Artifact archive failed tar validation.'
    }
    & docker rm -f $validationContainer *> $null
    $validationContainer = $null

    if (-not $SkipSafetyBackup) {
        $backupScript = Join-Path $PSScriptRoot 'Backup-Dip.ps1'
        if (-not (Test-Path $backupScript -PathType Leaf)) {
            throw "Safety backup script is missing: $backupScript"
        }
        Write-Host 'Creating pre-restore safety backup...' -ForegroundColor Yellow
        & $backupScript `
            -ComposeProject $ComposeProject `
            -OutputDirectory 'artifacts\pre-restore-backups' `
            -Database $Database `
            -DatabaseUser $DatabaseUser
        Write-Host 'Pre-restore safety backup completed.' -ForegroundColor Green
    }

    & docker compose -p $ComposeProject stop api worker web *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not stop application services before restore.'
    }
    $servicesStopped = $true

    & docker exec $pg psql -U $DatabaseUser -d postgres -v ON_ERROR_STOP=1 -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$Database' AND pid<>pg_backend_pid();" *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not terminate active database sessions.'
    }

    & docker exec $pg psql -U $DatabaseUser -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS \"$Database\";" *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not drop target database.'
    }

    & docker exec $pg psql -U $DatabaseUser -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE \"$Database\" OWNER \"$DatabaseUser\";" *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not create target database.'
    }

    & docker exec $pg pg_restore -U $DatabaseUser -d $Database --clean --if-exists --no-owner --no-privileges $containerDump
    if ($LASTEXITCODE -ne 0) {
        throw 'Database restore failed.'
    }

    $restoreContainer = "dip-restore-$restoreId"
    & docker create --name $restoreContainer -v "${artifactVolume}:/target" alpine:3.22 sh -c 'set -eu; rm -rf /target/* /target/.[!.]* /target/..?* 2>/dev/null || true; tar -xzf /tmp/artifacts.tar.gz -C /target' *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not create artifact restore container.'
    }
    & docker cp $artifactArchive "${restoreContainer}:/tmp/artifacts.tar.gz"
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not copy artifact archive into restore container.'
    }
    & docker start -a $restoreContainer
    if ($LASTEXITCODE -ne 0) {
        throw 'Artifact restore failed.'
    }
    & docker rm -f $restoreContainer *> $null
    $restoreContainer = $null

    # Database post-restore sanity. The EF history table is expected for a restorable
    # DIP database and protects against accepting a structurally incomplete dump.
    $sanity = (& docker exec $pg psql -U $DatabaseUser -d $Database -At -v ON_ERROR_STOP=1 -c 'SELECT 1;' | Out-String).Trim()
    if ($sanity -ne '1') {
        throw 'Post-restore database sanity query failed.'
    }

    $migrationCount = (& docker exec $pg psql -U $DatabaseUser -d $Database -At -v ON_ERROR_STOP=1 -c 'SELECT count(*) FROM "__EFMigrationsHistory";' | Out-String).Trim()
    if ($migrationCount -notmatch '^\d+$') {
        throw 'Could not validate EF migration history after restore.'
    }
    if ([int]$migrationCount -lt 1) {
        throw 'Restored database contains no EF migration history.'
    }

    & docker compose -p $ComposeProject start api worker web *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Database/artifacts were restored but application services failed to start.'
    }
    $servicesStopped = $false

    # At minimum, require containers to remain running after startup. The API's
    # /api/v1/system/ready endpoint can then be used by the deployment layer for
    # dependency-aware traffic admission.
    Start-Sleep -Seconds 2
    foreach ($service in @('api', 'worker', 'web')) {
        $container = (& docker compose -p $ComposeProject ps -q $service | Out-String).Trim()
        if (-not $container) {
            throw "Service '$service' did not produce a running container after restore."
        }
        $running = (& docker inspect $container --format '{{.State.Running}}' | Out-String).Trim()
        if ($running -ne 'true') {
            throw "Service '$service' is not running after restore."
        }
    }

    $restoreSucceeded = $true
    Write-Host "Restore completed and post-restore checks passed: $backup" -ForegroundColor Green
}
finally {
    if ($validationContainer) {
        try { & docker rm -f $validationContainer *> $null } catch {}
    }
    if ($restoreContainer) {
        try { & docker rm -f $restoreContainer *> $null } catch {}
    }
    if ($pg) {
        try { & docker exec $pg rm -f "/tmp/dip-restore-$restoreId.dump" *> $null } catch {}
    }
    if ($servicesStopped) {
        try {
            & docker compose -p $ComposeProject start api worker web *> $null
            Write-Warning 'Restore did not complete; application services were restarted. Use the pre-restore safety backup if target data needs to be rolled back.'
        }
        catch {
            Write-Warning "Restore failed and application services could not be restarted automatically: $($_.Exception.Message)"
        }
    }
    Pop-Location
}

if (-not $restoreSucceeded) {
    throw 'Restore did not complete successfully.'
}
