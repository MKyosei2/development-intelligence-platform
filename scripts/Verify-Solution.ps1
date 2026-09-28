# Purpose: Runs Verify Solution as part of repository development and verification workflows.
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [switch]$KeepServicesRunning,
    [string]$PostgreSqlHost=$(if($env:POSTGRES_HOST){$env:POSTGRES_HOST}else{'127.0.0.1'}),
    [int]$PostgreSqlPort=$(if($env:POSTGRES_PORT){[int]$env:POSTGRES_PORT}else{5432}),
    [string]$PostgreSqlUser=$(if($env:POSTGRES_USER){$env:POSTGRES_USER}else{'dip'}),
    [string]$PostgreSqlPassword=$(if($env:POSTGRES_PASSWORD){$env:POSTGRES_PASSWORD}else{'change-me-local-only'}),
    [int]$ApiPort=55080
)

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'PostgreSqlRuntime.ps1')
$pg=Resolve-DipPostgresClient `
    -HostName $PostgreSqlHost `
    -Port $PostgreSqlPort `
    -User $PostgreSqlUser `
    -Password $PostgreSqlPassword
$out=Join-Path $root 'artifacts\final-verification'
$completion=Join-Path $out 'PROJECT_COMPLETION.txt'
$failure=Join-Path $out 'PROJECT_COMPLETION_FAILED.txt'
New-Item -ItemType Directory -Force -Path $out|Out-Null
Remove-Item $completion,$failure -Force -ErrorAction SilentlyContinue

function Invoke-Checked([string]$exe,[string[]]$Arguments){
    & $exe @Arguments
    if($LASTEXITCODE -ne 0){throw "$exe $($Arguments -join ' ') failed with exit code $LASTEXITCODE"}
}

