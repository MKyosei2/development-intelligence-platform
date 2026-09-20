#requires -Version 5.1
# Purpose: Applies repository-wide Purpose normalization and then verifies first-party source readability.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'Repair-SourcePurposeComments.ps1') -RepositoryRoot $RepositoryRoot

& (Join-Path $PSScriptRoot 'Verify-SourceReadability.ps1') `
    -RepositoryRoot $RepositoryRoot `
    -Strict `
    -ReportPath (Join-Path $RepositoryRoot 'artifacts\source-readability\report.md') `
    -InventoryPath (Join-Path $RepositoryRoot 'artifacts\source-readability\inventory.csv')

Write-Host 'PASS: repository-wide Purpose normalization and readability verification completed.' -ForegroundColor Green
