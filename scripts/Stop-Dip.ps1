[CmdletBinding()]
param(
    [switch]$ResetData
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot

try {
    $docker = Get-Command "docker.exe" -ErrorAction SilentlyContinue

    if ($null -eq $docker) {
        $candidate = Join-Path $env:ProgramFiles "Docker\Docker\resources\bin\docker.exe"
        if (Test-Path -LiteralPath $candidate) {
            $dockerPath = $candidate
        }
        else {
            Write-Host "Docker CLI is unavailable. No running DIP containers can be controlled from this shell." -ForegroundColor Yellow
            return
        }
    }
    else {
        $dockerPath = $docker.Source
    }

    $arguments = @("compose", "down", "--remove-orphans")
    if ($ResetData) {
        $arguments += "--volumes"
    }

    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        & $dockerPath @arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    if ($exitCode -ne 0) {
        throw "docker compose down failed with exit code $exitCode."
    }

    if ($ResetData) {
        Write-Host "DIP stopped and persistent Docker volumes were removed." -ForegroundColor Green
    }
    else {
        Write-Host "DIP stopped. Persistent data was preserved." -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
