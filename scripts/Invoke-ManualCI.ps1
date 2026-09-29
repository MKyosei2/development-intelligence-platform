# Purpose: Runs repository CI workflows explicitly while preserving exact-ref verification semantics.
[CmdletBinding()]
param(
    [ValidateSet('Fast','Targeted','Full')]
    [string]$Preset = 'Fast',
    [string[]]$Target = @(),
    [string]$Ref = '',
    [switch]$Yes,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI 'gh' is required."
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git is required."
}

& gh auth status
if ($LASTEXITCODE -ne 0) {
    throw 'GitHub CLI is not authenticated.'
}

if (-not $Ref) {
    $Ref = (& git branch --show-current | Out-String).Trim()
}
if (-not $Ref) {
    throw 'Could not determine ref.'
}

$map = [ordered]@{
    Commit      = 'commit-message.yml'
    DotNet      = 'dotnet-ci.yml'
    Python      = 'python-ci.yml'
    Native      = 'native-collector-ci.yml'
    Integration = 'integration-test.yml'
    Gui         = 'gui-e2e.yml'
    Package     = 'docker-build.yml'
    Security    = 'security-scan.yml'
    QA          = 'universal-qa-benchmark.yml'
    Unreal      = 'unreal-collector-ci.yml'
    Publication = 'publication-readiness.yml'
}

switch ($Preset) {
    'Fast' {
        $selected = @('Commit','DotNet','Publication')
    }
    'Targeted' {
        if ($Target.Count -eq 0) {
            throw "Targeted requires -Target. Valid values: $($map.Keys -join ', ')"
        }
        $unknown = @($Target | Where-Object { -not $map.Contains($_) })
        if ($unknown.Count -gt 0) {
            throw "Unknown Target value(s): $($unknown -join ', ')"
        }
        $selected = @($Target | Select-Object -Unique)
    }
    'Full' {
        $selected = @($map.Keys)
    }
}

$remoteRefJson = & gh api "repos/MKyosei2/development-intelligence-platform/git/ref/heads/$Ref"
if ($LASTEXITCODE -ne 0) {
    throw "Remote ref '$Ref' was not found."
}
$remoteRef = $remoteRefJson | ConvertFrom-Json
$remoteSha = [string]$remoteRef.object.sha
if ([string]::IsNullOrWhiteSpace($remoteSha) -or $remoteSha.Length -ne 40) {
    throw "Could not resolve exact remote SHA for '$Ref'."
}

$localSha = $null
try {
    $localSha = (& git rev-parse $Ref 2>$null | Out-String).Trim()
}
catch {
    $localSha = $null
}

Write-Host "Preset    : $Preset"
Write-Host "Ref       : $Ref"
Write-Host "Remote SHA: $remoteSha"

if ($localSha) {
    Write-Host "Local SHA : $localSha"
    if ($localSha -ne $remoteSha) {
        throw "Local ref '$Ref' does not match the remote ref. local=$localSha remote=$remoteSha"
    }
}

foreach ($name in $selected) {
    Write-Host "  $name -> $($map[$name])"
}

if ($DryRun) {
    Write-Host 'DRY RUN: no GitHub Actions were dispatched.' -ForegroundColor Green
    return
}

if (-not $Yes) {
    $answer = Read-Host 'Type RUN to consume GitHub Actions quota'
    if ($answer -ne 'RUN') {
        Write-Host 'Cancelled.'
        return
    }
}

$dispatches = [System.Collections.Generic.List[object]]::new()

foreach ($name in $selected) {
    $workflow = $map[$name]
    $started = [DateTimeOffset]::UtcNow

    & gh workflow run $workflow --ref $Ref
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to dispatch $workflow."
    }

    $dispatches.Add([pscustomobject]@{
        Name        = $name
        Workflow    = $workflow
        Ref         = $Ref
        ExpectedSha = $remoteSha
        DispatchedAtUtc = $started.ToString('o')
    }) | Out-Null
}

$dispatches |
    ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $root 'artifacts/manual-ci-dispatches.json') -Encoding UTF8

Write-Host "Manual CI dispatch completed for exact remote SHA $remoteSha." -ForegroundColor Green
