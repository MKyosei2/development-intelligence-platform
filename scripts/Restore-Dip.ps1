# Purpose: Restores DIP data and operational artifacts from a validated backup.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BackupDirectory,
    [switch]$ConfirmRestore,
    [switch]$SkipSafetyBackup,
    [switch]$AllowCrossRevisionRestore,
    [switch]$DisableAutomaticRollback,
    [string]$ArtifactDirectory = 'artifacts\durable',
    [string]$Database = $(if($env:POSTGRES_DB){$env:POSTGRES_DB}else{'dip'}),
    [string]$DatabaseUser = $(if($env:POSTGRES_USER){$env:POSTGRES_USER}else{'dip'}),
    [string]$DatabaseHost = $(if($env:POSTGRES_HOST){$env:POSTGRES_HOST}else{'127.0.0.1'}),
    [int]$DatabasePort = $(if($env:POSTGRES_PORT){[int]$env:POSTGRES_PORT}else{5432}),
    [string]$DatabasePassword = $(if($env:POSTGRES_PASSWORD){$env:POSTGRES_PASSWORD}else{''})
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'PostgreSqlRuntime.ps1')

if (-not $ConfirmRestore) {
    throw 'Restore is destructive. Re-run with -ConfirmRestore.'
}

foreach ($name in @('pg_restore','psql','git')) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "$name is required."
    }
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$backup = (Resolve-Path $BackupDirectory).Path
$dump = Join-Path $backup 'database.dump'
$zip = Join-Path $backup 'artifacts.zip'
$manifestPath = Join-Path $backup 'backup-manifest.json'

function Resolve-DipPath {
    param([string]$Path,[string]$Root)
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $Root $Path))
}

function Invoke-LocalPsql {
    param(
        [Parameter(Mandatory=$true)][string]$TargetDatabase,
        [Parameter(Mandatory=$true)][string]$Sql,
        [switch]$TuplesOnly,
        [switch]$DiscardOutput
    )

    $arguments = @(
        '-h',$DatabaseHost,
        '-p',[string]$DatabasePort,
        '-U',$DatabaseUser,
        '-d',$TargetDatabase,
        '-v','ON_ERROR_STOP=1',
        '-w'
    )
    if ($TuplesOnly) { $arguments += @('-A','-t') }
    $arguments += @('-f','-')

    Invoke-DipNativeInputChecked -File 'psql' -Arguments $arguments -InputText $Sql -DiscardOutput:$DiscardOutput -FailureMessage "psql failed for database '$TargetDatabase'."
}

foreach ($file in @($dump,$zip,$manifestPath)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Backup component missing: $file"
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schema -ne 'dip-backup-v3') { throw "Native restore requires dip-backup-v3, found $($manifest.schema)." }
if ((Get-FileHash -LiteralPath $dump -Algorithm SHA256).Hash -ne $manifest.databaseSha256) { throw 'Database hash mismatch.' }
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $manifest.artifactsSha256) { throw 'Artifact hash mismatch.' }

$current = (Invoke-DipNativeChecked -File 'git' -Arguments @('-C',$root,'rev-parse','HEAD') | Out-String).Trim()
if (-not $AllowCrossRevisionRestore -and $manifest.sourceCommit -and $manifest.sourceCommit -ne $current) {
    throw "Backup commit $($manifest.sourceCommit) differs from HEAD $current. Use -AllowCrossRevisionRestore only after compatibility review."
}

$oldPg = $env:PGPASSWORD
$env:PGPASSWORD = $DatabasePassword
$safety = $null
$errorRecord = $null
$success = $false

