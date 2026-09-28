# Purpose: Verifies EF Core migrations: exact list, latest apply, rollback-to-zero, reapply, and idempotence.
[CmdletBinding()]
param(
    [string]$DatabaseUser = $(if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'dip' }),
    [string]$DatabaseHost = $(if ($env:POSTGRES_HOST) { $env:POSTGRES_HOST } else { '127.0.0.1' }),
    [int]$DatabasePort = $(if ($env:POSTGRES_PORT) { [int]$env:POSTGRES_PORT } else { 5432 }),
    [string]$DatabasePassword = $(if ($env:POSTGRES_PASSWORD) { $env:POSTGRES_PASSWORD } else { 'dip-test-password' })
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet is required.'
}

. (Join-Path $PSScriptRoot 'PostgreSqlRuntime.ps1')

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$db = 'dip_migration_' + [guid]::NewGuid().ToString('N').Substring(0, 12)
$oldConnection = $env:ConnectionStrings__PostgreSql

$pg = Resolve-DipPostgresClient `
    -HostName $DatabaseHost `
    -Port $DatabasePort `
    -User $DatabaseUser `
    -Password $DatabasePassword

function Invoke-Psql {
    param(
        [string]$Database,
        [string]$Sql,
        [switch]$TuplesOnly
    )

    Invoke-DipPsql `
        -Client $pg `
        -Database $Database `
        -Sql $Sql `
        -TuplesOnly:$TuplesOnly
}

function Get-HistoryCount {
    $exists = (
        Invoke-Psql `
            -Database $db `
            -Sql 'SELECT CASE WHEN to_regclass(''public."__EFMigrationsHistory"'') IS NULL THEN 0 ELSE 1 END;' `
            -TuplesOnly |
        Out-String
    ).Trim()

    if ($exists -eq '0') {
        return 0
    }

    if ($exists -ne '1') {
        throw "Unexpected migration-history existence result: '$exists'"
    }

    $count = (
        Invoke-Psql `
            -Database $db `
            -Sql 'SELECT count(*) FROM "__EFMigrationsHistory";' `
            -TuplesOnly |
        Out-String
    ).Trim()

    if ($count -notmatch '^\d+$') {
        throw "Invalid migration-history count: '$count'"
    }

    return [int]$count
}

function Get-HistoryIds {
    if ((Get-HistoryCount) -eq 0) {
        return @()
    }

    return @(
        Invoke-Psql `
            -Database $db `
            -Sql 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' `
            -TuplesOnly |
        ForEach-Object { $_.ToString().Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
}

try {
    if (-not (Test-DipPostgresReady -Client $pg)) {
        throw "PostgreSQL is not reachable at ${DatabaseHost}:${DatabasePort}."
    }

    Invoke-Psql `
        -Database 'postgres' `
        -Sql "CREATE DATABASE `"$db`" OWNER `"$DatabaseUser`";" |
    Out-Null

    $env:ConnectionStrings__PostgreSql =
        "Host=$DatabaseHost;Port=$DatabasePort;Database=$db;Username=$DatabaseUser;Password=$DatabasePassword"

    Push-Location $root
    try {
        dotnet tool restore
        if ($LASTEXITCODE -ne 0) {
            throw 'dotnet tool restore failed.'
        }

        $migrations = @(
            dotnet ef migrations list `
                --project src/Dip.Infrastructure `
                --startup-project src/Dip.Api `
                --no-connect 2>$null
        ) |
        Where-Object { $_ -match '^\d{8,}_.+' } |
        ForEach-Object { $_.ToString().Trim() }

        if ($migrations.Count -lt 1) {
            throw 'No EF migrations were discovered.'
        }

        $latest = $migrations[-1]

        dotnet ef database update `
            --project src/Dip.Infrastructure `
            --startup-project src/Dip.Api

        if ($LASTEXITCODE -ne 0) {
            throw "Initial migration to latest revision failed: $latest"
        }

        $initialIds = @(Get-HistoryIds)

        if ($initialIds.Count -ne $migrations.Count) {
            throw "Initial migration count mismatch. Expected=$($migrations.Count) Actual=$($initialIds.Count)"
        }

        for ($index = 0; $index -lt $migrations.Count; $index++) {
            if ($initialIds[$index] -ne $migrations[$index]) {
                throw "Initial migration id mismatch at index $index. Expected=$($migrations[$index]) Actual=$($initialIds[$index])"
            }
        }

        dotnet ef database update 0 `
            --project src/Dip.Infrastructure `
            --startup-project src/Dip.Api

        if ($LASTEXITCODE -ne 0) {
            throw 'Rollback to migration zero failed.'
        }

        $afterRollback = Get-HistoryCount
        if ($afterRollback -ne 0) {
            throw "Migration rollback verification failed. Remaining history rows=$afterRollback"
        }

        dotnet ef database update `
            --project src/Dip.Infrastructure `
            --startup-project src/Dip.Api

        if ($LASTEXITCODE -ne 0) {
            throw "Migration re-apply to latest revision failed: $latest"
        }

        $reappliedIds = @(Get-HistoryIds)
        if ($reappliedIds.Count -ne $migrations.Count) {
            throw "Reapplied migration count mismatch. Expected=$($migrations.Count) Actual=$($reappliedIds.Count)"
        }

        for ($index = 0; $index -lt $migrations.Count; $index++) {
            if ($reappliedIds[$index] -ne $migrations[$index]) {
                throw "Reapplied migration id mismatch at index $index. Expected=$($migrations[$index]) Actual=$($reappliedIds[$index])"
            }
        }

        dotnet ef database update `
            --project src/Dip.Infrastructure `
            --startup-project src/Dip.Api

        if ($LASTEXITCODE -ne 0) {
            throw 'Idempotent migration reapply failed.'
        }

        $afterIdempotent = @(Get-HistoryIds)
        if ($afterIdempotent.Count -ne $migrations.Count) {
            throw "Idempotent migration count changed. Expected=$($migrations.Count) Actual=$($afterIdempotent.Count)"
        }

        for ($index = 0; $index -lt $migrations.Count; $index++) {
            if ($afterIdempotent[$index] -ne $migrations[$index]) {
                throw "Idempotent migration id changed at index $index."
            }
        }

        $runtime = Get-DipPostgresRuntimeLabel -Client $pg
        $sha = (& git -C $root rev-parse HEAD | Out-String).Trim()
        $out = Join-Path $root 'artifacts\database-migration-verification'
        New-Item -ItemType Directory -Force -Path $out | Out-Null

        [ordered]@{
            schema = 'dip-database-migration-verification-v2'
            sourceCommit = $sha
            verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
            runtime = $runtime
            latestMigration = $latest
            expectedMigrationCount = $migrations.Count
            appliedMigrationCount = $afterIdempotent.Count
            migrationIdsExactMatch = $true
            rollbackToZero = 'PASS'
            reapplyLatest = 'PASS'
            idempotentReapply = 'PASS'
        } |
        ConvertTo-Json -Depth 6 |
        Set-Content (Join-Path $out 'database-migration-verification.json') -Encoding UTF8

        Write-Host "Database migration verification PASS: $latest ($($migrations.Count) exact migrations, client=$runtime)" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}
finally {
    $env:ConnectionStrings__PostgreSql = $oldConnection

    try {
        Invoke-Psql `
            -Database 'postgres' `
            -Sql "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$db';" |
        Out-Null
    }
    catch {}

    try {
        Invoke-Psql `
            -Database 'postgres' `
            -Sql "DROP DATABASE IF EXISTS `"$db`";" |
        Out-Null
    }
    catch {}
}
