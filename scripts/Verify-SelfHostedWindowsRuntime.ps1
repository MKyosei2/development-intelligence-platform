# Purpose: Automates the V er if y-S el fH os te dW in do ws Ru nt im e repository workflow.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$art = Join-Path $root 'artifacts\self-hosted-fallback\runtime'
New-Item -ItemType Directory -Force -Path $art | Out-Null

function Test-TcpPort([string]$HostName,[int]$Port,[int]$TimeoutMs=1500) {
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

$hostName = if($env:POSTGRES_HOST){$env:POSTGRES_HOST}else{'127.0.0.1'}
$port = if($env:POSTGRES_PORT){[int]$env:POSTGRES_PORT}else{5432}
$db = if($env:POSTGRES_DB){$env:POSTGRES_DB}else{'dip_test'}
$user = if($env:POSTGRES_USER){$env:POSTGRES_USER}else{'dip'}
$password = if($env:POSTGRES_PASSWORD){$env:POSTGRES_PASSWORD}else{'dip-test-password'}
$container = if($env:DIP_POSTGRES_CONTAINER){$env:DIP_POSTGRES_CONTAINER}else{'dip-local-ci-postgres'}
$startedContainer = $false

if (-not (Test-TcpPort $hostName $port)) {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "PostgreSQL is unavailable and Docker is not installed."
    }

    docker info *> $null
    if ($LASTEXITCODE -ne 0) { throw "Docker Desktop is not running." }

    $existing = (& docker ps -a --filter "name=^/${container}$" --format "{{.Names}}" | Out-String).Trim()
    if ($existing -eq $container) {
        docker start $container *> $null
    }
    else {
        docker run --name $container -e "POSTGRES_DB=$db" -e "POSTGRES_USER=$user" `
            -e "POSTGRES_PASSWORD=$password" -p "${port}:5432" -d postgres:16 *> $null
        $startedContainer = $true
    }

    for($i=0;$i -lt 90;$i++){
        if(Test-TcpPort $hostName $port){break}
        Start-Sleep -Seconds 1
    }

    if (-not (Test-TcpPort $hostName $port)) { throw "PostgreSQL did not become ready." }
}

$env:ConnectionStrings__PostgreSql = "Host=$hostName;Port=$port;Database=$db;Username=$user;Password=$password"
$env:ASPNETCORE_ENVIRONMENT = 'Testing'
$env:DOTNET_ENVIRONMENT = 'Testing'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:7001'
$env:Authentication__RequireAuthenticatedUser = 'false'
$env:GitHub__WebhookSecret = 'integration-test-secret'
$env:GitHub__Token = 'integration-test-token'
$env:DIP_API_URL = 'http://127.0.0.1:7001'
$env:Qa__WorkspaceRoot = $root
$env:Qa__ArtifactRoot = Join-Path $root 'artifacts\qa'
$env:Artifacts__Provider = 'Local'
$env:Artifacts__LocalRoot = Join-Path $root 'artifacts\durable'

Push-Location $root
$api = $null
$worker = $null

try {
    dotnet test tests/Dip.IntegrationTests/Dip.IntegrationTests.csproj -c Release --no-build `
        --logger "trx;LogFileName=self-hosted-integration.trx" --results-directory TestResults
    if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed.' }

    $apiOut = Join-Path $art 'api.stdout.log'
    $apiErr = Join-Path $art 'api.stderr.log'
    $workerOut = Join-Path $art 'worker.stdout.log'
    $workerErr = Join-Path $art 'worker.stderr.log'

    $api = Start-Process -FilePath 'dotnet' -ArgumentList @(
        'run','--no-launch-profile','--project','src/Dip.Api/Dip.Api.csproj',
        '-c','Release','--no-build','--no-restore'
    ) -PassThru -RedirectStandardOutput $apiOut -RedirectStandardError $apiErr

    $worker = Start-Process -FilePath 'dotnet' -ArgumentList @(
        'run','--no-launch-profile','--project','src/Dip.Worker/Dip.Worker.csproj',
        '-c','Release','--no-build','--no-restore'
    ) -PassThru -RedirectStandardOutput $workerOut -RedirectStandardError $workerErr

    $healthy = $false
    for ($i=0; $i -lt 90; $i++) {
        if ($api.HasExited) { throw "API exited early. See $apiErr" }
        try {
            $r = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:7001/health' -TimeoutSec 3
            if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300) {
                $healthy = $true
                break
            }
        }
        catch {}
        Start-Sleep -Seconds 2
    }

    if (-not $healthy) { throw 'API did not become healthy within the fallback timeout.' }

    dotnet test tests/Dip.EndToEndTests/Dip.EndToEndTests.csproj -c Release --no-build `
        --logger "trx;LogFileName=self-hosted-e2e.trx" --results-directory TestResults
    if ($LASTEXITCODE -ne 0) { throw 'End-to-end tests failed.' }

    Write-Host 'PASS: Windows PostgreSQL/API/Worker/E2E verification.' -ForegroundColor Green
}
finally {
    if ($api -and -not $api.HasExited) { Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue }
    if ($worker -and -not $worker.HasExited) { Stop-Process -Id $worker.Id -Force -ErrorAction SilentlyContinue }
    Pop-Location
    if ($startedContainer) { docker rm -f $container *> $null }
}
