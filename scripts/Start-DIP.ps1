# Purpose: Automates the S ta rt-D IP repository workflow.
[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$VerifyGui,
    [switch]$NoBrowser,
    [switch]$NoLogRedirect,
    [int]$HealthTimeoutSeconds = 420,
    [string]$PostgresHost = $(if ($env:POSTGRES_HOST) { $env:POSTGRES_HOST } else { '127.0.0.1' }),
    [int]$PostgresPort = $(if ($env:POSTGRES_PORT) { [int]$env:POSTGRES_PORT } else { 5432 }),
    [string]$PostgresDatabase = $(if ($env:POSTGRES_DB) { $env:POSTGRES_DB } else { 'dip' }),
    [string]$PostgresUser = $(if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'dip' }),
    [string]$PostgresPassword = $(if ($env:POSTGRES_PASSWORD) { $env:POSTGRES_PASSWORD } else { 'change-me-local-only' })
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$runtime = Join-Path $root '.dip\runtime'
$logs = Join-Path $runtime 'logs'
$venv = Join-Path $root '.dip\python-venv'
$artifacts = Join-Path $root 'artifacts\durable'
$workspaces = Join-Path $root '.dip\workspaces'

New-Item -Force -ItemType Directory $runtime,$logs,$artifacts,$workspaces | Out-Null

function Assert-Command([string]$name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Required command '$name' was not found in PATH."
    }
}

function Test-TcpPort([string]$HostName,[int]$Port,[int]$TimeoutMs = 1500) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect($HostName,$Port,$null,$null)
        if (-not $async.AsyncWaitHandle.WaitOne($TimeoutMs,$false)) { return $false }
        $client.EndConnect($async)
        return $true
    }
    catch { return $false }
    finally { $client.Close() }
}

function Invoke-NativeQuiet([string]$Exe,[string[]]$Arguments) {
    $oldPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $Exe @Arguments *> $null
        return $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldPreference
    }
}

function Ensure-DockerEngine([string]$DockerExe) {
    if ((Invoke-NativeQuiet $DockerExe @('info')) -eq 0) { return }

    $desktop = 'C:\Program Files\Docker\Docker\Docker Desktop.exe'
    if (-not (Test-Path -LiteralPath $desktop)) {
        throw 'Docker Engine is not running and Docker Desktop was not found.'
    }

    if ($null -eq (Get-Process -Name 'Docker Desktop' -ErrorAction SilentlyContinue)) {
        Start-Process -FilePath $desktop | Out-Null
    }

    for ($i = 0; $i -lt 180; $i++) {
        Start-Sleep -Seconds 1
        if ((Invoke-NativeQuiet $DockerExe @('info')) -eq 0) { return }
    }

    throw 'Docker Engine did not become available.'
}

