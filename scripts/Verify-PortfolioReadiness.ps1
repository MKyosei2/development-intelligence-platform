# Purpose: Verifies P or tf ol io Re ad in es s requirements for DIP.
[CmdletBinding()]
param(
    [string]$UnityPath = $env:UNITY_EDITOR_PATH,
    [switch]$SkipUnity,
    [switch]$SkipP2
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root 'artifacts\portfolio-readiness'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$passPath = Join-Path $out 'PORTFOLIO_READINESS_PASS.txt'
$partialPath = Join-Path $out 'PORTFOLIO_READINESS_PARTIAL.txt'
Remove-Item -LiteralPath $passPath,$partialPath -Force -ErrorAction SilentlyContinue

Push-Location $root
try {
    $verifySolution = Join-Path $PSScriptRoot 'Verify-Solution.ps1'
    if (-not (Test-Path -LiteralPath $verifySolution)) { throw 'Verify-Solution.ps1 is required.' }
    & $verifySolution -Configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'P0/full-solution verification failed.' }

    & (Join-Path $PSScriptRoot 'Verify-P1.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'P1 verification failed.' }

    if (-not $SkipP2) {
        & (Join-Path $PSScriptRoot 'Verify-P2.ps1')
        if ($LASTEXITCODE -ne 0) { throw 'P2 verification failed.' }
    }

    if (-not $SkipUnity) {
        & (Join-Path $PSScriptRoot 'Verify-UnityCollector.ps1') -UnityPath $UnityPath
        if ($LASTEXITCODE -ne 0) { throw 'Unity Collector verification failed.' }
    }

    & (Join-Path $PSScriptRoot 'Verify-ProjectControl.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Project Control verification failed.' }

    $requiredEvidence = @(
        'artifacts\final-verification\PROJECT_COMPLETION.txt',
        'artifacts\p1-verification\P1_LOCAL_VERIFICATION.txt',
        'artifacts\qa-bug-zoo\qa-bug-zoo-results.json',
        'artifacts\project-control-verification\PROJECT_CONTROL_SOURCE_VERIFICATION_PASS.txt'
    )
    if (-not $SkipP2) { $requiredEvidence += 'artifacts\P2_VERIFICATION_PASS.txt' }
    if (-not $SkipUnity) { $requiredEvidence += 'artifacts\unity-verification\UNITY_COLLECTOR_VERIFICATION_PASS.txt' }

    foreach ($relative in $requiredEvidence) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $relative))) { throw "Required readiness evidence is missing: $relative" }
    }

    $partial = $SkipP2 -or $SkipUnity
    $lines = @(
        "Timestamp=$([DateTimeOffset]::UtcNow.ToString('u'))",
        'P0=PASS',
        'P1=PASS',
        "P2=$(if($SkipP2){'SKIPPED'}else{'PASS'})",
        'BugZoo=PASS',
        'ProjectControl=PASS',
        "Unity=$(if($SkipUnity){'SKIPPED'}else{'PASS'})"
    )
    if ($partial) {
        $lines += 'STATUS=PARTIAL'
        $lines | Set-Content -LiteralPath $partialPath -Encoding UTF8
        Write-Warning "Portfolio readiness is PARTIAL because one or more phases were skipped: $partialPath"
    }
    else {
        $lines += 'STATUS=PASS'
        $lines | Set-Content -LiteralPath $passPath -Encoding UTF8
        Write-Host "Portfolio readiness PASS: $passPath" -ForegroundColor Green
    }
}
finally { Pop-Location }
