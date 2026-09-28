# Purpose: Verifies EF Core database migrations using local PostgreSQL tools or a running PostgreSQL Docker container.
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
        ) | Where-Object { $_ -match '^\d{8,}_.+' }

        if ($migrations.Count -lt 1) {
            throw 'No EF migrations were discovered.'
        }

        $latest = $migrations[-1]

        # Some historical DIP migrations intentionally retain their original
        # 12-digit IDs for database compatibility. EF CLI cannot always resolve
        # those IDs as explicit named targets. Verify rollback with the special
        # target '0', which is supported independently of migration-ID parsing,
        # then re-apply the full chain to the latest migration.
        dotnet ef database update `
            --project src/Dip.Infrastructure `
            --startup-project src/Dip.Api

        if ($LASTEXITCODE -ne 0) {
            throw "Initial migration to latest revision failed: $latest"
        }

        dotnet ef database update 0 `
            --project src/Dip.Infrastructure `
            --startup-project src/Dip.Api

        if ($LASTEXITCODE -ne 0) {
            throw 'Rollback to migration zero failed.'
        }

        $afterRollback = (
            Invoke-Psql `
                -Database $db `
                -Sql 'SELECT count(*) FROM "__EFMigrationsHistory";' `
                -TuplesOnly |
            Out-String
        ).Trim()

        if ($afterRollback -notmatch '^\d+

        $applied = (
            Invoke-Psql `
                -Database $db `
                -Sql 'SELECT count(*) FROM "__EFMigrationsHistory";' `
                -TuplesOnly |
            Out-String
        ).Trim()

        if ($applied -notmatch '^\d+$' -or [int]$applied -lt 1) {
            throw 'Migration history verification failed.'
        }

        $runtime = Get-DipPostgresRuntimeLabel -Client $pg

        Write-Host (
            "Database migration verification PASS: $latest " +
            "($applied applied, client=$runtime)"
        ) -ForegroundColor Green
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
    catch {
    }

    try {
        Invoke-Psql `
            -Database 'postgres' `
            -Sql "DROP DATABASE IF EXISTS `"$db`";" |
            Out-Null
    }
    catch {
    }
}
 -or [int]$afterRollback -ne 0) {
            throw "Migration rollback verification failed. Remaining history rows=$afterRollback"
        }

        dotnet ef database update `
            --project src/Dip.Infrastructure `
            --startup-project src/Dip.Api

        if ($LASTEXITCODE -ne 0) {
            throw "Migration re-apply to latest revision failed: $latest"
        }

        dotnet ef database update `
            --project src/Dip.Infrastructure `
            --startup-project src/Dip.Api

        if ($LASTEXITCODE -ne 0) {
            throw 'Idempotent migration reapply failed.'
        }

        $applied = (
            Invoke-Psql `
                -Database $db `
                -Sql 'SELECT count(*) FROM "__EFMigrationsHistory";' `
                -TuplesOnly |
            Out-String
        ).Trim()

        if ($applied -notmatch '^\d+$' -or [int]$applied -lt 1) {
            throw 'Migration history verification failed.'
        }

        $runtime = Get-DipPostgresRuntimeLabel -Client $pg

        Write-Host (
            "Database migration verification PASS: $latest " +
            "($applied applied, client=$runtime)"
        ) -ForegroundColor Green
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
    catch {
    }

    try {
        Invoke-Psql `
            -Database 'postgres' `
            -Sql "DROP DATABASE IF EXISTS `"$db`";" |
            Out-Null
    }
    catch {
    }
}
