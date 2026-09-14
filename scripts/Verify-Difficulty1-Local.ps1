[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repo
try {
    Write-Host 'DIP Difficulty-1 local verification' -ForegroundColor Cyan

    $branch = (& git branch --show-current).Trim()
    if ($branch -ne 'feat/difficulty1-foundation-20260913') {
        throw "Unexpected branch: $branch"
    }

    $migration = Get-ChildItem 'src\Dip.Infrastructure\Persistence\Migrations' `
        -Filter '*_AddDifficulty1Foundation.cs' `
        -ErrorAction SilentlyContinue
    if (-not $migration) {
        throw 'AddDifficulty1Foundation migration is missing. Run scripts\Generate-Difficulty1-Migration.ps1 first.'
    }

    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }

    dotnet test tests\Dip.Application.Tests\Dip.Application.Tests.csproj `
        -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Application tests failed.' }

    Write-Host ''
    Write-Host '[PASS] Difficulty-1 local compile/unit verification completed.' -ForegroundColor Green
    Write-Host '[INFO] No commit, push, or GitHub Actions rerun was performed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
