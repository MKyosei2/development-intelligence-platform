[CmdletBinding()]
param(
    [switch]$Commit,
    [switch]$Push,
    [switch]$CreatePullRequest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

$branch = (& git branch --show-current | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or -not $branch) { throw 'Could not resolve the current branch.' }
if ($branch -eq 'main') {
    throw 'Final verification must be prepared on a dedicated PR branch, not directly on main.'
}

$dirty = (& git status --porcelain | Out-String).Trim()
if ($dirty) { throw "Working tree must be clean before preparing final verification.`n$dirty" }

& git fetch origin main --prune
if ($LASTEXITCODE -ne 0) { throw 'Could not fetch origin/main.' }
$mergeBase = (& git merge-base HEAD origin/main | Out-String).Trim()
$remoteMain = (& git rev-parse origin/main | Out-String).Trim()
if ($mergeBase -ne $remoteMain) {
    throw 'Final-verification branch must be based on the current origin/main.'
}

$sourceParentSha = (& git rev-parse HEAD | Out-String).Trim()
$now = [DateTimeOffset]::UtcNow
$triggerPath = Join-Path $root '.portfolio-ci-trigger'
@(
    'schema=dip-final-verification-v1',
    "requestedAtUtc=$($now.ToString('o'))",
    "sourceParentSha=$sourceParentSha",
    "requestedFromBranch=$branch"
) | Set-Content -LiteralPath $triggerPath -Encoding UTF8

$changed = @(& git diff --name-only)
if ($changed.Count -ne 1 -or $changed[0] -ne '.portfolio-ci-trigger') {
    throw "Final trigger preparation changed unexpected files: $($changed -join ', ')"
}

Write-Host 'Final verification trigger prepared.' -ForegroundColor Green
Write-Host "Source parent SHA: $sourceParentSha"
Write-Host 'Only .portfolio-ci-trigger changed.'

if ($Commit) {
    & git add .portfolio-ci-trigger
    if ($LASTEXITCODE -ne 0) { throw 'git add failed.' }
    & git commit -m 'ci(portfolio): request final exact-revision verification'
    if ($LASTEXITCODE -ne 0) { throw 'Final trigger commit failed.' }

    if ($Push -or $CreatePullRequest) {
        & git push -u origin $branch
        if ($LASTEXITCODE -ne 0) { throw 'Final trigger branch push failed.' }
    }

    if ($CreatePullRequest) {
        if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI gh is required.' }
        & gh pr create `
            --base main `
            --head $branch `
            --title 'ci(portfolio): request final exact-revision verification' `
            --body 'Source is frozen. This PR changes only `.portfolio-ci-trigger`. After merge, the resulting main SHA must obtain all 11 required Green workflows before benchmark/runtime evidence and Release.'
        if ($LASTEXITCODE -ne 0) { throw 'Could not create final trigger PR.' }
    }
}
elseif ($Push -or $CreatePullRequest) {
    throw '-Push/-CreatePullRequest require -Commit.'
}
