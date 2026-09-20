#requires -Version 5.1
# Purpose: Applies the requested source refactor, repository-wide Purpose normalization, and strict readability verification.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$SkipDotNetFormat
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'Apply-RequestedPortfolioRefactor.ps1') `
    -RepositoryRoot $RepositoryRoot `
    -SkipDotNetFormat:$SkipDotNetFormat
