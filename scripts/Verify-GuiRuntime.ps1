[CmdletBinding()]
param(
    [int]$StartupTimeoutSeconds = 420,
    [switch]$KeepOnFailure
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root 'artifacts\gui-runtime-verification'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$passFile = Join-Path $out 'GUI_RUNTIME_VERIFICATION_PASS.txt'
$resultFile = Join-Path $out 'gui-runtime-verification.json'
$logFile = Join-Path $out 'compose.log'
$envFile = Join-Path $out 'gui-smoke.env'
Remove-Item $passFile -Force -ErrorAction SilentlyContinue

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker Desktop / docker CLI is required for GUI runtime verification.'
}
& docker info *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker daemon is not responding.' }
& docker compose version *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker Compose v2 is required.' }

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new(
        [System.Net.IPAddress]::Loopback,
        0)
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
                Write-Host ('{0,-24} PASS' -f $Name) -ForegroundColor Green
                return $response
            }
        }
        catch { }
        Start-Sleep -Seconds 2
    }
    throw "$Name did not become ready: $Url"
}

$ports = [ordered]@{
    POSTGRES_PORT = Get-FreeTcpPort
    REDIS_PORT = Get-FreeTcpPort
    ANALYSIS_PORT = Get-FreeTcpPort
    API_PORT = Get-FreeTcpPort
    WEB_PORT = Get-FreeTcpPort
    WORKER_METRICS_PORT = Get-FreeTcpPort
}
$composeProject = 'dip-gui-smoke-' + ([Guid]::NewGuid().ToString('N').Substring(0, 10))

@(
    'ASPNETCORE_ENVIRONMENT=Development'
    'DIP_REQUIRE_AUTHENTICATION=false'
    'POSTGRES_DB=dip_gui_smoke'
    'POSTGRES_USER=dip'
    'POSTGRES_PASSWORD=dip-local-password'
    "POSTGRES_PORT=$($ports.POSTGRES_PORT)"
    "REDIS_PORT=$($ports.REDIS_PORT)"
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
$projectId = $null

try {
    Write-Host "Building isolated DIP GUI stack ($composeProject)..." -ForegroundColor Cyan
    & docker compose -p $composeProject --env-file $envFile up -d --build --remove-orphans postgres redis python-analysis api worker web
    if ($LASTEXITCODE -ne 0) { throw "docker compose up failed with exit code $LASTEXITCODE." }

    $maxSeconds = [Math]::Max(120, [Math]::Min(1200, $StartupTimeoutSeconds))
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($maxSeconds)
    Wait-Http 'Python Analysis' "$analysis/health" $deadline | Out-Null
    Wait-Http 'API' "$api/health" $deadline | Out-Null
    Wait-Http 'Worker metrics' "$worker/metrics" $deadline | Out-Null
    $home = Wait-Http 'Web GUI' "$web/" $deadline

    if ($home.Content -notmatch 'Development Intelligence') {
        throw 'Web GUI loaded, but the DIP application shell was not rendered.'
    }

    $newProject = Invoke-WebRequest -Uri "$web/projects/new" -UseBasicParsing -TimeoutSec 10
    if ($newProject.StatusCode -ne 200 -or $newProject.Content -notmatch 'Create project') {
        throw 'Create Project GUI did not render correctly.'
    }
    Write-Host ('{0,-24} PASS' -f 'Create Project GUI') -ForegroundColor Green

    $status = Invoke-RestMethod -Uri "$api/api/v1/system/status" -Method Get -TimeoutSec 15
    $requiredComponents = @('api','database','redis','analysis','worker','artifact-store')
    foreach ($componentKey in $requiredComponents) {
        $component = @($status.components | Where-Object { $_.key -eq $componentKey }) | Select-Object -First 1
        if ($null -eq $component) { throw "System Status omitted required component '$componentKey'." }
        if ($component.state -ne 'Healthy') {
            throw "System Status reports $componentKey as $($component.state): $($component.detail)"
        }
    }
    Write-Host ('{0,-24} PASS' -f 'System Status') -ForegroundColor Green

    $projectName = 'GUI Runtime Smoke ' + ([DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
    $body = @{
        name = $projectName
        repositoryUrl = 'https://github.com/example/dip-gui-runtime-smoke'
        defaultBranch = 'main'
        releaseDateUtc = $null
    } | ConvertTo-Json
    $created = Invoke-RestMethod -Uri "$api/api/projects" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 15
    if ([string]::IsNullOrWhiteSpace([string]$created.id)) { throw 'Project creation did not return an id.' }
    $projectId = [string]$created.id

    $projects = @(Invoke-RestMethod -Uri "$api/api/projects" -Method Get -TimeoutSec 15)
    if (-not ($projects | Where-Object { [string]$_.id -eq $projectId })) {
        throw 'Created project was not returned by the portfolio API.'
    }
    Write-Host ('{0,-24} PASS' -f 'Project create/list') -ForegroundColor Green

    $dashboard = Wait-Http 'Project Dashboard' "$web/dashboard/$projectId" $deadline
    if ($dashboard.Content -notmatch [Regex]::Escape($projectName)) {
        throw 'Project Dashboard did not render the created project.'
    }

    $intelligence = Wait-Http 'Project Intelligence' "$web/dashboard/$projectId/intelligence" $deadline
    if ($intelligence.Content -notmatch 'Project Intelligence') {
        throw 'Project Intelligence GUI did not render.'
    }

    $governance = Wait-Http 'QA Governance' "$web/dashboard/$projectId/qa-governance" $deadline
    if ($governance.Content -notmatch 'QA Governance') {
        throw 'QA Governance GUI did not render.'
    }

    $success = $true
    $verifiedAt = [DateTimeOffset]::UtcNow
    $payload = [ordered]@{
        schema = 'dip-gui-runtime-verification-v1'
        verifiedAtUtc = $verifiedAt.ToString('o')
        startedAtUtc = $startedAt.ToString('o')
        isolatedComposeProject = $composeProject
        projectId = $projectId
        checks = @(
            'Docker build: API/Worker/Web/Python Analysis',
            'PostgreSQL/Redis readiness',
            'Python Analysis health',
            'API health',
            'Worker metrics',
            'Blazor interactive application shell',
            'Create Project GUI',
            'System Status all required core components healthy',
            'Project create/list persistence path',
            'Project Dashboard render',
            'Project Intelligence render',
            'QA Governance render'
        )
    }
    $payload | ConvertTo-Json -Depth 8 | Set-Content -Path $resultFile -Encoding UTF8
    @(
        'GUI RUNTIME VERIFICATION PASS',
        "VerifiedAtUtc=$($verifiedAt.ToString('o'))",
        "ProjectId=$projectId",
        'DockerBuild=PASS',
        'Analysis=PASS',
        'API=PASS',
        'Worker=PASS',
        'Web=PASS',
        'SystemStatus=PASS',
        'ProjectCreate=PASS',
        'ProjectDashboard=PASS',
        'ProjectIntelligence=PASS',
        'QaGovernance=PASS'
    ) | Set-Content -Path $passFile -Encoding UTF8
    Write-Host 'GUI RUNTIME VERIFICATION PASS' -ForegroundColor Green
}
catch {
    Write-Host ('GUI runtime verification failed: ' + $_.Exception.Message) -ForegroundColor Red
    try {
        (& docker compose -p $composeProject --env-file $envFile logs --no-color --tail 240 2>&1 | Out-String) |
            Set-Content -Path $logFile -Encoding UTF8
        Write-Host "Diagnostics: $logFile" -ForegroundColor Yellow
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
