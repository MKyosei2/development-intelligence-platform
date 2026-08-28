[CmdletBinding()]
param(
    [int]$StartupTimeoutSeconds = 420,
    [switch]$RunPlaywright,
    [switch]$Headed,
    [switch]$ReuseImages,
    [switch]$KeepOnFailure,
    [switch]$KeepOnSuccess
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$timestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd_HHmmss')
$outRoot = Join-Path (Join-Path $root 'artifacts') 'gui-runtime-verification'
$out = Join-Path $outRoot $timestamp
New-Item -ItemType Directory -Force -Path $out | Out-Null

$passFile = Join-Path $out 'GUI_RUNTIME_VERIFICATION_PASS.txt'
$resultFile = Join-Path $out 'gui-runtime-verification.json'
$composeLog = Join-Path $out 'docker-compose.log'
$playwrightLog = Join-Path $out 'playwright.log'
$envFile = Join-Path $out 'gui-smoke.env'

function Assert-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$LogFile
    )

    # Docker and several other native tools legitimately write progress to stderr.
    # Windows PowerShell can surface those records as NativeCommandError when the
    # caller uses ErrorActionPreference=Stop. The process exit code is therefore
    # the source of truth for native-command success here.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $lines = @(& $Executable @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    if ($lines.Count -gt 0) {
        foreach ($line in $lines) {
            Write-Host ([string]$line)
        }
        if (-not [string]::IsNullOrWhiteSpace($LogFile)) {
            $lines | ForEach-Object { [string]$_ } | Add-Content -Path $LogFile -Encoding UTF8
        }
    }

    if ($exitCode -ne 0) {
        throw "$Executable $($Arguments -join ' ') failed with exit code $exitCode."
    }

    return $lines
}

function Invoke-Docker([string[]]$Arguments) {
    return Invoke-NativeChecked -Executable 'docker' -Arguments $Arguments -LogFile $composeLog
}

function Get-FreeTcpPort([System.Collections.Generic.HashSet[int]]$Used) {
    while ($true) {
        $listener = [System.Net.Sockets.TcpListener]::new(
            [System.Net.IPAddress]::Loopback,
            0)
        try {
            $listener.Start()
            $port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
        }
        finally {
            $listener.Stop()
        }

        if ($Used.Add($port)) {
            return $port
        }
    }
}

function Wait-Http {
    param(
        [string]$Name,
        [string]$Url,
        [DateTimeOffset]$Deadline
    )

    while ([DateTimeOffset]::UtcNow -lt $Deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Write-Host ('{0,-26} PASS  {1}' -f $Name, $Url) -ForegroundColor Green
                return $response
            }
        }
        catch {
            # Service is still starting. Detailed logs are collected on terminal failure.
        }
        Start-Sleep -Seconds 2
    }

    throw "$Name did not become ready before timeout: $Url"
}

function Get-ComposeContainerId([string]$Project, [string]$Service) {
    $lines = Invoke-Docker @('compose', '-p', $Project, 'ps', '-q', $Service)
    return (($lines | Out-String).Trim())
}

function Wait-ContainerHealthy {
    param(
        [string]$Project,
        [string]$Service,
        [DateTimeOffset]$Deadline
    )

    while ([DateTimeOffset]::UtcNow -lt $Deadline) {
        $id = Get-ComposeContainerId $Project $Service
        if (-not [string]::IsNullOrWhiteSpace($id)) {
            $previousPreference = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            try {
                $stateLines = @(& docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $id 2>&1)
                $exitCode = $LASTEXITCODE
            }
            finally {
                $ErrorActionPreference = $previousPreference
            }
            if ($exitCode -eq 0) {
                $state = (($stateLines | Out-String).Trim()).ToLowerInvariant()
                if ($state -eq 'healthy' -or $state -eq 'running') {
                    Write-Host ('{0,-26} PASS  {1}' -f $Service, $state) -ForegroundColor Green
                    return
                }
                if ($state -eq 'unhealthy' -or $state -eq 'exited' -or $state -eq 'dead') {
                    throw "Container '$Service' entered terminal state '$state'."
                }
            }
        }
        Start-Sleep -Seconds 2
    }

    throw "Container '$Service' did not become healthy before timeout."
}

function Write-StackDiagnostics([string]$Project) {
    try {
        Invoke-Docker @('compose', '-p', $Project, 'ps') | Out-Null
    }
    catch { }

    try {
        $previousPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $logs = @(& docker compose -p $Project logs --no-color --tail 400 2>&1)
        }
        finally {
            $ErrorActionPreference = $previousPreference
        }
        if ($logs.Count -gt 0) {
            $logs | ForEach-Object { [string]$_ } | Set-Content -Path (Join-Path $out 'stack-diagnostics.log') -Encoding UTF8
        }
    }
    catch { }
}

