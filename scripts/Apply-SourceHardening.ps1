#requires -Version 5.1
# Purpose: Runs the permanent source Purpose repair and strict readability gates without invoking one-shot refactor tooling.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'Repair-SourcePurposeComments.ps1') -RepositoryRoot $RepositoryRoot
& (Join-Path $PSScriptRoot 'Verify-SourceReadability.ps1') -RepositoryRoot $RepositoryRoot -Strict
