[CmdletBinding()]
param(
    [int]$StartupTimeoutSeconds = 420,
    [switch]$KeepOnFailure,
    [switch]$SkipBrowserInstall
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$e2e = Join-Path $root 'tests\Dip.UiE2E'
$out = Join-Path $root 'artifacts\gui-phase1-e2e'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$passFile = Join-Path $out 'GUI_PHASE1_E2E_PASS.txt'
$resultFile = Join-Path $out 'gui-phase1-e2e.json'
$composeLog = Join-Path $out 'compose.log'
$envFile = Join-Path $out 'gui-phase1.env'
Remove-Item $passFile -Force -ErrorAction SilentlyContinue

function Assert-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally { $listener.Stop() }
}

function Wait-Http([string]$Name, [string]$Url, [DateTimeOffset]$Deadline) {
    while ([DateTimeOffset]::UtcNow -lt $Deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Write-Host ('{0,-28} PASS' -f $Name) -ForegroundColor Green
                return
            }
        }
        catch { }
        Start-Sleep -Seconds 2
    }
    throw "$Name did not become ready: $Url"
}

Assert-Command docker
Assert-Command node
Assert-Command npm
& docker info *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker daemon is not responding.' }
& docker compose version *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker Compose v2 is required.' }

# Run source/package checks before spending time building containers.
& (Join-Path $PSScriptRoot 'Verify-GuiPhase1Static.ps1')
if ($LASTEXITCODE -ne 0) { throw 'GUI Phase 1 static validation failed.' }

$ports = [ordered]@{
    POSTGRES_PORT = Get-FreeTcpPort
    ANALYSIS_PORT = Get-FreeTcpPort
    API_PORT = Get-FreeTcpPort
    WEB_PORT = Get-FreeTcpPort
    WORKER_METRICS_PORT = Get-FreeTcpPort
}
$composeProject = 'dip-gui-phase1-' + ([Guid]::NewGuid().ToString('N').Substring(0, 10))
@(
    'ASPNETCORE_ENVIRONMENT=Development'
    'DIP_REQUIRE_AUTHENTICATION=false'
    'POSTGRES_DB=dip_gui_phase1'
    'POSTGRES_USER=dip'
    'POSTGRES_PASSWORD=dip-local-password'
    "POSTGRES_PORT=$($ports.POSTGRES_PORT)"
    "ANALYSIS_PORT=$($ports.ANALYSIS_PORT)"
    "API_PORT=$($ports.API_PORT)"
    "WEB_PORT=$($ports.WEB_PORT)"
    "WORKER_METRICS_PORT=$($ports.WORKER_METRICS_PORT)"
) | Set-Content -Path $envFile -Encoding ASCII

$api = "http://127.0.0.1:$($ports.API_PORT)"
$web = "http://127.0.0.1:$($ports.WEB_PORT)"
$analysis = "http://127.0.0.1:$($ports.ANALYSIS_PORT)"
$worker = "http://127.0.0.1:$($ports.WORKER_METRICS_PORT)"
$startedAt = [DateTimeOffset]::UtcNow
$success = $false

try {
    Write-Host "Building isolated GUI Phase 1 stack ($composeProject)..." -ForegroundColor Cyan
    & docker compose -p $composeProject --env-file $envFile up -d --build --remove-orphans postgres python-analysis api worker web
    if ($LASTEXITCODE -ne 0) { throw "docker compose up failed with exit code $LASTEXITCODE." }

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds([Math]::Max(120, [Math]::Min(1200, $StartupTimeoutSeconds)))
    Wait-Http 'Python Analysis' "$analysis/health" $deadline
    Wait-Http 'API health' "$api/health" $deadline
    Wait-Http 'Worker metrics' "$worker/metrics" $deadline
    Wait-Http 'Web GUI' "$web/" $deadline

    $status = Invoke-RestMethod -Uri "$api/api/v1/system/status" -Method Get -TimeoutSec 15
    foreach ($key in @('api','database','coordination','analysis','worker','artifact-store')) {
        $component = @($status.components | Where-Object { $_.key -eq $key }) | Select-Object -First 1
        if ($null -eq $component -or $component.state -ne 'Healthy') {
            throw "System Status core component '$key' is not Healthy."
        }
    }
    Write-Host ('{0,-28} PASS' -f 'System Status core') -ForegroundColor Green

    Push-Location $e2e
    try {
        if (-not (Test-Path (Join-Path $e2e 'node_modules\@playwright\test'))) {
            npm install --no-audit --no-fund
            if ($LASTEXITCODE -ne 0) { throw 'npm install failed.' }
        }
        if (-not $SkipBrowserInstall) {
            npx playwright install chromium
            if ($LASTEXITCODE -ne 0) { throw 'Playwright Chromium installation failed.' }
        }
        $env:DIP_WEB_BASE_URL = $web
        npx playwright test --grep-invert '@live-github'
        if ($LASTEXITCODE -ne 0) { throw 'Playwright GUI Phase 1 E2E failed.' }
    }
    finally { Pop-Location }

    $success = $true
    $verifiedAt = [DateTimeOffset]::UtcNow
    [ordered]@{
        schema = 'dip-gui-phase1-e2e-v1'
        startedAtUtc = $startedAt.ToString('o')
        verifiedAtUtc = $verifiedAt.ToString('o')
        isolatedComposeProject = $composeProject
        webBaseUrl = $web
        checks = @(
            'Static GUI Phase 1 package validation',
            'Docker build and isolated stack readiness',
            'System Status core components',
            'Portfolio GUI',
            'GitHub Repository Picker connected/disconnected state',
            'Manual project creation',
            'Active Project selection state',
            'Project Overview',
            'Repository GUI',
            'CI / Builds GUI',
            'QA GUI and workspace-required state'
        )
    } | ConvertTo-Json -Depth 6 | Set-Content -Path $resultFile -Encoding UTF8
    @(
        'GUI PHASE 1 E2E PASS',
        "VerifiedAtUtc=$($verifiedAt.ToString('o'))",
        'StaticValidation=PASS',
        'DockerBuild=PASS',
        'SystemStatusCore=PASS',
        'Playwright=PASS'
    ) | Set-Content -Path $passFile -Encoding UTF8
    Write-Host 'GUI PHASE 1 E2E PASS' -ForegroundColor Green
}
catch {
    Write-Host ('GUI Phase 1 verification failed: ' + $_.Exception.Message) -ForegroundColor Red
    try {
        (& docker compose -p $composeProject --env-file $envFile logs --no-color --tail 300 2>&1 | Out-String) |
            Set-Content -Path $composeLog -Encoding UTF8
        Write-Host "Diagnostics: $composeLog" -ForegroundColor Yellow
    }
    catch { }
    throw
}
finally {
    if ($success -or -not $KeepOnFailure) {
        & docker compose -p $composeProject --env-file $envFile down --volumes --remove-orphans *> $null
    }
    elseif (-not $success) {
        Write-Host "Smoke stack kept for inspection: $composeProject" -ForegroundColor Yellow
    }
}