Assert-Command 'docker'
Assert-Command 'git'

# Verify daemon and Compose independently. Do not treat Docker's stderr progress as failure.
Invoke-NativeChecked -Executable 'docker' -Arguments @('info') -LogFile $composeLog | Out-Null
Invoke-NativeChecked -Executable 'docker' -Arguments @('compose', 'version') -LogFile $composeLog | Out-Null

$usedPorts = [System.Collections.Generic.HashSet[int]]::new()
$ports = [ordered]@{
    POSTGRES_PORT = Get-FreeTcpPort $usedPorts
    REDIS_PORT = Get-FreeTcpPort $usedPorts
    ANALYSIS_PORT = Get-FreeTcpPort $usedPorts
    API_PORT = Get-FreeTcpPort $usedPorts
    WORKER_METRICS_PORT = Get-FreeTcpPort $usedPorts
    WEB_PORT = Get-FreeTcpPort $usedPorts
}

$composeProject = 'dip-gui-smoke-' + ([Guid]::NewGuid().ToString('N').Substring(0, 10))
$startedAt = [DateTimeOffset]::UtcNow
$success = $false
$projectId = $null
$playwrightPassed = $false
$previousLocation = Get-Location

$environment = [ordered]@{
    ASPNETCORE_ENVIRONMENT = 'Development'
    DOTNET_ENVIRONMENT = 'Development'
    DIP_REQUIRE_AUTHENTICATION = 'false'
    POSTGRES_DB = 'dip_gui_smoke'
    POSTGRES_USER = 'dip'
    POSTGRES_PASSWORD = 'dip-local-password'
    POSTGRES_PORT = [string]$ports.POSTGRES_PORT
    REDIS_PORT = [string]$ports.REDIS_PORT
    ANALYSIS_PORT = [string]$ports.ANALYSIS_PORT
    API_PORT = [string]$ports.API_PORT
    WORKER_METRICS_PORT = [string]$ports.WORKER_METRICS_PORT
    WEB_PORT = [string]$ports.WEB_PORT
}

foreach ($entry in $environment.GetEnumerator()) {
    Set-Item -Path ("Env:" + $entry.Key) -Value $entry.Value
}

$environment.GetEnumerator() |
    ForEach-Object { '{0}={1}' -f $_.Key, $_.Value } |
    Set-Content -Path $envFile -Encoding ASCII

$analysis = "http://127.0.0.1:$($ports.ANALYSIS_PORT)"
$api = "http://127.0.0.1:$($ports.API_PORT)"
$worker = "http://127.0.0.1:$($ports.WORKER_METRICS_PORT)"
$web = "http://127.0.0.1:$($ports.WEB_PORT)"

Write-Host ''
Write-Host '==================================================' -ForegroundColor Cyan
Write-Host 'DIP GUI RUNTIME VERIFICATION' -ForegroundColor Cyan
Write-Host '==================================================' -ForegroundColor Cyan
Write-Host "Compose project : $composeProject"
Write-Host "PostgreSQL      : $($ports.POSTGRES_PORT)"
Write-Host "Redis           : $($ports.REDIS_PORT)"
Write-Host "Analysis        : $($ports.ANALYSIS_PORT)"
Write-Host "API             : $($ports.API_PORT)"
Write-Host "Worker metrics  : $($ports.WORKER_METRICS_PORT)"
Write-Host "Web GUI         : $($ports.WEB_PORT)"
Write-Host ''

