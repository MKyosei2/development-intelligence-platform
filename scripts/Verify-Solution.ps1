[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [switch]$KeepServicesRunning,
    [int]$PostgreSqlPort=55432,
    [int]$ApiPort=55080
)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out=Join-Path $root 'artifacts\final-verification';$completion=Join-Path $out 'PROJECT_COMPLETION.txt';$failure=Join-Path $out 'PROJECT_COMPLETION_FAILED.txt'
New-Item -ItemType Directory -Force -Path $out|Out-Null;Remove-Item $completion,$failure -Force -ErrorAction SilentlyContinue
function Invoke-Checked([string]$exe,[string[]]$args){& $exe @args;if($LASTEXITCODE -ne 0){throw "$exe $($args -join ' ') failed with exit code $LASTEXITCODE"}}
function Wait-Http([string]$uri,[System.Diagnostics.Process]$process){$deadline=[DateTimeOffset]::UtcNow.AddSeconds(120);while([DateTimeOffset]::UtcNow -lt $deadline){if($process.HasExited){throw "API exited before health check"};try{$r=Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 3;if($r.StatusCode -lt 400){return}}catch{};Start-Sleep -Milliseconds 500};throw "Timed out waiting for $uri"}
$api=$null;$worker=$null;$composeStarted=$false
Push-Location $root
try{
 foreach($cmd in @('dotnet','docker','git')){if(-not(Get-Command $cmd -ErrorAction SilentlyContinue)){throw "Required command '$cmd' was not found in PATH."}}
 $sdk=(& dotnet --version|Out-String).Trim();if(-not $sdk.StartsWith('8.')){throw ".NET SDK 8.x is required; active SDK is '$sdk'."}
 Invoke-Checked 'dotnet' @('restore','DevelopmentIntelligencePlatform.sln')
 Invoke-Checked 'dotnet' @('build','DevelopmentIntelligencePlatform.sln','-c',$Configuration,'--no-restore')
 foreach($project in @('tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj','tests/Dip.Application.Tests/Dip.Application.Tests.csproj','tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj')){Invoke-Checked 'dotnet' @('test',$project,'-c',$Configuration,'--no-build')}
 $env:POSTGRES_DB='dip_verification';$env:POSTGRES_USER='dip';$env:POSTGRES_PASSWORD='dip-verification-password';$env:POSTGRES_PORT="$PostgreSqlPort"
 $env:ConnectionStrings__PostgreSql="Host=127.0.0.1;Port=$PostgreSqlPort;Database=dip_verification;Username=dip;Password=dip-verification-password";$env:GitHub__WebhookSecret='integration-test-secret';$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development'
 Invoke-Checked 'docker' @('compose','--project-name','dip-final-verification','up','-d','postgres');$composeStarted=$true
 Start-Sleep -Seconds 5
 Invoke-Checked 'dotnet' @('test','tests/Dip.IntegrationTests/Dip.IntegrationTests.csproj','-c',$Configuration,'--no-build')
 $apiOut=Join-Path $out 'api.stdout.log';$apiErr=Join-Path $out 'api.stderr.log';$workerOut=Join-Path $out 'worker.stdout.log';$workerErr=Join-Path $out 'worker.stderr.log'
 $api=Start-Process dotnet -ArgumentList @('run','--project','src/Dip.Api/Dip.Api.csproj','-c',$Configuration,'--no-build','--urls',"http://127.0.0.1:$ApiPort") -RedirectStandardOutput $apiOut -RedirectStandardError $apiErr -PassThru
 $worker=Start-Process dotnet -ArgumentList @('run','--project','src/Dip.Worker/Dip.Worker.csproj','-c',$Configuration,'--no-build') -RedirectStandardOutput $workerOut -RedirectStandardError $workerErr -PassThru
 Wait-Http "http://127.0.0.1:$ApiPort/health" $api
 $env:DIP_API_URL="http://127.0.0.1:$ApiPort";Invoke-Checked 'dotnet' @('test','tests/Dip.EndToEndTests/Dip.EndToEndTests.csproj','-c',$Configuration,'--no-build')
 @('PROJECT COMPLETION VERIFIED',"Timestamp=$([DateTimeOffset]::UtcNow.ToString('u'))","Configuration=$Configuration",'UnitTests=PASS','IntegrationTests=PASS','LiveE2E=PASS')|Set-Content $completion -Encoding UTF8
 Write-Host "Full solution verification PASS: $completion" -ForegroundColor Green
}catch{$_|Out-String|Set-Content $failure -Encoding UTF8;throw}
finally{
 foreach($p in @($worker,$api)){if($null-ne$p){try{if(-not$p.HasExited){Stop-Process -Id $p.Id -Force}}catch{}}}
 if($composeStarted -and -not $KeepServicesRunning){& docker compose --project-name dip-final-verification down --volumes --remove-orphans *> $null}
 Pop-Location
}
