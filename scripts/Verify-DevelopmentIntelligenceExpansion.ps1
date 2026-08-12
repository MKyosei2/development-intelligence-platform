[CmdletBinding()]
param(
    [switch]$RunIntegration,
    [switch]$RunEndToEnd,
    [switch]$RunDockerConfig
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repo

$artifactDir = Join-Path $repo 'artifacts/development-intelligence-expansion-verification'
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
$pass = Join-Path $artifactDir 'DEVELOPMENT_INTELLIGENCE_EXPANSION_VERIFICATION_PASS.txt'
$fail = Join-Path $artifactDir 'DEVELOPMENT_INTELLIGENCE_EXPANSION_VERIFICATION_FAIL.txt'
Remove-Item $pass, $fail -Force -ErrorAction SilentlyContinue

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE." }
}

function Invoke-DotNetStep {
    param([string]$Name, [string[]]$Arguments)
    Invoke-Step $Name { & $script:dotnet @Arguments }
}

try {
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $script:dotnet = $dotnetCommand.Source
    $version = & $script:dotnet --version
    if ($LASTEXITCODE -ne 0 -or -not $version.StartsWith('8.')) {
        throw ".NET 8 SDK is required. Detected: $version"
    }

    Invoke-DotNetStep 'Restore solution' @('restore', '.\DevelopmentIntelligencePlatform.sln')
    Invoke-DotNetStep 'Build full solution (Release)' @('build', '.\DevelopmentIntelligencePlatform.sln', '--configuration', 'Release', '--no-restore')

    foreach ($project in @(
        '.\tests\Dip.Domain.Tests\Dip.Domain.Tests.csproj',
        '.\tests\Dip.Application.Tests\Dip.Application.Tests.csproj',
        '.\tests\Dip.Infrastructure.Tests\Dip.Infrastructure.Tests.csproj'
    )) {
        Invoke-DotNetStep "Test $project" @('test', $project, '--configuration', 'Release', '--no-build')
    }

    Invoke-DotNetStep 'List EF migrations' @(
        'ef', 'migrations', 'list',
        '--project', '.\src\Dip.Infrastructure\Dip.Infrastructure.csproj',
        '--startup-project', '.\src\Dip.Api\Dip.Api.csproj',
        '--context', 'DipDbContext',
        '--no-build'
    )

    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($null -ne $python) {
        Invoke-Step 'Compile Unreal collector' { & $python.Source -m compileall -q '.\collectors\unreal-collector' }
        Invoke-Step 'Test Unreal collector' { & $python.Source -m unittest discover -s '.\collectors\unreal-collector\tests' -v }
    }

    if ($RunDockerConfig) {
        $docker = Get-Command docker -ErrorAction Stop
        Invoke-Step 'Validate Docker Compose' { & $docker.Source compose config --quiet }
    }

    if ($RunIntegration) {
        Invoke-DotNetStep 'Integration tests' @('test', '.\tests\Dip.IntegrationTests\Dip.IntegrationTests.csproj', '--configuration', 'Release', '--no-build')
    }

    if ($RunEndToEnd) {
        Invoke-DotNetStep 'End-to-end tests' @('test', '.\tests\Dip.EndToEndTests\Dip.EndToEndTests.csproj', '--configuration', 'Release', '--no-build')
    }

    @(
        'DEVELOPMENT_INTELLIGENCE_EXPANSION_VERIFICATION=PASS',
        "DOTNET_SDK=$version",
        "INTEGRATION_EXECUTED=$([bool]$RunIntegration)",
        "E2E_EXECUTED=$([bool]$RunEndToEnd)",
        "DOCKER_CONFIG_EXECUTED=$([bool]$RunDockerConfig)",
        'MIGRATIONS_EXPECTED=202608120001_InitialBaseline;202608120002_AddDurablePlatformIntelligence;202608120003_AddDevelopmentIntelligenceExpansion;202608120004_AddProjectControlGraph',
        'UNIVERSAL_QA_DEFERRED=true',
        "UTC=$([DateTimeOffset]::UtcNow.ToString('O'))"
    ) | Set-Content -LiteralPath $pass -Encoding UTF8

    Write-Host "PASS: $pass" -ForegroundColor Green
}
catch {
    @(
        'DEVELOPMENT_INTELLIGENCE_EXPANSION_VERIFICATION=FAIL',
        "ERROR=$($_.Exception.Message)",
        "INTEGRATION_REQUESTED=$([bool]$RunIntegration)",
        "E2E_REQUESTED=$([bool]$RunEndToEnd)",
        "DOCKER_CONFIG_REQUESTED=$([bool]$RunDockerConfig)",
        "UTC=$([DateTimeOffset]::UtcNow.ToString('O'))"
    ) | Set-Content -LiteralPath $fail -Encoding UTF8
    throw
}
