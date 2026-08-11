[CmdletBinding()]
param(
    [switch]$SkipDotNet,
    [switch]$SkipNative,
    [switch]$SkipLiveE2E
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifactDirectory = Join-Path $root 'artifacts\p2-verification'
$completionPath = Join-Path $root 'artifacts\P2_VERIFICATION_PASS.txt'
$partialPath = Join-Path $root 'artifacts\P2_VERIFICATION_PARTIAL.txt'

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Push-Location $root
try {
    New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
    Remove-Item -LiteralPath $completionPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $partialPath -Force -ErrorAction SilentlyContinue

    $required = @(
        'src/Dip.Application/Features/Intelligence/P2OrchestrationService.cs',
        'src/Dip.Application/Features/Intelligence/ForecastAndDecisionService.cs',
        'src/Dip.Application/Features/P2Intelligence/AdaptiveQaPlanner.cs',
        'src/Dip.Application/Features/P2Intelligence/EvidenceGroundedRootCauseService.cs',
        'src/Dip.Application/Features/P2Intelligence/P2TechnicalIntelligenceService.cs',
        'src/Dip.Application/Features/P2Intelligence/ProjectDecisionIntelligenceService.cs',
        'src/Dip.Infrastructure/Intelligence/IntelligenceExecutionSafety.cs',
        'src/Dip.Infrastructure/Intelligence/MinimumHardwareRunner.cs',
        'src/Dip.Infrastructure/Intelligence/RegressionRunner.cs',
        'src/Dip.Infrastructure/Intelligence/ReliabilityRunner.cs',
        'src/Dip.Infrastructure/Intelligence/MutationRunner.cs',
        'src/Dip.Infrastructure/P2Intelligence/P2ExperimentRunner.cs',
        'src/Dip.Infrastructure/P2Intelligence/MediaRegressionRunner.cs',
        'src/Dip.Infrastructure/P2Intelligence/TestImpactAnalyzer.cs',
        'src/Dip.Infrastructure/P2Intelligence/NativeSymbolicator.cs',
        'src/Dip.Api/Controllers/P2IntelligenceController.cs',
        'tests/Dip.Application.Tests/P2Intelligence/GameRegressionEvaluatorTests.cs',
        'tests/Dip.Infrastructure.Tests/P2Intelligence/TestImpactAnalyzerTests.cs',
        'tests/Dip.EndToEndTests/Scenarios/P2IntelligenceScenario.cs',
        'tests/Dip.EndToEndTests/Scenarios/P2DecisionIntelligenceScenario.cs'
    )

    foreach ($file in $required) {
        if (-not (Test-Path -LiteralPath $file)) {
            throw "Missing P2 file: $file"
        }
    }

    $contentChecks = @{
        'src/Dip.Infrastructure/P2Intelligence/P2ExperimentRunner.cs' = @('Baseline passed','fuzz-manifest.json','MinimizeFailureAsync')
        'src/Dip.Infrastructure/P2Intelligence/TestImpactAnalyzer.cs' = @('ProjectReference','TestImpactResult')
        'src/Dip.Infrastructure/P2Intelligence/NativeSymbolicator.cs' = @('llvm-symbolizer','addr2line','symbolication.json')
        'src/Dip.Application/Features/P2Intelligence/P2Contracts.cs' = @('GameRegressionEvaluator','ITestImpactAnalyzer','INativeSymbolicator')
        'src/Dip.Application/Features/QualityAutomation/QaOrchestrationService.cs' = @('GetCrashClustersAsync')
    }
    foreach ($entry in $contentChecks.GetEnumerator()) {
        $raw = Get-Content -LiteralPath $entry.Key -Raw -Encoding UTF8
        foreach ($token in $entry.Value) {
            if (-not $raw.Contains($token)) { throw "P2 priority implementation marker '$token' is missing from $($entry.Key)." }
        }
    }

    if (-not $SkipDotNet) {
        if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
            throw 'dotnet was not found in PATH.'
        }

        $sdk = (& dotnet --version | Out-String).Trim()
        if (-not $sdk.StartsWith('8.')) {
            throw "P2 verification requires .NET SDK 8.x, active SDK is '$sdk'."
        }

        Invoke-Checked -FilePath 'dotnet' -Arguments @(
            'restore', 'DevelopmentIntelligencePlatform.sln'
        )
        Invoke-Checked -FilePath 'dotnet' -Arguments @(
            'build', 'DevelopmentIntelligencePlatform.sln',
            '--configuration', 'Release', '--no-restore'
        )

        foreach ($project in @(
            'tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj',
            'tests/Dip.Application.Tests/Dip.Application.Tests.csproj',
            'tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj'
        )) {
            Invoke-Checked -FilePath 'dotnet' -Arguments @(
                'test', $project,
                '--configuration', 'Release',
                '--no-build'
            )
        }
    }

    if (-not $SkipNative -and (Test-Path -LiteralPath '.\scripts\Build-P1-NativeTools.ps1')) {
        & '.\scripts\Build-P1-NativeTools.ps1'
    }

    if (-not $SkipLiveE2E) {
        $fullVerification = '.\scripts\Verify-Solution.ps1'
        if (-not (Test-Path -LiteralPath $fullVerification)) {
            throw 'Verify-Solution.ps1 is required for live P2 E2E verification.'
        }
        & $fullVerification -Configuration Release
    }

    $isPartial = $SkipDotNet -or $SkipNative -or $SkipLiveE2E
    $lines = @(
        "P2 verification completed at $([DateTimeOffset]::UtcNow.ToString('u'))",
        "DotNet=$(-not $SkipDotNet)",
        "Native=$(-not $SkipNative)",
        "LiveE2E=$(-not $SkipLiveE2E)",
        'Simulation-derived hardware results remain distinct from real-device evidence.'
    )

    if ($isPartial) {
        $lines += 'STATUS=PARTIAL'
        $lines | Set-Content -LiteralPath $partialPath -Encoding UTF8
        Write-Warning "P2 verification completed in PARTIAL mode: $partialPath"
    }
    else {
        $lines += 'STATUS=PASS'
        $lines | Set-Content -LiteralPath $completionPath -Encoding UTF8
        Write-Host "P2 verification PASS: $completionPath" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
