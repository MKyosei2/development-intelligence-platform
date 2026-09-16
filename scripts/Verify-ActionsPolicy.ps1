# Purpose: Automates the V er if y-A ct io ns Po li cy repository workflow.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$verifier = Join-Path $root 'tools\verify_actions_policy.py'
if (-not (Test-Path -LiteralPath $verifier)) { throw "Missing verifier: $verifier" }
python $verifier
if ($LASTEXITCODE -ne 0) { throw 'GitHub Actions execution policy verification failed.' }
Write-Host 'PASS: GitHub Actions three-stage policy.' -ForegroundColor Green
