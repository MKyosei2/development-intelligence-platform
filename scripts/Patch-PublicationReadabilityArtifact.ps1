#requires -Version 5.1
# Purpose: Routes publication-readiness readability outputs to CI artifacts instead of tracked documentation.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$path = Join-Path $RepositoryRoot '.github\workflows\publication-readiness.yml'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$text = [System.IO.File]::ReadAllText($path)

$old = @'
      - name: Verify source readability
        shell: pwsh
        run: ./scripts/Verify-SourceReadability.ps1 -Strict
'@

$new = @'
      - name: Verify source readability
        shell: pwsh
        run: |
          ./scripts/Verify-SourceReadability.ps1 `
            -Strict `
            -ReportPath artifacts/source-readability/report.md `
            -InventoryPath artifacts/source-readability/inventory.csv
'@

if ($text.Contains($old)) {
    $text = $text.Replace($old, $new)
}
elseif (-not $text.Contains('artifacts/source-readability/report.md')) {
    throw 'Expected readability step was not found; refusing to patch an unexpected workflow.'
}

$needle = '            artifacts/portfolio-readiness/exact-revision.md'
if (-not $text.Contains('artifacts/source-readability/inventory.csv')) {
    if (-not $text.Contains($needle)) {
        throw 'Expected artifact upload block was not found.'
    }
    $replacement = $needle + "`n" +
        '            artifacts/source-readability/report.md' + "`n" +
        '            artifacts/source-readability/inventory.csv'
    $text = $text.Replace($needle, $replacement)
}

[System.IO.File]::WriteAllText($path, $text, $utf8)
Write-Host 'Updated publication-readiness readability artifact routing.' -ForegroundColor Green
