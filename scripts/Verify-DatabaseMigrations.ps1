[CmdletBinding()]
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
foreach ($command in @('dotnet', 'docker', 'git')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command is required."
    }
}

function Get-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()
    return $port
}

function Invoke-EfUpdate([string]$Target = '') {
    $args = @(
        'ef', 'database', 'update'
    )
    if (-not [string]::IsNullOrWhiteSpace($Target)) {
        $args += $Target
    }
    $args += @(
        '--project', 'src/Dip.Infrastructure',
        '--startup-project', 'src/Dip.Api',
        '--configuration', $Configuration,
        '--no-build'
    )
    & dotnet @args
    if ($LASTEXITCODE -ne 0) {
        throw "EF database update failed for target '$Target'."
    }
}

function Get-AppliedMigrations([string]$ContainerId, [string]$Database) {
    $raw = (& docker exec $ContainerId psql -U dip -d $Database -At -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not read EF migration history.'
    }
    return @($raw -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$compose = "dip-migration-$stamp"
$db = 'dipmigration'
$pgPort = Get-FreePort
$old = @{}
$variables = @(
    'POSTGRES_PORT',
    'POSTGRES_DB',
    'POSTGRES_USER',
    'POSTGRES_PASSWORD',
    'ConnectionStrings__PostgreSql',
    'ASPNETCORE_ENVIRONMENT',
    'Authentication__RequireAuthenticatedUser'
)
foreach ($name in $variables) {
    $old[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

$out = Join-Path $root 'artifacts\migration-verification'
New-Item -Force -ItemType Directory $out | Out-Null
Remove-Item (Join-Path $out 'MIGRATION_VERIFICATION_PASS.txt') -Force -ErrorAction SilentlyContinue

$latestMigration = $null
$previousMigration = $null
$migrationCount = 0
$rollbackVerified = $false

try {
    $env:POSTGRES_PORT = "$pgPort"
    $env:POSTGRES_DB = $db
    $env:POSTGRES_USER = 'dip'
    $env:POSTGRES_PASSWORD = 'dip-migration-password'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Authentication__RequireAuthenticatedUser = 'false'
    $env:ConnectionStrings__PostgreSql = "Host=127.0.0.1;Port=$pgPort;Database=$db;Username=dip;Password=dip-migration-password"

    Push-Location $root
    try {
        & docker compose -p $compose up -d postgres
        if ($LASTEXITCODE -ne 0) {
            throw 'PostgreSQL start failed.'
        }

        $containerId = (& docker compose -p $compose ps -q postgres | Out-String).Trim()
        if (-not $containerId) {
            throw 'Postgres container missing.'
        }

        $ready = $false
        for ($i = 0; $i -lt 60; $i++) {
            & docker exec $containerId pg_isready -U dip -d $db *> $null
            if ($LASTEXITCODE -eq 0) {
                $ready = $true
                break
            }
            Start-Sleep -Milliseconds 500
        }
        if (-not $ready) {
            throw 'PostgreSQL did not become ready.'
        }

        & dotnet tool restore
        if ($LASTEXITCODE -ne 0) {
            throw 'dotnet tool restore failed.'
        }

        & dotnet build DevelopmentIntelligencePlatform.sln -c $Configuration --nologo
        if ($LASTEXITCODE -ne 0) {
            throw 'Build failed.'
        }

        # Product-grade migration verification must reject model drift before touching
        # a production database. EF8 returns non-zero when model changes are not covered
        # by a migration.
        & dotnet ef migrations has-pending-model-changes `
            --project src/Dip.Infrastructure `
            --startup-project src/Dip.Api `
            --configuration $Configuration `
            --no-build
        if ($LASTEXITCODE -ne 0) {
            throw 'Pending EF model changes detected. Create a migration before release.'
        }

        # 1) Fresh install -> latest.
        Invoke-EfUpdate
        $freshMigrations = Get-AppliedMigrations $containerId $db
        $migrationCount = $freshMigrations.Count
        if ($migrationCount -lt 1) {
            throw 'Fresh upgrade applied no migrations.'
        }
        $latestMigration = $freshMigrations[-1]

        # 2) Latest -> previous -> latest. Do not hard-code a migration ID: the
        # verification remains valid when new migrations are appended.
        if ($migrationCount -ge 2) {
            $previousMigration = $freshMigrations[-2]
            Invoke-EfUpdate $previousMigration

            $afterRollback = Get-AppliedMigrations $containerId $db
            if ($afterRollback.Count -ne ($migrationCount - 1)) {
                throw "Rollback migration count mismatch: expected $($migrationCount - 1), got $($afterRollback.Count)."
            }
            if ($afterRollback[-1] -ne $previousMigration) {
                throw "Rollback stopped at '$($afterRollback[-1])' instead of '$previousMigration'."
            }

            Invoke-EfUpdate
            $afterReUpgrade = Get-AppliedMigrations $containerId $db
            if ($afterReUpgrade.Count -ne $migrationCount) {
                throw "Re-upgrade migration count mismatch: expected $migrationCount, got $($afterReUpgrade.Count)."
            }
            if ($afterReUpgrade[-1] -ne $latestMigration) {
                throw "Re-upgrade ended at '$($afterReUpgrade[-1])' instead of '$latestMigration'."
            }
            $rollbackVerified = $true
        }

        # Product hardening invariants introduced by QA governance/authorization.
        $tables = (& docker exec $containerId psql -U dip -d $db -Atc "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('project_memberships','qa_governance_processed_runs','qa_release_gate_overrides','qa_backup_verifications');" | Out-String).Trim()
        if ($tables -ne '4') {
            throw "Hardening migration tables missing ($tables/4)."
        }

        $activeConstraint = (& docker exec $containerId psql -U dip -d $db -Atc "SELECT count(*) FROM pg_constraint WHERE conname='uq_qa_policies_single_active';" | Out-String).Trim()
        if ($activeConstraint -ne '1') {
            throw 'Single-active-policy database constraint is missing.'
        }

        # Sanity-check referential integrity after rollback/re-upgrade.
        $invalidForeignKeys = (& docker exec $containerId psql -U dip -d $db -Atc "SELECT count(*) FROM pg_constraint WHERE contype='f' AND NOT convalidated;" | Out-String).Trim()
        if ($invalidForeignKeys -ne '0') {
            throw "Database contains $invalidForeignKeys unvalidated foreign-key constraints."
        }
    }
    finally {
        Pop-Location
    }

    $sha = (& git -C $root rev-parse HEAD | Out-String).Trim()
    $proof = [ordered]@{
        schema = 'dip-migration-verification-v3'
        sourceCommit = $sha
        verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        postgresPort = $pgPort
        pendingModelChanges = 'NONE'
        migrationCount = $migrationCount
        latestMigration = $latestMigration
        previousMigration = $previousMigration
        freshUpgrade = 'PASS'
        rollback = if ($rollbackVerified) { 'PASS' } else { 'NOT_APPLICABLE' }
        reUpgrade = if ($rollbackVerified) { 'PASS' } else { 'NOT_APPLICABLE' }
        hardeningTables = 'PASS'
        singleActivePolicyConstraint = 'PASS'
        foreignKeyValidation = 'PASS'
    }
    $proof | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $out 'migration-verification.json') -Encoding UTF8

    @(
        'MIGRATION VERIFICATION PASS',
        "Commit=$sha",
        "MigrationCount=$migrationCount",
        "LatestMigration=$latestMigration",
        'PendingModelChanges=NONE',
        'FreshUpgrade=PASS',
        "Rollback=$($proof.rollback)",
        "ReUpgrade=$($proof.reUpgrade)",
        'SingleActivePolicyConstraint=PASS',
        'ForeignKeyValidation=PASS'
    ) | Set-Content (Join-Path $out 'MIGRATION_VERIFICATION_PASS.txt') -Encoding UTF8
}
finally {
    Push-Location $root
    try {
        & docker compose -p $compose down -v --remove-orphans *> $null
    }
    catch {}
    finally {
        Pop-Location
    }

    foreach ($name in $variables) {
        [Environment]::SetEnvironmentVariable($name, $old[$name], 'Process')
    }
}
