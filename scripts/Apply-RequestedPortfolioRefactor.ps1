#requires -Version 5.1
# Purpose: Applies the requested portfolio source refactor, formats C#, and runs source-readability verification.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$SkipDotNetFormat
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) { $python = Get-Command py -ErrorAction SilentlyContinue }
if (-not $python) { throw 'Python 3 is required to apply the deterministic source refactor.' }

$tool = Join-Path $RepositoryRoot 'tools\apply_requested_source_refactor.py'
& $python.Source $tool --repository-root $RepositoryRoot
if ($LASTEXITCODE -ne 0) { throw "Source refactor failed with exit code $LASTEXITCODE." }

if (-not $SkipDotNetFormat) {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        & dotnet format (Join-Path $RepositoryRoot 'DevelopmentIntelligencePlatform.sln') whitespace --no-restore --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw "dotnet format failed with exit code $LASTEXITCODE." }
    }
    else {
        Write-Warning 'dotnet was not found; skipped Roslyn whitespace formatting.'
    }
}

& (Join-Path $PSScriptRoot 'Repair-SourcePurposeComments.ps1') -RepositoryRoot $RepositoryRoot
& (Join-Path $PSScriptRoot 'Verify-SourceReadability.ps1') `
    -RepositoryRoot $RepositoryRoot `
    -Strict `
    -ReportPath (Join-Path $RepositoryRoot 'artifacts\source-readability\report.md') `
    -InventoryPath (Join-Path $RepositoryRoot 'artifacts\source-readability\inventory.csv')

& $python.Source $tool --repository-root $RepositoryRoot --verify-only
if ($LASTEXITCODE -ne 0) { throw "Post-refactor verification failed with exit code $LASTEXITCODE." }

Write-Host 'PASS: requested portfolio source refactor and readability verification completed.' -ForegroundColor Green
