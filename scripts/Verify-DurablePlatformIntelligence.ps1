[CmdletBinding()]
param(
    [switch]$RunIntegration,
    [switch]$RunEndToEnd
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repo

$artifactDir = Join-Path $repo 'artifacts/durable-platform-intelligence-verification'
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
$pass = Join-Path $artifactDir 'DURABLE_PLATFORM_INTELLIGENCE_VERIFICATION_PASS.txt'
$fail = Join-Path $artifactDir 'DURABLE_PLATFORM_INTELLIGENCE_VERIFICATION_FAIL.txt'
Remove-Item $pass, $fail -Force -ErrorAction SilentlyContinue

function Invoke-DotNetStep {
    param([string]$Name, [string[]]$Arguments)
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $script:dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

try {
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $script:dotnet = $dotnetCommand.Source
    $version = & $script:dotnet --version
    if ($LASTEXITCODE -ne 0 -or -not $version.StartsWith('8.')) {
        throw ".NET 8 SDK is required. Detected: $version"
    }

    Invoke-DotNetStep 'Restore solution' @('restore', '.\DevelopmentIntelligencePlatform.sln')
    Invoke-DotNetStep 'Build solution (Release)' @('build', '.\DevelopmentIntelligencePlatform.sln', '--configuration', 'Release', '--no-restore')

    foreach ($project in @(
        '.\tests\Dip.Domain.Tests\Dip.Domain.Tests.csproj',
        '.\tests\Dip.Application.Tests\Dip.Application.Tests.csproj',
        '.\tests\Dip.Infrastructure.Tests\Dip.Infrastructure.Tests.csproj'
    )) {
        Invoke-DotNetStep "Test $project" @('test', $project, '--configuration', 'Release', '--no-build')
    }

    if ($RunIntegration) {
        Invoke-DotNetStep 'Integration tests' @('test', '.\tests\Dip.IntegrationTests\Dip.IntegrationTests.csproj', '--configuration', 'Release', '--no-build')
    }

    if ($RunEndToEnd) {
        Invoke-DotNetStep 'End-to-end tests' @('test', '.\tests\Dip.EndToEndTests\Dip.EndToEndTests.csproj', '--configuration', 'Release', '--no-build')
    }

    @(
        'DURABLE_PLATFORM_INTELLIGENCE_VERIFICATION=PASS',
        "DOTNET_SDK=$version",
        "INTEGRATION_EXECUTED=$([bool]$RunIntegration)",
        "E2E_EXECUTED=$([bool]$RunEndToEnd)",
        'MIGRATION_METADATA_TEST=tests/Dip.Infrastructure.Tests/Persistence/MigrationMetadataTests.cs',
        "UTC=$([DateTimeOffset]::UtcNow.ToString('O'))"
    ) | Set-Content -LiteralPath $pass -Encoding UTF8

    Write-Host "PASS: $pass" -ForegroundColor Green
}
catch {
    @(
        'DURABLE_PLATFORM_INTELLIGENCE_VERIFICATION=FAIL',
        "ERROR=$($_.Exception.Message)",
        "INTEGRATION_REQUESTED=$([bool]$RunIntegration)",
        "E2E_REQUESTED=$([bool]$RunEndToEnd)",
        "UTC=$([DateTimeOffset]::UtcNow.ToString('O'))"
    ) | Set-Content -LiteralPath $fail -Encoding UTF8
    throw
}
