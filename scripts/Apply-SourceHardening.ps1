#requires -Version 5.1
# Purpose: Runs the permanent source Purpose repair and strict readability gates without invoking one-shot refactor tooling.
[CmdletBinding()]
param(
    [string]$RepositoryRoot
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

& (Join-Path $PSScriptRoot 'Repair-SourcePurposeComments.ps1') -RepositoryRoot $RepositoryRoot
& (Join-Path $PSScriptRoot 'Verify-SourceReadability.ps1') -RepositoryRoot $RepositoryRoot -Strict