try {
    Set-Location $root
    $maxSeconds = [Math]::Max(120, [Math]::Min(1200, $StartupTimeoutSeconds))
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($maxSeconds)

    $buildSwitch = if ($ReuseImages) { '--no-build' } else { '--build' }

    Write-Host 'STEP 1/5: infrastructure' -ForegroundColor Cyan
    Invoke-Docker @('compose', '-p', $composeProject, 'up', '-d', $buildSwitch, 'postgres', 'redis', 'python-analysis') | Out-Null
    Wait-ContainerHealthy $composeProject 'postgres' $deadline
    Wait-ContainerHealthy $composeProject 'redis' $deadline
    Wait-ContainerHealthy $composeProject 'python-analysis' $deadline
    Wait-Http 'Python Analysis' "$analysis/health" $deadline | Out-Null

    Write-Host 'STEP 2/5: API' -ForegroundColor Cyan
    Invoke-Docker @('compose', '-p', $composeProject, 'up', '-d', $buildSwitch, 'api') | Out-Null
    Wait-ContainerHealthy $composeProject 'api' $deadline
    Wait-Http 'API health' "$api/health" $deadline | Out-Null

    Write-Host 'STEP 3/5: worker + web' -ForegroundColor Cyan
    Invoke-Docker @('compose', '-p', $composeProject, 'up', '-d', $buildSwitch, 'worker', 'web') | Out-Null
    Wait-ContainerHealthy $composeProject 'worker' $deadline
    Wait-ContainerHealthy $composeProject 'web' $deadline
    Wait-Http 'Worker metrics' "$worker/metrics" $deadline | Out-Null
    $home = Wait-Http 'Web GUI' "$web/" $deadline

    if ($home.Content -notmatch 'Development Intelligence') {
        throw 'Web GUI responded, but the DIP application shell was not rendered.'
    }

    Write-Host 'STEP 4/5: deterministic runtime smoke' -ForegroundColor Cyan
    $newProject = Invoke-WebRequest -Uri "$web/projects/new" -UseBasicParsing -TimeoutSec 10
    if ($newProject.StatusCode -ne 200 -or $newProject.Content -notmatch 'Create project') {
        throw 'Create Project GUI did not render correctly.'
    }
    Write-Host ('{0,-26} PASS' -f 'Create Project GUI') -ForegroundColor Green

    $status = Invoke-RestMethod -Uri "$api/api/v1/system/status" -Method Get -TimeoutSec 15
    $requiredComponents = @('api', 'database', 'redis', 'analysis', 'worker', 'artifact-store')
    foreach ($componentKey in $requiredComponents) {
        $component = @($status.components | Where-Object { $_.key -eq $componentKey }) | Select-Object -First 1
        if ($null -eq $component) {
            throw "System Status omitted required component '$componentKey'."
        }
        if ($component.state -ne 'Healthy') {
            throw "System Status reports $componentKey as $($component.state): $($component.detail)"
        }
    }
    Write-Host ('{0,-26} PASS' -f 'System Status') -ForegroundColor Green

    $projectName = 'GUI Runtime Smoke ' + ([DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
    $body = @{
        name = $projectName
        repositoryUrl = 'https://github.com/example/dip-gui-runtime-smoke'
        defaultBranch = 'main'
        releaseDateUtc = $null
    } | ConvertTo-Json

    $created = Invoke-RestMethod -Uri "$api/api/projects" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 15
    if ([string]::IsNullOrWhiteSpace([string]$created.id)) {
        throw 'Project creation did not return an id.'
    }
    $projectId = [string]$created.id

    $projects = @(Invoke-RestMethod -Uri "$api/api/projects" -Method Get -TimeoutSec 15)
    if (-not ($projects | Where-Object { [string]$_.id -eq $projectId })) {
        throw 'Created project was not returned by the portfolio API.'
    }
    Write-Host ('{0,-26} PASS' -f 'Project create/list') -ForegroundColor Green

    $dashboard = Wait-Http 'Project Dashboard' "$web/dashboard/$projectId" $deadline
    if ($dashboard.Content -notmatch [Regex]::Escape($projectName)) {
        throw 'Project Dashboard did not render the created project.'
    }

    $qa = Wait-Http 'QA page' "$web/dashboard/$projectId/qa" $deadline
    if ($qa.Content -notmatch 'Autonomous Debug') {
        throw 'QA GUI did not expose the Autonomous Debug workflow.'
    }

    $management = Wait-Http 'Management page' "$web/dashboard/$projectId/management" $deadline
    if ($management.Content -notmatch 'Daily Cockpit' -or $management.Content -notmatch 'Sync & source of truth') {
        throw 'Management Operations GUI did not render its project-scoped navigation.'
    }

    $intelligence = Wait-Http 'Project Intelligence' "$web/dashboard/$projectId/intelligence" $deadline
    if ($intelligence.Content -notmatch 'Project Intelligence') {
        throw 'Project Intelligence GUI did not render.'
    }

    $governance = Wait-Http 'QA Governance' "$web/dashboard/$projectId/qa-governance" $deadline
    if ($governance.Content -notmatch 'QA Governance') {
        throw 'QA Governance GUI did not render.'
    }

    if ($RunPlaywright) {
        Write-Host 'STEP 5/5: Playwright browser E2E' -ForegroundColor Cyan
        Assert-Command 'npm'
        Assert-Command 'npx'
        $uiDir = Join-Path (Join-Path $root 'tests') 'Dip.UiE2E'
        Set-Location $uiDir

        if (Test-Path (Join-Path $uiDir 'package-lock.json')) {
            Invoke-NativeChecked -Executable 'npm' -Arguments @('ci', '--no-audit', '--no-fund') -LogFile $playwrightLog | Out-Null
        }
        else {
            Invoke-NativeChecked -Executable 'npm' -Arguments @('install', '--no-package-lock', '--no-audit', '--no-fund') -LogFile $playwrightLog | Out-Null
        }

        $runningOnWindows = $env:OS -eq 'Windows_NT'
        if ($runningOnWindows) {
            Invoke-NativeChecked -Executable 'npx' -Arguments @('playwright', 'install', 'chromium') -LogFile $playwrightLog | Out-Null
        }
        else {
            Invoke-NativeChecked -Executable 'npx' -Arguments @('playwright', 'install', '--with-deps', 'chromium') -LogFile $playwrightLog | Out-Null
        }

        $env:DIP_WEB_BASE_URL = $web
        $playwrightArgs = @('playwright', 'test', '--grep-invert', '@live-github')
        if ($Headed) {
            $playwrightArgs += '--headed'
        }
        Invoke-NativeChecked -Executable 'npx' -Arguments $playwrightArgs -LogFile $playwrightLog | Out-Null
        $playwrightPassed = $true
        Set-Location $root
    }
    else {
        Write-Host 'STEP 5/5: Playwright skipped (use -RunPlaywright to enable)' -ForegroundColor DarkGray
    }

    $success = $true
    $verifiedAt = [DateTimeOffset]::UtcNow
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $headShaLines = @(& git rev-parse HEAD 2>&1)
        $headShaExit = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }
    if ($headShaExit -eq 0) {
        $headSha = (($headShaLines | Out-String).Trim())
    }
    else {
        $headSha = 'unknown'
    }

    $payload = [ordered]@{
        schema = 'dip-gui-runtime-verification-v2'
        revision = $headSha
        verifiedAtUtc = $verifiedAt.ToString('o')
        startedAtUtc = $startedAt.ToString('o')
        isolatedComposeProject = $composeProject
        projectId = $projectId
        ports = $ports
        playwright = [bool]$RunPlaywright
        playwrightPassed = $playwrightPassed
        checks = @(
            'PostgreSQL readiness',
            'Redis readiness',
            'Python Analysis health',
            'API health',
            'Worker metrics',
            'Blazor application shell',
            'Create Project GUI',
            'System Status core components healthy',
            'Project create/list persistence',
            'Project Dashboard render',
            'QA Autonomous Debug GUI render',
            'Management Operations GUI render',
            'Project Intelligence render',
            'QA Governance render'
        )
    }
    $payload | ConvertTo-Json -Depth 8 | Set-Content -Path $resultFile -Encoding UTF8

    $playwrightStatus = if ($RunPlaywright) { 'PASS' } else { 'SKIPPED' }

    @(
        'GUI RUNTIME VERIFICATION PASS',
        "Revision=$headSha",
        "VerifiedAtUtc=$($verifiedAt.ToString('o'))",
        "ProjectId=$projectId",
        'Infrastructure=PASS',
        'Analysis=PASS',
        'API=PASS',
        'Worker=PASS',
        'Web=PASS',
        'SystemStatus=PASS',
        'ProjectCreate=PASS',
        'ProjectDashboard=PASS',
        'QaAutonomousDebugGui=PASS',
        'ManagementGui=PASS',
        "Playwright=$playwrightStatus"
    ) | Set-Content -Path $passFile -Encoding UTF8

    Write-Host ''
    Write-Host 'GUI RUNTIME VERIFICATION PASS' -ForegroundColor Green
    Write-Host "Evidence: $resultFile"
    if ($KeepOnSuccess) {
        Write-Host "GUI remains available at $web" -ForegroundColor Yellow
        Write-Host "Compose project: $composeProject" -ForegroundColor Yellow
    }
}
catch {
    Write-Host ''
    Write-Host ('GUI runtime verification failed: ' + $_.Exception.Message) -ForegroundColor Red
    Write-StackDiagnostics $composeProject
    throw
}
finally {
    Set-Location $previousLocation

    $keep = ($success -and $KeepOnSuccess) -or ((-not $success) -and $KeepOnFailure)
    if (-not $keep) {
        try {
            Invoke-Docker @('compose', '-p', $composeProject, 'down', '--volumes', '--remove-orphans') | Out-Null
        }
        catch {
            Write-Warning "Failed to fully stop isolated Compose project '$composeProject'."
        }
    }
    else {
        Write-Host "Isolated stack kept for inspection: $composeProject" -ForegroundColor Yellow
    }
}
