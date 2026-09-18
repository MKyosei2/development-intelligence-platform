# Purpose: Generates A rt if ac tT ra ck in g M ig ra ti on artifacts required by DIP.
﻿[CmdletBinding()]
param(
    [string]$MigrationName = 'AddDifficulty2LocalArtifacts'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repo
try {
    $phase1 = Get-ChildItem 'src\Dip.Infrastructure\Persistence\Migrations' `
        -Filter '*_AddDifficulty1Foundation.cs' -ErrorAction SilentlyContinue
    if (-not $phase1) {
        throw 'Phase 1 migration is missing. Apply/migrate Phase 1 before generating Phase 2.'
    }

    $existing = Get-ChildItem 'src\Dip.Infrastructure\Persistence\Migrations' `
        -Filter "*_$MigrationName.cs" -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "[PASS] Migration already exists: $($existing.Name)" -ForegroundColor Green
        exit 0
    }

    if (-not $env:ConnectionStrings__PostgreSql) {
        $env:ConnectionStrings__PostgreSql = 'Host=127.0.0.1;Port=5432;Database=dip;Username=dip;Password=change-me-local-only'
    }

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    dotnet ef migrations add $MigrationName `
        --project 'src\Dip.Infrastructure\Dip.Infrastructure.csproj' `
        --startup-project 'src\Dip.Api\Dip.Api.csproj' `
        --context DipDbContext `
        --output-dir 'Persistence\Migrations'

    if ($LASTEXITCODE -ne 0) { throw 'dotnet ef migrations add failed.' }

    Write-Host ''
    Write-Host '[PASS] Phase 2 EF migration and model snapshot were generated.' -ForegroundColor Green
    Write-Host '[INFO] Review generated migration before committing.' -ForegroundColor Yellow
    git status --short
}
finally { Pop-Location }
