# Purpose: Verifies game-development QA foundations, native tooling, capture evidence, and Bug Zoo behavior.
[CmdletBinding()]
param([switch]$SkipBugZoo)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root 'artifacts/game-qa-verification'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$passPath = Join-Path $out 'GAME_QA_LOCAL_VERIFICATION.txt'
Remove-Item -LiteralPath $passPath -Force -ErrorAction SilentlyContinue

Push-Location $root
try {
    dotnet restore DevelopmentIntelligencePlatform.sln
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed' }

    dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }

    foreach ($project in @(
        'tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj',
        'tests/Dip.Application.Tests/Dip.Application.Tests.csproj',
        'tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj')) {
        dotnet test $project -c Release --no-build
        if ($LASTEXITCODE -ne 0) { throw "test failed: $project" }
    }

    & (Join-Path $PSScriptRoot 'Build-NativeTools.ps1') -Configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Native tool verification failed' }

    if ($env:OS -eq 'Windows_NT' -and [Environment]::UserInteractive) {
        & (Join-Path $PSScriptRoot 'Test-CaptureAgent.ps1')
        if ($LASTEXITCODE -ne 0) { throw 'Capture-agent smoke test failed' }
    }

    if (-not $SkipBugZoo) {
        & (Join-Path $PSScriptRoot 'Invoke-QaBugZoo.ps1')
        if ($LASTEXITCODE -ne 0) { throw 'QA Bug Zoo failed' }
    }

    @(
        'GAME QA LOCAL VERIFICATION PASSED',
        "Timestamp: $(Get-Date -Format o)",
        'Note: Live E2E still requires API/Worker/PostgreSQL and should be run separately.'
    ) | Set-Content -Path $passPath -Encoding UTF8

    Write-Host 'Game QA local verification passed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
