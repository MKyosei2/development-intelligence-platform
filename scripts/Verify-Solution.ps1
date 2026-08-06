[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path `
    $repositoryRoot `
    "DevelopmentIntelligencePlatform.sln"

if (-not (Test-Path -LiteralPath $solutionPath)) {
    throw "Solution not found: $solutionPath"
}

Push-Location $repositoryRoot

try {
    Write-Host "Cleaning generated directories..."

    Remove-Item `
        -LiteralPath (Join-Path $repositoryRoot ".vs") `
        -Recurse `
        -Force `
        -ErrorAction SilentlyContinue

    Get-ChildItem `
        -Path $repositoryRoot `
        -Recurse `
        -Directory `
        -Filter bin |
        Remove-Item `
            -Recurse `
            -Force `
            -ErrorAction SilentlyContinue

    Get-ChildItem `
        -Path $repositoryRoot `
        -Recurse `
        -Directory `
        -Filter obj |
        Remove-Item `
            -Recurse `
            -Force `
            -ErrorAction SilentlyContinue

    Write-Host "Restoring solution..."
    dotnet restore $solutionPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }

    Write-Host "Building solution..."
    dotnet build `
        $solutionPath `
        --configuration $Configuration `
        --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    Write-Host "Running tests..."
    dotnet test `
        $solutionPath `
        --configuration $Configuration `
        --no-build
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed with exit code $LASTEXITCODE."
    }

    Write-Host ""
    Write-Host "SUCCESS: restore, build, and test completed."
}
finally {
    Pop-Location
}
