[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repo
$artifact = Join-Path $repo 'artifacts/upgrade6-fix-verification'
New-Item -ItemType Directory -Force -Path $artifact | Out-Null
$pass = Join-Path $artifact 'UPGRADE6_FIX_VERIFICATION_PASS.txt'
$fail = Join-Path $artifact 'UPGRADE6_FIX_VERIFICATION_FAIL.txt'
Remove-Item $pass,$fail -Force -ErrorAction SilentlyContinue

try {
    $dotnet = Get-Command dotnet -ErrorAction Stop
    $version = & $dotnet.Source --version
    if ($LASTEXITCODE -ne 0 -or -not $version.StartsWith('8.')) {
        throw ".NET 8 SDK is required. Detected: $version"
    }

    & $dotnet.Source restore .\DevelopmentIntelligencePlatform.sln
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    & $dotnet.Source build .\DevelopmentIntelligencePlatform.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

    if (-not $SkipTests) {
        foreach ($project in @(
            '.\tests\Dip.Domain.Tests\Dip.Domain.Tests.csproj',
            '.\tests\Dip.Application.Tests\Dip.Application.Tests.csproj',
            '.\tests\Dip.Infrastructure.Tests\Dip.Infrastructure.Tests.csproj'
        )) {
            & $dotnet.Source test $project --configuration Release --no-build
            if ($LASTEXITCODE -ne 0) { throw "Unit tests failed: $project" }
        }
    }

    @(
        'UPGRADE6_FIX_VERIFICATION=PASS',
        "DOTNET_SDK=$version",
        "TESTS_SKIPPED=$([bool]$SkipTests)",
        "UTC=$([DateTimeOffset]::UtcNow.ToString('O'))"
    ) | Set-Content -LiteralPath $pass -Encoding UTF8

    Write-Host "PASS: $pass" -ForegroundColor Green
}
catch {
    @(
        'UPGRADE6_FIX_VERIFICATION=FAIL',
        "ERROR=$($_.Exception.Message)",
        "UTC=$([DateTimeOffset]::UtcNow.ToString('O'))"
    ) | Set-Content -LiteralPath $fail -Encoding UTF8
    throw
}
