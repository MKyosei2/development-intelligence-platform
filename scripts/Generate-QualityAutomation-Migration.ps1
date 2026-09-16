# Purpose: Automates the G en er at e-Q ua li ty Au to ma ti on-M ig ra ti on repository workflow.
[CmdletBinding()]
param(
    [string]$MigrationName = 'AddDifficulty4QualityIntelligence'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repo
try {
    Write-Host 'DIP Phase 4 migration generation' -ForegroundColor Cyan

    $phase1 = Get-ChildItem 'src\Dip.Infrastructure\Persistence\Migrations' -Filter '*_AddDifficulty1Foundation.cs' -ErrorAction SilentlyContinue
    if (-not $phase1) { throw 'Phase 1 migration is missing.' }

    $phase2 = Get-ChildItem 'src\Dip.Infrastructure\Persistence\Migrations' -Filter '*_AddDifficulty2LocalArtifacts.cs' -ErrorAction SilentlyContinue
    if (-not $phase2) {
        Write-Host '[INFO] Generating Phase 2 migration first.' -ForegroundColor Yellow
        & (Join-Path $PSScriptRoot 'Generate-ArtifactTracking-Migration.ps1')
    }

    $existing = Get-ChildItem 'src\Dip.Infrastructure\Persistence\Migrations' -Filter "*_$MigrationName.cs" -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "[PASS] Migration already exists: $($existing.Name)" -ForegroundColor Green
    }
    else {
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
        Write-Host '[PASS] Phase 4 migration/model snapshot generated.' -ForegroundColor Green
    }

    dotnet ef migrations has-pending-model-changes `
        --project 'src\Dip.Infrastructure\Dip.Infrastructure.csproj' `
        --startup-project 'src\Dip.Api\Dip.Api.csproj' `
        --context DipDbContext
    if ($LASTEXITCODE -ne 0) { throw 'Phase 4 still has pending EF model changes after migration generation.' }
    Write-Host '[PASS] No pending model changes remain.' -ForegroundColor Green
    git status --short
}
finally { Pop-Location }
