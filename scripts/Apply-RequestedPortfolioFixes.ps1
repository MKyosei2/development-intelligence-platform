#requires -Version 5.1
# Purpose: Applies the requested ApiClient transport, Purpose-comment, test-formatting, and readability fixes locally.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$SkipTestFormat,
    [switch]$SkipReadabilityVerification
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = [System.IO.Path]::GetFullPath($RepositoryRoot)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$apiClientPath = Join-Path $repo 'src\Dip.Web\Services\ApiClient.cs'
$repairPurposePath = Join-Path $repo 'scripts\Repair-SourcePurposeComments.ps1'
$verifyReadabilityPath = Join-Path $repo 'scripts\Verify-SourceReadability.ps1'
$solutionPath = Join-Path $repo 'DevelopmentIntelligencePlatform.sln'

foreach ($required in @($apiClientPath, $repairPurposePath, $verifyReadabilityPath, $solutionPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required repository file was not found: $required"
    }
}

Write-Host '[1/4] Ensuring ApiClient.Transport exists...' -ForegroundColor Cyan
$apiText = [System.IO.File]::ReadAllText($apiClientPath)
$normalized = $apiText -replace "`r`n", "`n" -replace "`r", "`n"

if ($normalized -notmatch '(?m)^\s*internal\s+HttpClient\s+Transport\s*=>\s*_httpClient;\s*$') {
    $needle = '    private readonly HttpClient _httpClient;'
    if (-not $normalized.Contains($needle)) {
        throw 'ApiClient._httpClient field was not found. Refusing to patch an unexpected source revision.'
    }

    $replacement = $needle + "`n`n    internal HttpClient Transport => _httpClient;"
    $normalized = $normalized.Replace($needle, $replacement)
    [System.IO.File]::WriteAllText(
        $apiClientPath,
        $normalized.TrimEnd("`n") + "`n",
        $utf8NoBom)

    Write-Host 'Added internal ApiClient.Transport accessor.' -ForegroundColor Green
}
else {
    Write-Host 'ApiClient.Transport is already present.' -ForegroundColor DarkGray
}

Write-Host '[2/4] Repairing malformed Purpose comments repository-wide...' -ForegroundColor Cyan
& $repairPurposePath -RepositoryRoot $repo -BrokenOnly
if ($LASTEXITCODE -ne 0) {
    throw "Purpose repair failed with exit code $LASTEXITCODE."
}

Write-Host '[3/4] Formatting test C#...' -ForegroundColor Cyan
if (-not $SkipTestFormat) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet was not found. Install/use the repository .NET SDK or rerun with -SkipTestFormat.'
    }

    & dotnet format $solutionPath whitespace --include (Join-Path $repo 'tests') --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet format for tests failed with exit code $LASTEXITCODE."
    }
}
else {
    Write-Host 'Test formatting skipped by request.' -ForegroundColor DarkGray
}

Write-Host '[4/4] Running local readability verification...' -ForegroundColor Cyan
if (-not $SkipReadabilityVerification) {
    $artifactDir = Join-Path $repo 'artifacts\source-readability'
    New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

    & $verifyReadabilityPath `
        -RepositoryRoot $repo `
        -ReportPath (Join-Path $artifactDir 'report.md') `
        -InventoryPath (Join-Path $artifactDir 'inventory.csv')

    if ($LASTEXITCODE -ne 0) {
        throw "Source readability verification failed with exit code $LASTEXITCODE."
    }
}
else {
    Write-Host 'Readability verification skipped by request.' -ForegroundColor DarkGray
}

Write-Host ''
Write-Host 'Requested S0/S1 source fixes were applied locally.' -ForegroundColor Green
Write-Host 'This script does not commit, push, edit .portfolio-ci-trigger, or start GitHub Actions.'