try {
    Invoke-DipNativeChecked -File 'pg_restore' -Arguments @('--list',$dump) -DiscardOutput -FailureMessage 'Database archive validation failed.'

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $testDir = Join-Path ([IO.Path]::GetTempPath()) ('dip-restore-check-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $testDir | Out-Null
    try {
        [IO.Compression.ZipFile]::ExtractToDirectory($zip,$testDir)
    }
    finally {
        Remove-Item -LiteralPath $testDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    if (-not $SkipSafetyBackup) {
        $backupArgs = @{
            OutputDirectory = 'artifacts\pre-restore-backups'
            ArtifactDirectory = $ArtifactDirectory
            Database = $Database
            DatabaseUser = $DatabaseUser
            DatabaseHost = $DatabaseHost
            DatabasePort = $DatabasePort
            DatabasePassword = $DatabasePassword
        }
        $safety = @(& (Join-Path $PSScriptRoot 'Backup-Dip.ps1') @backupArgs) | Select-Object -Last 1
    }

    & (Join-Path $PSScriptRoot 'Stop-DIP.ps1') -KeepData

    $safeDb = $Database.Replace('"','""')
    $safeUser = $DatabaseUser.Replace('"','""')
    $safeDbLiteral = $Database.Replace("'","''")

    Invoke-LocalPsql -TargetDatabase 'postgres' -Sql "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$safeDbLiteral' AND pid<>pg_backend_pid();" -DiscardOutput
    Invoke-LocalPsql -TargetDatabase 'postgres' -Sql "DROP DATABASE IF EXISTS `"$safeDb`";" -DiscardOutput
    Invoke-LocalPsql -TargetDatabase 'postgres' -Sql "CREATE DATABASE `"$safeDb`" OWNER `"$safeUser`";" -DiscardOutput

    $restoreArgs = @(
        '-h',$DatabaseHost,
        '-p',[string]$DatabasePort,
        '-U',$DatabaseUser,
        '-d',$Database,
        '--clean',
        '--if-exists',
        '--no-owner',
        '--no-privileges',
        $dump
    )
    Invoke-DipNativeChecked -File 'pg_restore' -Arguments $restoreArgs -DiscardOutput -FailureMessage 'Database restore failed.'

    $artifactRoot = Resolve-DipPath -Path $ArtifactDirectory -Root $root
    $stage = "$artifactRoot.restore-$([guid]::NewGuid().ToString('N'))"
    New-Item -Force -ItemType Directory -Path $stage | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($zip,$stage)

    if (Test-Path -LiteralPath $artifactRoot) {
        $old = "$artifactRoot.old-$([guid]::NewGuid().ToString('N'))"
        Move-Item -LiteralPath $artifactRoot -Destination $old
        Move-Item -LiteralPath $stage -Destination $artifactRoot
        Remove-Item -LiteralPath $old -Recurse -Force
    }
    else {
        Move-Item -LiteralPath $stage -Destination $artifactRoot
    }

    $count = (Invoke-LocalPsql -TargetDatabase $Database -Sql 'SELECT count(*) FROM "__EFMigrationsHistory";' -TuplesOnly | Out-String).Trim()
    if ($count -notmatch '^\d+$' -or [int]$count -lt 1) { throw 'EF migration history validation failed.' }

    & (Join-Path $PSScriptRoot 'Start-DIP.ps1') -NoBuild -NoBrowser
    $success = $true
    Write-Host "Restore completed: $backup" -ForegroundColor Green
}
catch {
    $errorRecord = $_
}
finally {
    $env:PGPASSWORD = $oldPg
}

if (-not $success) {
    if ($safety -and -not $DisableAutomaticRollback) {
        Write-Warning "Restore failed; rolling back from $safety"
        $rollbackArgs = @{
            BackupDirectory = $safety
            ConfirmRestore = $true
            SkipSafetyBackup = $true
            AllowCrossRevisionRestore = $true
            DisableAutomaticRollback = $true
            ArtifactDirectory = $ArtifactDirectory
            Database = $Database
            DatabaseUser = $DatabaseUser
            DatabaseHost = $DatabaseHost
            DatabasePort = $DatabasePort
            DatabasePassword = $DatabasePassword
        }
        & $PSCommandPath @rollbackArgs
    }

    if ($errorRecord) { throw $errorRecord }
    throw 'Restore failed.'
}