function Ensure-Postgres {
    if (Test-TcpPort $PostgresHost $PostgresPort) {
        Write-Host "[PASS] PostgreSQL TCP ${PostgresHost}:${PostgresPort}" -ForegroundColor Green
        return
    }

    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if (-not $docker) {
        $fixed = 'C:\Program Files\Docker\Docker\resources\bin\docker.exe'
        if (Test-Path -LiteralPath $fixed) { $docker = Get-Item -LiteralPath $fixed }
    }
    if (-not $docker) {
        throw "PostgreSQL is not reachable at ${PostgresHost}:${PostgresPort} and Docker is unavailable."
    }

    $dockerExe = $docker.Source
    Ensure-DockerEngine $dockerExe

    $container = 'dip-local-postgres'
    $oldPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $existing = (& $dockerExe ps -a --filter "name=^/${container}$" --format "{{.Names}}" 2>$null | Out-String).Trim()
        if ($existing -eq $container) {
            & $dockerExe start $container *> $null
            if ($LASTEXITCODE -ne 0) { throw "Existing PostgreSQL container '$container' could not be started." }
        }
        else {
            & $dockerExe run --name $container `
                -e "POSTGRES_DB=$PostgresDatabase" `
                -e "POSTGRES_USER=$PostgresUser" `
                -e "POSTGRES_PASSWORD=$PostgresPassword" `
                -p "${PostgresPort}:5432" `
                -d postgres:16-alpine *> $null
            if ($LASTEXITCODE -ne 0) { throw "Docker PostgreSQL container '$container' could not be created." }
        }
    }
    finally {
        $ErrorActionPreference = $oldPreference
    }

    for ($i = 0; $i -lt 90; $i++) {
        if (Test-TcpPort $PostgresHost $PostgresPort) {
            Write-Host "[PASS] PostgreSQL TCP ${PostgresHost}:${PostgresPort}" -ForegroundColor Green
            return
        }
        Start-Sleep -Seconds 1
    }

    throw "PostgreSQL did not become reachable at ${PostgresHost}:${PostgresPort}."
}

function Wait-Http([string]$name,[string]$url,[datetime]$deadline) {
    do {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec 4
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Write-Host "[PASS] $name $url" -ForegroundColor Green
                return
            }
        }
        catch {}
        Start-Sleep -Milliseconds 750
    } while ((Get-Date) -lt $deadline)

    throw "$name did not become healthy: $url"
}

function Start-Tracked([string]$name,[string]$exe,[string[]]$arguments,[string]$cwd) {
    if ($null -eq $arguments -or $arguments.Count -eq 0) {
        throw "No arguments were supplied for process '$name'."
    }

    $pidFile = Join-Path $runtime "$name.pid"
    if (Test-Path $pidFile) {
        $oldPid = [int](Get-Content $pidFile -Raw)
        if (Get-Process -Id $oldPid -ErrorAction SilentlyContinue) {
            throw "$name is already running (PID $oldPid). Run scripts/Stop-DIP.ps1 first."
        }
        Remove-Item $pidFile -Force
    }

    $stdout = Join-Path $logs "$name.out.log"
    $stderr = Join-Path $logs "$name.err.log"
    Remove-Item $stdout,$stderr -Force -ErrorAction SilentlyContinue

    $startProcess = @{
        FilePath = $exe
        ArgumentList = $arguments
        WorkingDirectory = $cwd
        PassThru = $true
    }

    if (-not $NoLogRedirect) {
        $startProcess.RedirectStandardOutput = $stdout
        $startProcess.RedirectStandardError = $stderr
    }

    $p = Start-Process @startProcess

    $p.Id | Set-Content $pidFile -Encoding ASCII
    Write-Host "Started $name (PID $($p.Id))."
    return $p
}

function Show-Logs {
    Get-ChildItem $logs -Filter '*.log' -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "===== $($_.Name) =====" -ForegroundColor Yellow
        Get-Content $_.FullName -Tail 120 -ErrorAction SilentlyContinue
    }
}

try {
    Assert-Command dotnet
    Assert-Command git
    Ensure-Postgres

    $python = Get-Command python -ErrorAction SilentlyContinue
    if (-not $python) { $python = Get-Command python3 -ErrorAction SilentlyContinue }
    if (-not $python) { throw 'Python 3 was not found.' }

    $connection = "Host=$PostgresHost;Port=$PostgresPort;Database=$PostgresDatabase;Username=$PostgresUser;Password=$PostgresPassword"
    $env:ConnectionStrings__PostgreSql = $connection
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:Authentication__RequireAuthenticatedUser = 'false'
    $env:Qa__WorkspaceRoot = $workspaces
    $env:Qa__ArtifactRoot = Join-Path $root 'artifacts\qa'
    $env:Qa__SandboxEnabled = 'true'
    $env:Artifacts__Provider = 'Local'
    $env:Artifacts__LocalRoot = $artifacts
    $env:AnalysisService__BaseUrl = 'http://127.0.0.1:8100'
    $env:SystemStatus__WorkerMetricsUrl = 'http://127.0.0.1:9091/metrics'
    $env:OperationalMetrics__Port = '9091'
    $env:Api__BaseUrl = 'http://127.0.0.1:5080/'
    $env:Ui__PublicWebUrl = 'http://127.0.0.1:5081'
    $env:Ui__PublicApiUrl = 'http://127.0.0.1:5080'

    if (-not (Test-Path (Join-Path $venv 'pyvenv.cfg'))) {
        & $python.Source -m venv $venv
        if ($LASTEXITCODE -ne 0) { throw 'Python venv creation failed.' }
    }

    $venvPython = if ($env:OS -eq 'Windows_NT') {
        Join-Path $venv 'Scripts\python.exe'
    }
    else {
        Join-Path $venv 'bin/python'
    }

    & $venvPython -m pip install --disable-pip-version-check -r (Join-Path $root 'services\python-analysis\requirements.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Python analysis dependencies failed to install.' }

    if (-not $NoBuild) {
        Push-Location $root
        try {
            & dotnet restore DevelopmentIntelligencePlatform.sln
            if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

            & dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
            if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
        }
        finally { Pop-Location }
    }

    $analysisKey = if ($env:DIP_ANALYSIS_API_KEY) { $env:DIP_ANALYSIS_API_KEY } else { 'native-development-analysis-key' }
    $env:API_KEY = $analysisKey
    $env:AnalysisService__ApiKey = $analysisKey

    $analysis = Start-Tracked 'analysis' $venvPython @(
        '-m','uvicorn','app.main:app','--host','127.0.0.1','--port','8100'
    ) (Join-Path $root 'services\python-analysis')

    $api = Start-Tracked 'api' 'dotnet' @(
        'run','--no-launch-profile','--project',
        (Join-Path $root 'src\Dip.Api\Dip.Api.csproj'),
        '-c','Release','--no-build','--no-restore',
        '--urls','http://127.0.0.1:5080'
    ) $root

    $worker = Start-Tracked 'worker' 'dotnet' @(
        'run','--no-launch-profile','--project',
        (Join-Path $root 'src\Dip.Worker\Dip.Worker.csproj'),
        '-c','Release','--no-build','--no-restore'
    ) $root

    $web = Start-Tracked 'web' 'dotnet' @(
        'run','--no-launch-profile','--project',
        (Join-Path $root 'src\Dip.Web\Dip.Web.csproj'),
        '-c','Release','--no-build','--no-restore',
        '--urls','http://127.0.0.1:5081'
    ) $root

    $deadline = (Get-Date).AddSeconds($HealthTimeoutSeconds)
    Wait-Http 'Python Analysis' 'http://127.0.0.1:8100/health' $deadline
    Wait-Http 'API' 'http://127.0.0.1:5080/health' $deadline
    Wait-Http 'Worker' 'http://127.0.0.1:9091/metrics' $deadline
    Wait-Http 'Web' 'http://127.0.0.1:5081/' $deadline

    $ready = Invoke-RestMethod 'http://127.0.0.1:5080/api/v1/system/ready'
    if (-not $ready.ready) {
        throw "DIP readiness failed: $($ready | ConvertTo-Json -Compress)"
    }
    Write-Host '[PASS] API readiness' -ForegroundColor Green

    if ($VerifyGui) {
        Assert-Command npm
        Push-Location (Join-Path $root 'tests\Dip.UiE2E')
        try {
            if (Test-Path package-lock.json) { & npm ci } else { & npm install }
            if ($LASTEXITCODE -ne 0) { throw 'npm install failed.' }

            & npx playwright install chromium
            if ($LASTEXITCODE -ne 0) { throw 'Playwright install failed.' }

            $env:DIP_WEB_BASE_URL = 'http://127.0.0.1:5081'
            & npm test
            if ($LASTEXITCODE -ne 0) { throw 'GUI E2E failed.' }
        }
        finally { Pop-Location }
        Write-Host '[PASS] GUI E2E' -ForegroundColor Green
    }

    Write-Host 'DIP native runtime: READY' -ForegroundColor Green
    Write-Host 'Web: http://127.0.0.1:5081/'

    if (-not $NoBrowser) {
        Start-Process 'http://127.0.0.1:5081/'
    }
}
catch {
    Write-Host "DIP startup FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Show-Logs
    try { & (Join-Path $PSScriptRoot 'Stop-DIP.ps1') -KeepData } catch {}
    exit 1
}
