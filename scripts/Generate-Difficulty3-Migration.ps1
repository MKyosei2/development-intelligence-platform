[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repo
try {
    Write-Host 'DIP Phase 3 migration verification' -ForegroundColor Cyan

    $phase2 = Get-ChildItem 'src\Dip.Infrastructure\Persistence\Migrations' `
        -Filter '*_AddDifficulty2LocalArtifacts.cs' -ErrorAction SilentlyContinue
    if (-not $phase2) {
        Write-Host '[INFO] Phase 2 migration is missing; generating it first.' -ForegroundColor Yellow
        & (Join-Path $PSScriptRoot 'Generate-Difficulty2-Migration.ps1')
        if ($LASTEXITCODE -ne 0) { throw 'Phase 2 migration generation failed.' }
    }

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    dotnet ef migrations has-pending-model-changes `
        --project 'src\Dip.Infrastructure\Dip.Infrastructure.csproj' `
        --startup-project 'src\Dip.Api\Dip.Api.csproj' `
        --context DipDbContext

    if ($LASTEXITCODE -eq 0) {
        Write-Host '[PASS] Phase 3 does not require an additional EF migration.' -ForegroundColor Green
    }
    else {
        throw 'Phase 3 unexpectedly introduced pending EF model changes. Review before generating a migration.'
    }
}
finally { Pop-Location }
