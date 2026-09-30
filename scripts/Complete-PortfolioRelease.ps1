# Purpose: Runs the complete fail-closed exact-revision portfolio release workflow without requiring external Unity/Unreal projects.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Version,
    [int]$WaitMinutes = 60,
    [switch]$PublishRelease
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

foreach ($command in @('git','python','gh','dotnet')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Required command '$command' was not found."
    }
}

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Invalid semantic version: $Version"
}

$branch = (& git branch --show-current | Out-String).Trim()
if ($branch -ne 'main') { throw "Final release must run from main, not '$branch'." }

$dirty = (& git status --porcelain --untracked-files=no | Out-String).Trim()
if ($dirty) { throw "Final release requires a clean tracked working tree.`n$dirty" }

& git fetch origin main --prune
if ($LASTEXITCODE -ne 0) { throw 'Could not fetch origin/main.' }

$sha = (& git rev-parse HEAD | Out-String).Trim()
$remoteSha = (& git rev-parse origin/main | Out-String).Trim()
if ($sha -ne $remoteSha) {
    throw "Local main is not the publication revision. local=$sha origin/main=$remoteSha"
}

Write-Host "Final DIP SHA: $sha" -ForegroundColor Cyan

$ciPath = Join-Path $root 'artifacts\final-evidence\ci-matrix.json'
& python tools/verify_final_ci_matrix.py --sha $sha --wait-minutes $WaitMinutes --output $ciPath
if ($LASTEXITCODE -ne 0) { throw 'Required exact-SHA CI matrix is not fully Green.' }

& (Join-Path $PSScriptRoot 'Run-PortfolioBenchmarks.ps1') -ExpectedSha $sha
if ($LASTEXITCODE -ne 0) { throw 'Exact-SHA benchmark failed.' }

$qaOut = Join-Path $root 'artifacts\qa-bug-zoo'
& dotnet run --project tools/Dip.QaBugZooVerifier/Dip.QaBugZooVerifier.csproj -c Release -- `
    --root $root `
    --output $qaOut `
    --expected-sha $sha
if ($LASTEXITCODE -ne 0) { throw 'Exact-SHA QA Bug Zoo verification failed.' }

$projectTruthPath = Join-Path $root 'artifacts\qa-project-truth\project-truth-evidence.json'
$startedHere = $false
try {
    $healthy = $false
    try {
        $health = Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:5080/health' -TimeoutSec 2
        $healthy = $health.StatusCode -eq 200
    }
    catch { $healthy = $false }

    if (-not $healthy) {
        & (Join-Path $PSScriptRoot 'Start-DIP.ps1') -NoBrowser
        if ($LASTEXITCODE -ne 0) { throw 'Could not start DIP for QA Project Truth verification.' }
        $startedHere = $true
    }

    & python tools/publish_qa_scenario_evidence.py `
        --api-base 'http://127.0.0.1:5080' `
        --expected-dip-sha $sha `
        --output 'artifacts/qa-project-truth'
    if ($LASTEXITCODE -ne 0) { throw 'QA Scenario -> DIP -> Project Truth verification failed.' }
}
finally {
    if ($startedHere) {
        try { & (Join-Path $PSScriptRoot 'Stop-DIP.ps1') } catch { }
    }
}

& python tools/finalize_portfolio_evidence.py `
    --sha $sha `
    --ci 'artifacts/final-evidence/ci-matrix.json' `
    --benchmark 'artifacts/portfolio-benchmark/portfolio-benchmark-results.json' `
    --qa-bug-zoo 'artifacts/qa-bug-zoo/qa-bug-zoo-results.json' `
    --project-truth 'artifacts/qa-project-truth/project-truth-evidence.json' `
    --output-root 'artifacts/final-evidence'
if ($LASTEXITCODE -ne 0) { throw 'Final evidence binding failed.' }

$finalRoot = Join-Path $root ("artifacts\final-evidence\" + $sha)
Write-Host ''
Write-Host 'FINAL EVIDENCE READY' -ForegroundColor Green
Write-Host "SHA: $sha"
Write-Host "Evidence: $finalRoot"
Write-Host 'Unity runtime: NOT_EVALUATED (not a publication requirement)'
Write-Host 'Unreal runtime: NOT_EVALUATED (not a publication requirement)'

if ($PublishRelease) {
    & gh auth status
    if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI is not authenticated.' }

    $dispatchTime = [DateTimeOffset]::UtcNow
    & gh workflow run release.yml --ref main -f "version=$Version" -f "expected_sha=$sha"
    if ($LASTEXITCODE -ne 0) { throw 'Could not dispatch Release workflow.' }

    $releaseRunId = $null
    $deadline = (Get-Date).AddMinutes(5)
    while (-not $releaseRunId -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        $json = & gh run list --workflow release.yml --branch main --limit 20 --json databaseId,headSha,status,conclusion,createdAt
        if ($LASTEXITCODE -ne 0) { throw 'Could not query Release workflow runs.' }
        $candidate = @($json | ConvertFrom-Json) | Where-Object {
            $_.headSha -eq $sha -and [DateTimeOffset]$_.createdAt -ge $dispatchTime.AddMinutes(-1)
        } | Sort-Object createdAt -Descending | Select-Object -First 1
        if ($candidate) { $releaseRunId = [string]$candidate.databaseId }
    }
    if (-not $releaseRunId) { throw 'Could not identify the dispatched Release run.' }

    & gh run watch $releaseRunId --exit-status
    if ($LASTEXITCODE -ne 0) { throw "Release workflow failed: run $releaseRunId" }

    $tag = "v$Version"
    $uploadFiles = @(
        (Join-Path $finalRoot 'FINAL_EVIDENCE_SUMMARY.json'),
        (Join-Path $finalRoot 'SHA256SUMS.txt'),
        (Join-Path $root 'artifacts\portfolio-benchmark\portfolio-benchmark-results.json'),
        (Join-Path $root 'artifacts\qa-bug-zoo\qa-bug-zoo-results.json'),
        $projectTruthPath
    )
    foreach ($file in $uploadFiles) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Release evidence file missing: $file" }
    }

    & gh release upload $tag @uploadFiles --clobber
    if ($LASTEXITCODE -ne 0) { throw "Could not upload exact-revision evidence to $tag." }
    Write-Host "RELEASE COMPLETE: $tag @ $sha" -ForegroundColor Green
}
