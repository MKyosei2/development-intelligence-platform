# Purpose: Automates the DIP workflow represented by S et D IP P ro du ct io nB ri dg eE nv ir on me nt.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][Guid]$ProjectId,
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$PipeName = '',
    [string]$BridgeToken = '',
    [string]$Executable = '',
    [string[]]$ArgumentList = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$resolvedRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
if (-not $PipeName) { $PipeName = 'dip-production-' + $ProjectId.ToString('N') }

$env:DIP_AGENT_PROJECT_ID = $ProjectId.ToString()
$env:DIP_AGENT_PROJECT_ROOT = $resolvedRoot
$env:DIP_AGENT_PIPE_NAME = $PipeName
if ($BridgeToken) { $env:DIP_AGENT_BRIDGE_TOKEN = $BridgeToken }

Write-Host '[PASS] DIP production bridge environment configured for this PowerShell process.' -ForegroundColor Green
Write-Host "Project root: $resolvedRoot"
Write-Host "Pipe:         $PipeName"
Write-Host 'Bridge token value is intentionally not printed.' -ForegroundColor DarkGray

if ($Executable) {
    $exe = (Resolve-Path -LiteralPath $Executable).Path
    Write-Host "Starting: $exe" -ForegroundColor Cyan
    Start-Process -FilePath $exe -ArgumentList $ArgumentList -WorkingDirectory $resolvedRoot
}
else {
    Write-Host 'Launch Maya / Unity / Unreal from this PowerShell process so it inherits these variables.' -ForegroundColor Yellow
}
