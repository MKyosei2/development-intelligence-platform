# Purpose: Verifies GUI Phase1 for repository development and validation workflows.
[CmdletBinding()]
param(
    [int]$StartupTimeoutSeconds=420,
    [switch]$KeepOnFailure,
    [switch]$SkipBrowserInstall
)

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out=Join-Path $root 'artifacts\gui-phase1-e2e'
New-Item -ItemType Directory -Force -Path $out|Out-Null
$passFile=Join-Path $out 'GUI_PHASE1_E2E_PASS.txt'
$resultFile=Join-Path $out 'gui-phase1-e2e.json'
Remove-Item $passFile -Force -ErrorAction SilentlyContinue

# Keep the existing static GUI contract, but delegate runtime/browser execution
# to the Native-first verifier already used by GitHub GUI E2E. This removes a
# second Docker-specific stack implementation and avoids configuration drift.
& (Join-Path $PSScriptRoot 'Verify-GuiPhase1Static.ps1')

if($SkipBrowserInstall){
    Write-Warning 'SkipBrowserInstall is retained for call-site compatibility; the Native GUI verifier owns Playwright browser provisioning.'
}

if($KeepOnFailure){
    & (Join-Path $PSScriptRoot 'Verify-GuiRuntime.ps1') -RunPlaywright -StartupTimeoutSeconds $StartupTimeoutSeconds -KeepOnFailure
}else{
    & (Join-Path $PSScriptRoot 'Verify-GuiRuntime.ps1') -RunPlaywright -StartupTimeoutSeconds $StartupTimeoutSeconds
}

$runtimeRoot=Join-Path $root 'artifacts\gui-runtime-verification'
$latest=Get-ChildItem -LiteralPath $runtimeRoot -Directory -ErrorAction Stop |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if($null-eq$latest){throw 'Native GUI verification did not create an evidence directory.'}
$nativePass=Join-Path $latest.FullName 'GUI_RUNTIME_VERIFICATION_PASS.txt'
$nativeJson=Join-Path $latest.FullName 'gui-runtime-verification.json'
if(-not(Test-Path -LiteralPath $nativePass -PathType Leaf)){throw 'Native GUI verification PASS artifact is missing.'}
if(-not(Test-Path -LiteralPath $nativeJson -PathType Leaf)){throw 'Native GUI verification JSON artifact is missing.'}

$native=Get-Content -LiteralPath $nativeJson -Raw|ConvertFrom-Json
if([string]$native.runtime-ne'native'){throw 'GUI verification evidence is not Native runtime evidence.'}
if([string]$native.playwright-ne'PASS'){throw 'GUI verification did not record Playwright PASS.'}

$sha=(& git -C $root rev-parse HEAD|Out-String).Trim()
if([string]$native.sourceCommit-ne$sha){throw "GUI runtime evidence belongs to $($native.sourceCommit), expected $sha"}

[ordered]@{
    schema='dip-gui-phase1-e2e-v2'
    runtime='native'
    sourceCommit=$sha
    verifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
    staticValidation='PASS'
    systemStatus='PASS'
    playwright='PASS'
    nativeEvidence=$nativeJson
}|ConvertTo-Json -Depth 8|Set-Content $resultFile -Encoding UTF8

@(
    'GUI PHASE 1 E2E PASS',
    'Runtime=Native',
    "Commit=$sha",
    'StaticValidation=PASS',
    'SystemStatusCore=PASS',
    'Playwright=PASS'
)|Set-Content $passFile -Encoding UTF8
Write-Host 'GUI PHASE 1 E2E PASS (Native runtime)' -ForegroundColor Green
