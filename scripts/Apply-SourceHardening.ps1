#requires -Version 5.1
# Purpose: Verifies source readability by default and applies Purpose repair only when explicitly requested.
[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $invocationScriptPath = $MyInvocation.MyCommand.Path
    if ([string]::IsNullOrWhiteSpace($invocationScriptPath)) {
        throw 'Unable to resolve the script path. Supply -RepositoryRoot explicitly.'
    }
    $scriptDirectory = Split-Path -Parent $invocationScriptPath
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptDirectory '..')).Path
}

& (Join-Path $PSScriptRoot 'Test-SourcePurposeTooling.ps1')
if ($Apply) {
    & (Join-Path $PSScriptRoot 'Repair-SourcePurposeComments.ps1') -RepositoryRoot $RepositoryRoot -Apply
}
& (Join-Path $PSScriptRoot 'Verify-SourceReadability.ps1') -RepositoryRoot $RepositoryRoot -Strict
