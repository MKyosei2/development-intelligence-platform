[CmdletBinding()]
param([switch]$PurgeData)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$runtime=Join-Path $root 'artifacts\runtime'
$envFile=Join-Path $runtime 'dip.runtime.env'
if(-not(Get-Command docker -ErrorAction SilentlyContinue)){throw 'docker is required.'}
$args=@('compose')
if(Test-Path $envFile){$args+=@('--env-file',$envFile)}
$args+=@('down','--remove-orphans')
if($PurgeData){$args+='--volumes'}
& docker @args
if($LASTEXITCODE-ne0){exit $LASTEXITCODE}
if($PurgeData){Write-Host 'DIP stopped and persistent Docker volumes were removed.' -ForegroundColor Yellow}else{Write-Host 'DIP stopped. Persistent data volumes were preserved.' -ForegroundColor Green}