function Wait-Http([string]$uri,[System.Diagnostics.Process]$process){
    $deadline=[DateTimeOffset]::UtcNow.AddSeconds(120)
    while([DateTimeOffset]::UtcNow -lt $deadline){
        if($process.HasExited){throw 'API exited before health check.'}
        try{
            $r=Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 3
            if($r.StatusCode -lt 400){return}
        }catch{}
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for $uri"
}

function Invoke-Psql([string]$Database,[string]$Sql,[switch]$TuplesOnly){
    Invoke-DipPsql `
        -Client $pg `
        -Database $Database `
        -Sql $Sql `
        -TuplesOnly:$TuplesOnly
}

foreach($cmd in @('dotnet','git')){
    if(-not(Get-Command $cmd -ErrorAction SilentlyContinue)){throw "Required command '$cmd' was not found in PATH."}
}
$sdk=(& dotnet --version|Out-String).Trim()
if(-not $sdk.StartsWith('8.')){throw ".NET SDK 8.x is required; active SDK is '$sdk'."}

$database='dip_verification_'+[guid]::NewGuid().ToString('N').Substring(0,12)
$api=$null
$worker=$null
$databaseCreated=$false
$oldEnv=@{
    POSTGRES_DB=$env:POSTGRES_DB
    POSTGRES_USER=$env:POSTGRES_USER
    POSTGRES_PASSWORD=$env:POSTGRES_PASSWORD
    POSTGRES_HOST=$env:POSTGRES_HOST
    POSTGRES_PORT=$env:POSTGRES_PORT
    ConnectionStrings__PostgreSql=$env:ConnectionStrings__PostgreSql
    GitHub__WebhookSecret=$env:GitHub__WebhookSecret
    ASPNETCORE_ENVIRONMENT=$env:ASPNETCORE_ENVIRONMENT
    DOTNET_ENVIRONMENT=$env:DOTNET_ENVIRONMENT
    Authentication__RequireAuthenticatedUser=$env:Authentication__RequireAuthenticatedUser
    DIP_API_URL=$env:DIP_API_URL
    Qa__WorkspaceRoot=$env:Qa__WorkspaceRoot
    Qa__ArtifactRoot=$env:Qa__ArtifactRoot
    Artifacts__Provider=$env:Artifacts__Provider
    Artifacts__LocalRoot=$env:Artifacts__LocalRoot
}

Push-Location $root
try{
    $probe=(Invoke-Psql 'postgres' 'SELECT 1;' -TuplesOnly | Out-String).Trim()
    if($probe-ne'1'){throw "PostgreSQL is not reachable at ${PostgreSqlHost}:${PostgreSqlPort}."}

    Invoke-Psql 'postgres' "CREATE DATABASE `"$database`" OWNER `"$PostgreSqlUser`";" | Out-Null
    $databaseCreated=$true

    $env:POSTGRES_DB=$database
    $env:POSTGRES_USER=$PostgreSqlUser
    $env:POSTGRES_PASSWORD=$PostgreSqlPassword
    $env:POSTGRES_HOST=$PostgreSqlHost
    $env:POSTGRES_PORT=[string]$PostgreSqlPort
    $env:ConnectionStrings__PostgreSql="Host=$PostgreSqlHost;Port=$PostgreSqlPort;Database=$database;Username=$PostgreSqlUser;Password=$PostgreSqlPassword"
    $env:GitHub__WebhookSecret='integration-test-secret'
    $env:ASPNETCORE_ENVIRONMENT='Testing'
    $env:DOTNET_ENVIRONMENT='Testing'
    $env:Authentication__RequireAuthenticatedUser='false'
    $env:Qa__WorkspaceRoot=$root
    $env:Qa__ArtifactRoot=Join-Path $root 'artifacts\qa'
    $env:Artifacts__Provider='Local'
    $env:Artifacts__LocalRoot=Join-Path $root 'artifacts\durable'

    Invoke-Checked 'dotnet' @('restore','DevelopmentIntelligencePlatform.sln')
    Invoke-Checked 'dotnet' @('build','DevelopmentIntelligencePlatform.sln','-c',$Configuration,'--no-restore')
    foreach($project in @(
        'tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj',
        'tests/Dip.Application.Tests/Dip.Application.Tests.csproj',
        'tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj'
    )){
        Invoke-Checked 'dotnet' @('test',$project,'-c',$Configuration,'--no-build')
    }

    Invoke-Checked 'dotnet' @('test','tests/Dip.IntegrationTests/Dip.IntegrationTests.csproj','-c',$Configuration,'--no-build')

    $apiOut=Join-Path $out 'api.stdout.log'
    $apiErr=Join-Path $out 'api.stderr.log'
    $workerOut=Join-Path $out 'worker.stdout.log'
    $workerErr=Join-Path $out 'worker.stderr.log'
    $apiDll=Join-Path $root "src\Dip.Api\bin\$Configuration\net8.0\Dip.Api.dll"
    $workerDll=Join-Path $root "src\Dip.Worker\bin\$Configuration\net8.0\Dip.Worker.dll"
    if(-not(Test-Path -LiteralPath $apiDll -PathType Leaf)){throw "API DLL was not found: $apiDll"}
    if(-not(Test-Path -LiteralPath $workerDll -PathType Leaf)){throw "Worker DLL was not found: $workerDll"}

    # Launch managed DLLs through the trusted dotnet host rather than generated apphost EXEs.
    $api=Start-Process dotnet -ArgumentList @(
        $apiDll,'--urls',"http://127.0.0.1:$ApiPort"
    ) -RedirectStandardOutput $apiOut -RedirectStandardError $apiErr -PassThru
    $worker=Start-Process dotnet -ArgumentList @(
        $workerDll
    ) -RedirectStandardOutput $workerOut -RedirectStandardError $workerErr -PassThru

    Wait-Http "http://127.0.0.1:$ApiPort/health" $api
    $env:DIP_API_URL="http://127.0.0.1:$ApiPort"
    Invoke-Checked 'dotnet' @('test','tests/Dip.EndToEndTests/Dip.EndToEndTests.csproj','-c',$Configuration,'--no-build')

    @(
        'PROJECT COMPLETION VERIFIED',
        "Timestamp=$([DateTimeOffset]::UtcNow.ToString('u'))",
        "Configuration=$Configuration",
        'Runtime=NativePostgreSQL',
        'RuntimeLaunch=dotnet-managed-dll',
        'UnitTests=PASS',
        'IntegrationTests=PASS',
        'LiveE2E=PASS'
    )|Set-Content $completion -Encoding UTF8
    Write-Host "Full solution verification PASS: $completion" -ForegroundColor Green
}catch{
    $_|Out-String|Set-Content $failure -Encoding UTF8
    throw
}finally{
    foreach($p in @($worker,$api)){
        if($null-ne$p){
            try{if(-not$p.HasExited){Stop-Process -Id $p.Id -Force}}catch{}
        }
    }

    if($databaseCreated -and -not $KeepServicesRunning){
        try{Invoke-Psql 'postgres' "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$database';" | Out-Null}catch{}
        try{Invoke-Psql 'postgres' "DROP DATABASE IF EXISTS `"$database`";" | Out-Null}catch{}
    }

    foreach($entry in $oldEnv.GetEnumerator()){
        Set-Item -Path ("Env:"+$entry.Key) -Value $entry.Value -ErrorAction SilentlyContinue
        if($null-eq$entry.Value){Remove-Item -Path ("Env:"+$entry.Key) -ErrorAction SilentlyContinue}
    }
    Pop-Location
}
