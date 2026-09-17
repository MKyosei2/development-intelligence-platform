#requires -Version 5.1
# Purpose: Removes stale code-reference entries that point only to deleted placeholder features.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$path = Join-Path $RepositoryRoot 'docs\code-reference\README.md'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$text = [System.IO.File]::ReadAllText($path)

$lines = @($text -replace "`r`n", "`n" -split "`n")
$filtered = foreach ($line in $lines) {
    if ($line -match '^\s*-\s*基準revision:') { continue }
    if ($line -match '^\|\s*\[Analysis\]\(\./Analysis/README\.md\)') { continue }
    if ($line -match '^\|\s*\[TeamLoad\]\(\./TeamLoad/README\.md\)') { continue }
    $line
}

[System.IO.File]::WriteAllText(
    $path,
    (($filtered -join "`n").TrimEnd() + "`n"),
    $utf8
)

Write-Host 'Removed stale Analysis/TeamLoad placeholder entries and fixed revision pin from code-reference index.' -ForegroundColor Green
