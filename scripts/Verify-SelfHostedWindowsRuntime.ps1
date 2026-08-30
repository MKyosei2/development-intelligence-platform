$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$art = Join-Path $root 'artifacts\self-hosted-fallback\runtime'
New-Item -ItemType Directory -Force -Path $art | Out-Null

& (Join-Path $PSScriptRoot 'Initialize-SelfHostedPostgres.ps1')

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
        'run','--no-launch-profile',
        '--project','src/Dip.Api/Dip.Api.csproj',
        '-c','Release','--no-build','--no-restore'
    ) -PassThru -RedirectStandardOutput $apiOut -RedirectStandardError $apiErr

    $worker = Start-Process -FilePath 'dotnet' -ArgumentList @(
        'run','--no-launch-profile',
        '--project','src/Dip.Worker/Dip.Worker.csproj',
        '-c','Release','--no-build','--no-restore'
    ) -PassThru -RedirectStandardOutput $workerOut -RedirectStandardError $workerErr

    $healthy = $false
    for ($i=0; $i -lt 90; $i++) {
        if ($api.HasExited) {
            throw "API exited early. See $apiErr"
        }

        try {
            $r = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:7001/health' -TimeoutSec 3
            if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300) {
                $healthy = $true
                break
            }
        }
        catch { }

        Start-Sleep -Seconds 2
    }

    if (-not $healthy) { throw 'API did not become healthy within the fallback timeout.' }

    dotnet test tests/Dip.EndToEndTests/Dip.EndToEndTests.csproj -c Release --no-build `
        --logger "trx;LogFileName=self-hosted-e2e.trx" --results-directory TestResults
    if ($LASTEXITCODE -ne 0) { throw 'End-to-end tests failed.' }

    Write-Host 'PASS: Windows native PostgreSQL/API/Worker/E2E verification.'
}
finally {
    if ($api -and -not $api.HasExited) { Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue }
    if ($worker -and -not $worker.HasExited) { Stop-Process -Id $worker.Id -Force -ErrorAction SilentlyContinue }
    Pop-Location
}
