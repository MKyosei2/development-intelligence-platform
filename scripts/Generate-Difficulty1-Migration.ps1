[CmdletBinding()]
param(
    [string]$MigrationName = 'AddDifficulty1Foundation'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repo
try {
    $branch = (& git branch --show-current).Trim()
    if ($branch -ne 'feat/difficulty1-foundation-20260913') {
        throw "Run this only on feat/difficulty1-foundation-20260913. Current: $branch"
    }

    $existing = Get-ChildItem 'src\Dip.Infrastructure\Persistence\Migrations' -Filter "*_$MigrationName.cs" -ErrorAction SilentlyContinue
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
    Write-Host '[PASS] EF migration and DipDbContextModelSnapshot were generated.' -ForegroundColor Green
    Write-Host '[INFO] Review the migration before committing.' -ForegroundColor Yellow
    git status --short
}
finally {
    Pop-Location
}
