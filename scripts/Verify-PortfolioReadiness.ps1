# Purpose: Verifies portfolio readiness across core, Game QA, Technical Intelligence, runtime, and project-control evidence.
[CmdletBinding()]
param(
    [string]$UnityPath = $env:UNITY_EDITOR_PATH,
    [switch]$SkipUnity,
    [switch]$SkipTechnicalIntelligence
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root 'artifacts\portfolio-readiness'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$passPath = Join-Path $out 'PORTFOLIO_READINESS_PASS.txt'
$partialPath = Join-Path $out 'PORTFOLIO_READINESS_PARTIAL.txt'
Remove-Item -LiteralPath $passPath, $partialPath -Force -ErrorAction SilentlyContinue

Push-Location $root
try {
    $verifyCore = Join-Path $PSScriptRoot 'Verify-Solution.ps1'
    if (-not (Test-Path -LiteralPath $verifyCore -PathType Leaf)) {
        throw 'Verify-Solution.ps1 is required for Core verification.'
    }
    & $verifyCore -Configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Core/full-solution verification failed.' }

    $verifyGameQa = Join-Path $PSScriptRoot 'Verify-GameQa.ps1'
    if (-not (Test-Path -LiteralPath $verifyGameQa -PathType Leaf)) {
        throw 'Verify-GameQa.ps1 is required.'
    }
    & $verifyGameQa
    if ($LASTEXITCODE -ne 0) { throw 'Game QA verification failed.' }

    if (-not $SkipTechnicalIntelligence) {
        $verifyTechnicalIntelligence = Join-Path $PSScriptRoot 'Verify-TechnicalIntelligence.ps1'
        if (-not (Test-Path -LiteralPath $verifyTechnicalIntelligence -PathType Leaf)) {
            throw 'Verify-TechnicalIntelligence.ps1 is required.'
        }
        & $verifyTechnicalIntelligence
        if ($LASTEXITCODE -ne 0) { throw 'Technical Intelligence verification failed.' }
    }

    if (-not $SkipUnity) {
        & (Join-Path $PSScriptRoot 'Verify-UnityCollector.ps1') -UnityPath $UnityPath
        if ($LASTEXITCODE -ne 0) { throw 'Unity runtime verification failed.' }
    }

    & (Join-Path $PSScriptRoot 'Verify-ProjectControl.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Project Control verification failed.' }

    $requiredEvidence = @(
        'artifacts\final-verification\PROJECT_COMPLETION.txt',
        'artifacts\game-qa-verification\GAME_QA_LOCAL_VERIFICATION.txt',
        'artifacts\qa-bug-zoo\qa-bug-zoo-results.json',
        'artifacts\project-control-verification\PROJECT_CONTROL_SOURCE_VERIFICATION_PASS.txt'
    )
    if (-not $SkipTechnicalIntelligence) {
        $requiredEvidence += 'artifacts\technical-intelligence-verification\TECHNICAL_INTELLIGENCE_VERIFICATION_PASS.txt'
    }
    if (-not $SkipUnity) {
        $requiredEvidence += 'artifacts\unity-verification\UNITY_COLLECTOR_VERIFICATION_PASS.txt'
    }

    foreach ($relative in $requiredEvidence) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $relative))) {
            throw "Required readiness evidence is missing: $relative"
        }
    }

    $partial = $SkipTechnicalIntelligence -or $SkipUnity
    $lines = @(
        "Timestamp=$([DateTimeOffset]::UtcNow.ToString('u'))",
        'Core=PASS',
        'GameQa=PASS',
        "TechnicalIntelligence=$(if ($SkipTechnicalIntelligence) { 'SKIPPED' } else { 'PASS' })",
        'BugZoo=PASS',
        'ProjectControl=PASS',
        "RuntimeUnity=$(if ($SkipUnity) { 'SKIPPED' } else { 'PASS' })"
    )

    if ($partial) {
        $lines += 'STATUS=PARTIAL'
        $lines | Set-Content -LiteralPath $partialPath -Encoding UTF8
        Write-Warning "Portfolio readiness is PARTIAL because one or more responsibility groups were skipped: $partialPath"
    }
    else {
        $lines += 'STATUS=PASS'
        $lines | Set-Content -LiteralPath $passPath -Encoding UTF8
        Write-Host "Portfolio readiness PASS: $passPath" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
