[CmdletBinding(SupportsShouldProcess=$true,ConfirmImpact='High')]
param([switch]$KeepData,[switch]$PurgeRuntimeData)
Set-StrictMode -Version Latest;$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path;$runtime=Join-Path $root '.dip\runtime'
foreach($name in @('web','worker','api','analysis')){$pidFile=Join-Path $runtime "$name.pid";if(-not(Test-Path $pidFile)){continue};$pidValue=[int](Get-Content $pidFile -Raw);$p=Get-Process -Id $pidValue -ErrorAction SilentlyContinue;if($p){Write-Host "Stopping $name (PID $pidValue)...";Stop-Process -Id $pidValue -ErrorAction SilentlyContinue;try{Wait-Process -Id $pidValue -Timeout 10 -ErrorAction Stop}catch{Stop-Process -Id $pidValue -Force -ErrorAction SilentlyContinue}};Remove-Item $pidFile -Force -ErrorAction SilentlyContinue}
Write-Host 'DIP processes stopped. PostgreSQL was intentionally left running.' -ForegroundColor Green
if($PurgeRuntimeData -and -not$KeepData){if($PSCmdlet.ShouldProcess($root,'Delete DIP workspaces, durable artifacts and runtime state')){Remove-Item (Join-Path $root '.dip\workspaces'),(Join-Path $root 'artifacts\durable') -Recurse -Force -ErrorAction SilentlyContinue;Write-Warning 'DIP runtime data was deleted. PostgreSQL data was not touched.'}}
