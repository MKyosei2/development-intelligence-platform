param(
    [switch]$NoBuild,
    [switch]$VerifyGui,
    [switch]$NoBrowser,
    [int]$DockerTimeoutSeconds = 120,
    [int]$HealthTimeoutSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location -LiteralPath $RepoRoot

$WebUrl = "http://127.0.0.1:5081/"
$ApiHealthUrl = "http://127.0.0.1:5080/health"
$AnalysisHealthUrl = "http://127.0.0.1:8100/health"
$WorkerMetricsUrl = "http://127.0.0.1:9091/metrics"

function Write-Step {
    param([int]$Number, [string]$Message)
    Write-Host ("[{0}/6] {1}" -f $Number, $Message) -ForegroundColor Cyan
}

function Test-DockerDaemon {
    & docker info --format "{{.ServerVersion}}" 1>$null 2>$null
    return ($LASTEXITCODE -eq 0)
}

function Start-DockerDesktopIfNeeded {
    if (Test-DockerDaemon) {
        Write-Host "Docker Engine is already running." -ForegroundColor DarkGray
        return
    }

    $candidates = @(
        (Join-Path $env:ProgramFiles "Docker\Docker\Docker Desktop.exe"),
        (Join-Path $env:LOCALAPPDATA "Docker\Docker Desktop.exe")
    )

    $dockerDesktop = $candidates |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1

    if (-not $dockerDesktop) {
        throw "Docker Engine is not running and Docker Desktop was not found. Install Docker Desktop once, then run Start-DIP.cmd again."
    }

    Write-Host "Starting Docker Desktop automatically..." -ForegroundColor Yellow
    Start-Process -FilePath $dockerDesktop | Out-Null

    $deadline = (Get-Date).AddSeconds($DockerTimeoutSeconds)
    do {
        Start-Sleep -Seconds 2
        if (Test-DockerDaemon) {
            Write-Host "Docker Engine: READY" -ForegroundColor Green
            return
        }
    } while ((Get-Date) -lt $deadline)

    throw "Docker Desktop started, but Docker Engine did not become ready within $DockerTimeoutSeconds seconds."
}

function Wait-HttpEndpoint {
    param([string]$Name, [string]$Url, [DateTime]$Deadline)

    do {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 4 -ErrorAction Stop
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Write-Host ("  [PASS] {0,-18} HTTP {1}" -f $Name, $response.StatusCode) -ForegroundColor Green
                return
            }
        }
        catch {
        }

        Start-Sleep -Milliseconds 750
    } while ((Get-Date) -lt $Deadline)

    throw "$Name did not become healthy: $Url"
}

function Show-FailureDiagnostics {
    Write-Host ""
    Write-Host "===== docker compose ps -a =====" -ForegroundColor Yellow
    try { & docker compose ps -a } catch {}

    Write-Host ""
    Write-Host "===== recent compose logs =====" -ForegroundColor Yellow
    try { & docker compose logs --tail=120 } catch {}
}

try {
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host " Development Intelligence Platform" -ForegroundColor Cyan
    Write-Host " Full local runtime launcher" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host ""

    Write-Step 1 "Checking local runtime"

    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "Docker CLI was not found. Install Docker Desktop once, then run Start-DIP.cmd again."
    }

    & docker compose version | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose is not available."
    }

    Start-DockerDesktopIfNeeded

    Write-Step 2 "Preparing standard local configuration"

    $env:DIP_REQUIRE_AUTHENTICATION = "false"
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:API_PORT = "5080"
    $env:WEB_PORT = "5081"
    $env:ANALYSIS_PORT = "8100"
    $env:WORKER_METRICS_PORT = "9091"

    & docker compose config --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose configuration validation failed."
    }

    if ($NoBuild) {
        Write-Step 3 "Starting DIP services"
        & docker compose up -d --remove-orphans
    }
    else {
        Write-Step 3 "Building and starting DIP services"
        & docker compose up -d --build --remove-orphans
    }

    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up failed."
    }

    Write-Step 4 "Waiting for the full stack"

    $deadline = (Get-Date).AddSeconds($HealthTimeoutSeconds)
    Wait-HttpEndpoint -Name "Python Analysis" -Url $AnalysisHealthUrl -Deadline $deadline
    Wait-HttpEndpoint -Name "API" -Url $ApiHealthUrl -Deadline $deadline
    Wait-HttpEndpoint -Name "Worker Metrics" -Url $WorkerMetricsUrl -Deadline $deadline
    Wait-HttpEndpoint -Name "Web" -Url $WebUrl -Deadline $deadline

    Write-Step 5 "Verifying runtime status"

    & docker compose ps
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose ps failed."
    }

    if ($VerifyGui) {
        Write-Host ""
        Write-Host "Running Playwright GUI E2E against the full stack..." -ForegroundColor Cyan

        if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
            throw "-VerifyGui requires Node.js/npm. The DIP runtime itself is already healthy."
        }

        $e2e = Join-Path $RepoRoot "tests\Dip.UiE2E"
        Push-Location -LiteralPath $e2e
        try {
            if (Test-Path -LiteralPath "package-lock.json") { & npm ci } else { & npm install }
            if ($LASTEXITCODE -ne 0) { throw "npm dependency installation failed." }

            & npx playwright install chromium
            if ($LASTEXITCODE -ne 0) { throw "Playwright Chromium installation failed." }

            $env:DIP_WEB_BASE_URL = $WebUrl.TrimEnd("/")
            & npm test
            if ($LASTEXITCODE -ne 0) { throw "Playwright GUI E2E failed." }

            Write-Host "GUI E2E: PASS" -ForegroundColor Green
        }
        finally {
            Pop-Location
        }
    }

    Write-Step 6 "Opening DIP"

    Write-Host ""
    Write-Host "DIP runtime: READY" -ForegroundColor Green
    Write-Host "Web:             $WebUrl" -ForegroundColor Green
    Write-Host "API health:      $ApiHealthUrl" -ForegroundColor DarkGray
    Write-Host "Analysis health: $AnalysisHealthUrl" -ForegroundColor DarkGray
    Write-Host "Worker metrics:  $WorkerMetricsUrl" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Same PostgreSQL / Redis / Python Analysis / API / Worker / Web stack as docker-compose.yml." -ForegroundColor DarkGray
    Write-Host ""

    if (-not $NoBrowser) {
        Start-Process $WebUrl
    }

    exit 0
}
catch {
    Write-Host ""
    Write-Host "DIP startup FAILED" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Show-FailureDiagnostics
    Write-Host ""
    exit 1
}
