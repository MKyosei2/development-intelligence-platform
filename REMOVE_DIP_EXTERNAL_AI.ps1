[CmdletBinding()]
param(
    [string]$RepoPath = 'C:\Users\kyose\ドキュメント\GitHub\development-intelligence-platform',
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoPath = [System.IO.Path]::GetFullPath($RepoPath)
if (-not (Test-Path -LiteralPath (Join-Path $RepoPath '.git') -PathType Container)) {
    throw "Not a DIP Git repository: $RepoPath"
}

$relativeTargets = @(
    'src\Dip.Infrastructure\P2Intelligence\OpenAiCompatibleRootCauseClient.cs',
    'src\Dip.Infrastructure\P2Intelligence\OpenAiCompatibleFixProposalAgent.cs',
    'src\Dip.Infrastructure\P2Intelligence\P2IntelligenceOptions.cs'
)

Write-Host '=== DIP EXTERNAL AI SOURCE REMOVAL ===' -ForegroundColor Cyan
Write-Host "Repository: $RepoPath"
Write-Host "Mode: $(if($Apply){'APPLY'}else{'DRY-RUN'})"
Write-Host ''

foreach ($relative in $relativeTargets) {
    $full = [System.IO.Path]::GetFullPath((Join-Path $RepoPath $relative))
    $rootWithSlash = $RepoPath.TrimEnd('\') + '\'
    if (-not $full.StartsWith($rootWithSlash, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Safety guard rejected path outside repository: $full"
    }

    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
        Write-Host "[SKIP] Already absent: $relative" -ForegroundColor DarkGray
        continue
    }

    if ($Apply) {
        Remove-Item -LiteralPath $full -Force
        Write-Host "[REMOVED] $relative" -ForegroundColor Green
    }
    else {
        Write-Host "[WOULD REMOVE] $relative" -ForegroundColor Yellow
    }
}

if (-not $Apply) {
    Write-Host ''
    Write-Host 'Dry-run only. Re-run with -Apply to delete exactly the files above.' -ForegroundColor Yellow
    exit 0
}

$remaining = @($relativeTargets | Where-Object { Test-Path -LiteralPath (Join-Path $RepoPath $_) })
if ($remaining.Count -gt 0) {
    throw "Deletion verification failed: $($remaining -join ', ')"
}

Write-Host ''
Write-Host 'EXTERNAL_AI_SOURCE_REMOVAL=PASS' -ForegroundColor Green
Write-Host 'No Git commit/push/reset/clean/checkout operation was performed.' -ForegroundColor Green
