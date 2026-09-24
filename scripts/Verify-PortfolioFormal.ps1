# Purpose: Runs the formal Windows portfolio verification chain and binds its evidence to one exact source revision.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$UnityPath,
    [string[]]$RequiredQaLanguages = @('python','csharp','cpp','javascript','customlang'),
    [string]$PostgreSqlHost = $(if ($env:POSTGRES_HOST) { $env:POSTGRES_HOST } else { '127.0.0.1' }),
    [int]$PostgreSqlPort = $(if ($env:POSTGRES_PORT) { [int]$env:POSTGRES_PORT } else { 5432 }),
    [string]$PostgreSqlUser = $(if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'dip' }),
    [string]$PostgreSqlPassword = $(if ($env:POSTGRES_PASSWORD) { $env:POSTGRES_PASSWORD } else { 'change-me-local-only' })
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($env:OS -ne 'Windows_NT') { throw 'Formal portfolio verification must run on Windows.' }
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

foreach ($command in @('git','dotnet','node','npm','psql','pg_dump','pg_restore')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command is required."
    }
}

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity Editor not found: $UnityPath"
}
$status = (& git -C $root status --porcelain | Out-String).Trim()
if ($status) { throw 'Formal verification requires a clean working tree.' }
$sha = (& git -C $root rev-parse HEAD | Out-String).Trim()
$out = Join-Path $root 'artifacts\formal-portfolio-verification'
New-Item -ItemType Directory -Force -Path $out | Out-Null
Remove-Item (Join-Path $out 'FORMAL_PORTFOLIO_VERIFICATION_PASS.txt') -Force -ErrorAction SilentlyContinue

$env:POSTGRES_HOST = $PostgreSqlHost
$env:POSTGRES_PORT = [string]$PostgreSqlPort
$env:POSTGRES_USER = $PostgreSqlUser
$env:POSTGRES_PASSWORD = $PostgreSqlPassword

& (Join-Path $PSScriptRoot 'Verify-Solution.ps1') -Configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Solution verification failed' }
& (Join-Path $PSScriptRoot 'Verify-UniversalQa.ps1') -RequireLanguages $RequiredQaLanguages
if ($LASTEXITCODE -ne 0) { throw 'Universal QA verification failed' }
& (Join-Path $PSScriptRoot 'Verify-QaGovernance.ps1') -Configuration Release
if ($LASTEXITCODE -ne 0) { throw 'QA Governance verification failed' }
& (Join-Path $PSScriptRoot 'Verify-GuiPhase1.ps1')
if ($LASTEXITCODE -ne 0) { throw 'GUI E2E verification failed' }
& (Join-Path $PSScriptRoot 'Verify-DatabaseMigrations.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Database migration verification failed' }
& (Join-Path $PSScriptRoot 'Verify-DisasterRecovery.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Disaster recovery verification failed' }
& (Join-Path $PSScriptRoot 'Run-GovernanceLoadTest.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Governance load/concurrency verification failed' }
& (Join-Path $PSScriptRoot 'Verify-UnityCollector.ps1') -UnityPath $UnityPath
if ($LASTEXITCODE -ne 0) { throw 'Unity runtime verification failed' }
& (Join-Path $PSScriptRoot 'Verify-ProjectControl.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Project Control verification failed' }
& (Join-Path $PSScriptRoot 'Verify-TechnicalIntelligence.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Technical Intelligence verification failed' }
& (Join-Path $PSScriptRoot 'Run-PortfolioBenchmarks.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Portfolio benchmark failed' }

$benchPath = Join-Path $root 'artifacts\portfolio-benchmark\portfolio-benchmark-results.json'
if (-not (Test-Path -LiteralPath $benchPath -PathType Leaf)) {
    throw 'Portfolio benchmark evidence was not generated.'
}
$bench = Get-Content $benchPath -Raw | ConvertFrom-Json
if ($bench.sourceCommit -ne $sha) {
    throw "Benchmark evidence belongs to $($bench.sourceCommit), expected $sha"
}

$payload = [ordered]@{
    schema = 'dip-formal-portfolio-verification-v6'
    runtime = 'native'
    sourceCommit = $sha
    verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    os = [Runtime.InteropServices.RuntimeInformation]::OSDescription
    dotnet = (& dotnet --version | Out-String).Trim()
    unityPath = (Resolve-Path $UnityPath).Path
    postgres = "$PostgreSqlHost`:$PostgreSqlPort"
    qaLanguages = $RequiredQaLanguages
    benchmark = $bench.scenarios
}
$payload | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $out 'formal-verification.json') -Encoding UTF8
@(
    'FORMAL PORTFOLIO VERIFICATION PASS',
    "Commit=$sha",
    "VerifiedAtUtc=$($payload.verifiedAtUtc)",
    'Runtime=Native',
    'DockerRequired=NO',
    'Windows=PASS',
    '.NET=PASS',
    'Unity=PASS',
    'UniversalQA=PASS',
    'QaGovernance=PASS',
    'GuiE2E=PASS',
    'DatabaseMigration=PASS',
    'DisasterRecovery=PASS',
    'GovernanceLoad=PASS',
    'AuthorizationHardening=PASS',
    'BackupRestoreDR=PASS',
    'ProjectControl=PASS',
    'TechnicalIntelligence=PASS',
    'Benchmark=PASS'
) | Set-Content (Join-Path $out 'FORMAL_PORTFOLIO_VERIFICATION_PASS.txt') -Encoding UTF8
Write-Host "FORMAL PORTFOLIO VERIFICATION PASS ($sha)" -ForegroundColor Green
