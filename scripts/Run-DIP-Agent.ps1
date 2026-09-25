# Purpose: Automates Run DIP Agent for repository development and verification workflows.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][Guid]$ProjectId,
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$ApiUrl = 'http://127.0.0.1:5080/',
    [string]$AgentId = $env:COMPUTERNAME,
    [string]$RootId = 'project-root',
    [string]$BearerToken = '',
    [string]$PipeName = '',
    [string]$BridgeToken = '',
    [long]$MaxMediaMb = 1024
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

$env:DIP_AGENT_API_URL = $ApiUrl
$env:DIP_AGENT_PROJECT_ID = $ProjectId.ToString()
$env:DIP_AGENT_PROJECT_ROOT = (Resolve-Path $ProjectRoot).Path
$env:DIP_AGENT_ID = $AgentId
$env:DIP_AGENT_ROOT_ID = $RootId
$env:DIP_AGENT_POLL_SECONDS = '30'
$env:DIP_AGENT_MAX_MEDIA_MB = [string]$MaxMediaMb
if ($BearerToken) { $env:DIP_AGENT_BEARER_TOKEN = $BearerToken }
if ($PipeName) { $env:DIP_AGENT_PIPE_NAME = $PipeName }
if ($BridgeToken) { $env:DIP_AGENT_BRIDGE_TOKEN = $BridgeToken }

Write-Host 'Starting DIP Project Agent. Press Ctrl+C to stop.' -ForegroundColor Cyan
Write-Host "Project: $ProjectId"
Write-Host "Root:    $env:DIP_AGENT_PROJECT_ROOT"
Write-Host "API:     $ApiUrl"
Write-Host "Pipe:    $(if($env:DIP_AGENT_PIPE_NAME){$env:DIP_AGENT_PIPE_NAME}else{'auto: dip-production-' + $ProjectId.ToString('N')})"
Write-Host 'Bridge token value is intentionally not printed.' -ForegroundColor DarkGray

dotnet run --project (Join-Path $repo 'src\Dip.Agent.Windows\Dip.Agent.Windows.csproj') -c Release
exit $LASTEXITCODE
