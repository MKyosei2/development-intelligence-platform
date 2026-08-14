[CmdletBinding()]
param(
    [switch]$RequireAuthentication,
    [switch]$WithMonitoring,
    [switch]$WithStorage,
    [switch]$NoBrowser,
    [switch]$NoBuild,
    [int]$WebPort = 5081,
    [int]$ApiPort = 5080,
    [int]$AnalysisPort = 8100,
    [int]$WorkerMetricsPort = 9091,
    [int]$StartupTimeoutSeconds = 240
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$runtime = Join-Path $root 'artifacts\runtime'
$envFile = Join-Path $runtime 'dip.runtime.env'
$logFile = Join-Path $runtime 'startup.log'
New-Item -ItemType Directory -Force -Path $runtime | Out-Null

function Write-Status([string]$Name, [string]$State, [ConsoleColor]$Color = [ConsoleColor]::Gray) {
    $old = $Host.UI.RawUI.ForegroundColor
    try {
        $Host.UI.RawUI.ForegroundColor = $Color
        Write-Host ('{0,-22} {1}' -f $Name, $State)
    } finally {
        $Host.UI.RawUI.ForegroundColor = $old
    }
}

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name is required. Install Docker Desktop and make sure '$Name' is on PATH."
    }
}

function Invoke-Compose([string[]]$Arguments) {
    $prefix = @('compose', '--env-file', $envFile)
    & docker @prefix @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose failed with exit code $LASTEXITCODE."
    }
}

function Test-Http([string]$Url) {
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 500
    } catch {
        return $false
    }
}

function Wait-Http([string]$Name, [string]$Url, [DateTimeOffset]$Deadline) {
    while ([DateTimeOffset]::UtcNow -lt $Deadline) {
        if (Test-Http $Url) {
            Write-Status $Name 'PASS' Green
            return
        }
        Start-Sleep -Seconds 2
    }
    throw "$Name did not become ready before the startup timeout. URL: $Url"
}

Require-Command 'docker'
try {
    & docker info *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Docker daemon is not responding.' }
} catch {
    throw 'Docker Desktop is required and must be running before DIP can start.'
}
& docker compose version *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker Compose v2 is required.' }

if ($RequireAuthentication) {
    foreach ($name in 'DIP_GITHUB_OAUTH_CLIENT_ID','DIP_GITHUB_OAUTH_CLIENT_SECRET','DIP_JWT_SIGNING_KEY') {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ([string]::IsNullOrWhiteSpace($value)) {
            throw "$name is required when -RequireAuthentication is used."
        }
    }
    if ([Environment]::GetEnvironmentVariable('DIP_JWT_SIGNING_KEY').Length -lt 32) {
        throw 'DIP_JWT_SIGNING_KEY must contain at least 32 characters.'
    }
}

$lines = @(
    'ASPNETCORE_ENVIRONMENT=Development',
    "WEB_PORT=$WebPort",
    "API_PORT=$ApiPort",
    "ANALYSIS_PORT=$AnalysisPort",
    "WORKER_METRICS_PORT=$WorkerMetricsPort",
    ('DIP_REQUIRE_AUTHENTICATION=' + $(if ($RequireAuthentication) { 'true' } else { 'false' }))
)
$lines | Set-Content -Path $envFile -Encoding ASCII

$profiles = @()
if ($WithMonitoring) { $profiles += @('--profile','monitoring') }
if ($WithStorage) { $profiles += @('--profile','storage') }

Write-Host ''
Write-Host '========================================' -ForegroundColor Cyan
Write-Host ' Development Intelligence Platform' -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan
Write-Status 'Docker daemon' 'PASS' Green
Write-Status 'Authentication' $(if ($RequireAuthentication) { 'GitHub OAuth' } else { 'Local development mode' }) Yellow

try {
    Invoke-Compose @('config','--quiet')
    Write-Status 'Compose validation' 'PASS' Green

    $up = @()
    $up += $profiles
    $up += @('up','-d','--remove-orphans')
    if (-not $NoBuild) { $up += '--build' }
    Invoke-Compose $up

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds([Math]::Max(60, [Math]::Min(900, $StartupTimeoutSeconds)))
    Wait-Http 'Python Analysis' "http://127.0.0.1:$AnalysisPort/health" $deadline
    Wait-Http 'API' "http://127.0.0.1:$ApiPort/health" $deadline
    Wait-Http 'Worker' "http://127.0.0.1:$WorkerMetricsPort/metrics" $deadline
    Wait-Http 'Web GUI' "http://127.0.0.1:$WebPort/" $deadline

    $statusArgs = @()
    $statusArgs += $profiles
    $statusArgs += @('ps')
    $statusText = (& docker compose --env-file $envFile @statusArgs 2>&1 | Out-String)
    $statusText | Set-Content -Path $logFile -Encoding UTF8

    Write-Host ''
    Write-Host 'STATUS: READY' -ForegroundColor Green
    Write-Host "GUI:      http://127.0.0.1:$WebPort" -ForegroundColor Cyan
    Write-Host "API:      http://127.0.0.1:$ApiPort" -ForegroundColor Cyan
    Write-Host "Swagger:  http://127.0.0.1:$ApiPort/swagger" -ForegroundColor Cyan
    Write-Host "Analysis: http://127.0.0.1:$AnalysisPort/health" -ForegroundColor Cyan
    Write-Host "System:   http://127.0.0.1:$WebPort/system" -ForegroundColor Cyan
    if ($WithMonitoring) {
        Write-Host 'Grafana:  http://127.0.0.1:3000' -ForegroundColor Cyan
    }
    Write-Host ''
    Write-Host 'Stop with Stop-DIP.cmd. Data volumes are preserved by default.'

    if (-not $NoBrowser) {
        Start-Process "http://127.0.0.1:$WebPort"
    }
} catch {
    Write-Host ''
    Write-Host ('DIP startup failed: ' + $_.Exception.Message) -ForegroundColor Red
    try {
        $logArgs = @()
        $logArgs += $profiles
        $logArgs += @('logs','--no-color','--tail','160')
        (& docker compose --env-file $envFile @logArgs 2>&1 | Out-String) | Set-Content -Path $logFile -Encoding UTF8
        Write-Host "Diagnostics: $logFile" -ForegroundColor Yellow
    } catch { }
    exit 1
}
