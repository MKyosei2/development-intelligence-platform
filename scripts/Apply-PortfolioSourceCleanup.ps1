#requires -Version 5.1
# Purpose: Applies the final source-cleanup overlay and verifies repository-wide readability without triggering hosted CI.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$SkipFormat
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = [System.IO.Path]::GetFullPath($RepositoryRoot)
$apiClient = Join-Path $repo 'src\Dip.Web\Services\ApiClient.cs'
$repair = Join-Path $repo 'scripts\Repair-SourcePurposeComments.ps1'
$verify = Join-Path $repo 'scripts\Verify-SourceReadability.ps1'
$utf8 = New-Object System.Text.UTF8Encoding($false)

foreach ($required in @($apiClient, $repair, $verify)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required file was not found: $required"
    }
}

# Replace the UnsafeAccessor dependency with a normal internal accessor.
$apiText = [System.IO.File]::ReadAllText($apiClient)
if ($apiText -notmatch '(?m)^\s*internal\s+HttpClient\s+Transport\s*=>\s*_httpClient;\s*$') {
    $needle = '    private readonly HttpClient _httpClient;'
    if (-not $apiText.Contains($needle)) {
        throw 'Could not find the ApiClient HttpClient field; refusing to patch an unexpected revision.'
    }
    $replacement = $needle + "`n`n    internal HttpClient Transport => _httpClient;"
    $apiText = $apiText.Replace($needle, $replacement)
    [System.IO.File]::WriteAllText($apiClient, $apiText, $utf8)
    Write-Host 'Added ApiClient.Transport internal accessor.' -ForegroundColor Green
}

Write-Host 'Repairing malformed Purpose comments across handwritten source...' -ForegroundColor Cyan
& $repair -RepositoryRoot $repo -BrokenOnly
if ($LASTEXITCODE -ne 0) { throw 'Purpose repair failed.' }

if (-not $SkipFormat) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet was not found. Install/use the repository .NET 8 SDK or rerun with -SkipFormat.'
    }
    & dotnet format (Join-Path $repo 'DevelopmentIntelligencePlatform.sln') whitespace --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet format failed with exit code $LASTEXITCODE."
    }
}

$artifactDir = Join-Path $repo 'artifacts\source-readability'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

& $verify `
    -RepositoryRoot $repo `
    -Strict `
    -ReportPath (Join-Path $artifactDir 'report.md') `
    -InventoryPath (Join-Path $artifactDir 'inventory.csv')
if ($LASTEXITCODE -ne 0) { throw 'Strict source-readability audit failed.' }

& git -C $repo diff --check
if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed.' }

Write-Host 'Local source cleanup PASS. Hosted CI has not been triggered.' -ForegroundColor Green
