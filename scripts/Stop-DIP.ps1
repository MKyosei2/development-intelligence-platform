param(
    [switch]$RemoveData
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location -LiteralPath $RepoRoot

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "Docker is not installed. Nothing to stop." -ForegroundColor Yellow
    exit 0
}

& docker info 1>$null 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker Engine is not running. Nothing to stop." -ForegroundColor Yellow
    exit 0
}

Write-Host "Stopping Development Intelligence Platform..." -ForegroundColor Cyan

if ($RemoveData) {
    Write-Host "Persistent local Docker volumes will also be removed." -ForegroundColor Yellow
    & docker compose down --remove-orphans --volumes
}
else {
    & docker compose down --remove-orphans
}

if ($LASTEXITCODE -ne 0) {
    throw "docker compose down failed."
}

Write-Host "DIP stopped." -ForegroundColor Green
