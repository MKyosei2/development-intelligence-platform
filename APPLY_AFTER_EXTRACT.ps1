# Purpose: Completes the overlay application by removing source files that must not survive extraction.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = $PSScriptRoot

if (-not (Test-Path -LiteralPath (Join-Path $root 'DevelopmentIntelligencePlatform.sln') -PathType Leaf)) {
    throw 'Run this script from the repository root after extracting the overlay there. DevelopmentIntelligencePlatform.sln was not found.'
}

$deleteList = Join-Path $root '_DELETE_FILES.txt'
if (-not (Test-Path -LiteralPath $deleteList -PathType Leaf)) {
    throw '_DELETE_FILES.txt is missing.'
}

foreach ($relative in Get-Content -LiteralPath $deleteList) {
    $relative = $relative.Trim()
    if (-not $relative -or $relative.StartsWith('#')) { continue }
    $target = Join-Path $root $relative
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        Remove-Item -LiteralPath $target -Force
        Write-Host "DELETED: $relative" -ForegroundColor Yellow
    }
    else {
        Write-Host "ALREADY ABSENT: $relative" -ForegroundColor DarkGray
    }
}

$legacyEndpoint = Select-String -Path (Join-Path $root 'src/Dip.Api/Controllers/IntelligenceController.cs') -Pattern 'HttpPost\("adaptive-qa"\)' -Quiet
if ($legacyEndpoint) { throw 'Legacy adaptive-qa endpoint still exists after overlay application.' }

Write-Host 'Overlay deletion step completed.' -ForegroundColor Green
Write-Host 'Next: run clean restore/build/tests, then create a new exact-revision final evidence bundle.'
