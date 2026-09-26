# Purpose: Verifies technical-intelligence execution, reliability, regression, mutation, fuzzing, and live E2E paths.
[CmdletBinding()]
param(
    [switch]$SkipDotNet,
    [switch]$SkipNative,
    [switch]$SkipLiveE2E
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifactDirectory = Join-Path $root 'artifacts/technical-intelligence-verification'
$completionPath = Join-Path $artifactDirectory 'TECHNICAL_INTELLIGENCE_VERIFICATION_PASS.txt'
$partialPath = Join-Path $artifactDirectory 'TECHNICAL_INTELLIGENCE_VERIFICATION_PARTIAL.txt'

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
        'src/Dip.Application/Features/P2Intelligence/P2Contracts.cs',
        'src/Dip.Application/Features/P2Intelligence/SoakContracts.cs',
        'src/Dip.Application/Features/P2Intelligence/MutationContracts.cs',
        'src/Dip.Application/Features/P2Intelligence/FuzzContracts.cs',
        'src/Dip.Application/Features/P2Intelligence/RegressionContracts.cs',
        'src/Dip.Application/Features/P2Intelligence/MediaRegressionContracts.cs',
        'src/Dip.Application/Features/P2Intelligence/GameRegressionContracts.cs',
        'src/Dip.Application/Features/P2Intelligence/P2ExperimentRunnerContracts.cs',
        'src/Dip.Infrastructure/Intelligence/IntelligenceExecutionSafety.cs',
        'src/Dip.Infrastructure/Intelligence/MinimumHardwareRunner.cs',
        'src/Dip.Infrastructure/Intelligence/RegressionRunner.cs',
        'src/Dip.Infrastructure/Intelligence/ReliabilityRunner.cs',
        'src/Dip.Infrastructure/Intelligence/MutationRunner.cs',
        'src/Dip.Infrastructure/P2Intelligence/P2ExperimentRunner.cs',
        'src/Dip.Infrastructure/P2Intelligence/MutationExperimentRunner.cs',
        'src/Dip.Infrastructure/P2Intelligence/FuzzExperimentRunner.cs',
        'src/Dip.Infrastructure/P2Intelligence/ExperimentExecutionSupport.cs',
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
            throw "Missing technical-intelligence file: $file"
        }
    }

    $contentChecks = @{
        'src/Dip.Infrastructure/P2Intelligence/MutationExperimentRunner.cs' = @('Baseline passed')
        'src/Dip.Infrastructure/P2Intelligence/FuzzExperimentRunner.cs' = @('fuzz-manifest.json','MinimizeFailureAsync')
        'src/Dip.Infrastructure/P2Intelligence/ExperimentExecutionSupport.cs' = @('MinimizeFailureAsync')
        'src/Dip.Infrastructure/P2Intelligence/TestImpactAnalyzer.cs' = @('ProjectReference','TestImpactResult')
        'src/Dip.Infrastructure/P2Intelligence/NativeSymbolicator.cs' = @('llvm-symbolizer','addr2line','symbolication.json')
        'src/Dip.Application/Features/P2Intelligence/P2Contracts.cs' = @('ITestImpactAnalyzer','INativeSymbolicator')
        'src/Dip.Application/Features/P2Intelligence/GameRegressionContracts.cs' = @('GameRegressionEvaluator','GameRegressionCommand','GameRegressionResult')
        'src/Dip.Application/Features/P2Intelligence/SoakContracts.cs' = @('SoakCommand','SoakResult')
        'src/Dip.Application/Features/P2Intelligence/MutationContracts.cs' = @('MutationCommand','MutationResult')
        'src/Dip.Application/Features/P2Intelligence/FuzzContracts.cs' = @('FuzzCommand','FuzzResult')
        'src/Dip.Application/Features/P2Intelligence/RegressionContracts.cs' = @('GitBisectCommand','RegressionOriginResult')
        'src/Dip.Application/Features/P2Intelligence/MediaRegressionContracts.cs' = @('MediaRegressionCommand','IMediaRegressionRunner')
        'src/Dip.Application/Features/QualityAutomation/QaOrchestrationService.cs' = @('GetCrashClustersAsync')
    }

    foreach ($entry in $contentChecks.GetEnumerator()) {
        $raw = Get-Content -LiteralPath $entry.Key -Raw -Encoding UTF8
        foreach ($token in $entry.Value) {
            if (-not $raw.Contains($token)) {
                throw "Technical-intelligence implementation marker '$token' is missing from $($entry.Key)."
            }
        }
    }

    if (-not $SkipDotNet) {
        if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet was not found in PATH.' }
        $sdk = (& dotnet --version | Out-String).Trim()
        if (-not $sdk.StartsWith('8.')) { throw "Technical-intelligence verification requires .NET SDK 8.x, active SDK is '$sdk'." }

        Invoke-Checked -FilePath 'dotnet' -Arguments @('restore', 'DevelopmentIntelligencePlatform.sln')
        Invoke-Checked -FilePath 'dotnet' -Arguments @('build', 'DevelopmentIntelligencePlatform.sln', '--configuration', 'Release', '--no-restore')

        foreach ($project in @(
            'tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj',
            'tests/Dip.Application.Tests/Dip.Application.Tests.csproj',
            'tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj'
        )) {
            Invoke-Checked -FilePath 'dotnet' -Arguments @('test', $project, '--configuration', 'Release', '--no-build')
        }
    }

    if (-not $SkipNative) {
        & (Join-Path $PSScriptRoot 'Build-NativeTools.ps1') -Configuration Release
        if ($LASTEXITCODE -ne 0) { throw 'Native tool verification failed.' }
    }

    if (-not $SkipLiveE2E) {
        $fullVerification = Join-Path $PSScriptRoot 'Verify-Solution.ps1'
        if (-not (Test-Path -LiteralPath $fullVerification)) { throw 'Verify-Solution.ps1 is required for live technical-intelligence E2E verification.' }
        & $fullVerification -Configuration Release
        if ($LASTEXITCODE -ne 0) { throw 'Live technical-intelligence E2E verification failed.' }
    }

    $isPartial = $SkipDotNet -or $SkipNative -or $SkipLiveE2E
    $lines = @(
        "Technical Intelligence verification completed at $([DateTimeOffset]::UtcNow.ToString('u'))",
        "DotNet=$(-not $SkipDotNet)",
        "Native=$(-not $SkipNative)",
        "LiveE2E=$(-not $SkipLiveE2E)",
        'Simulation-derived hardware results remain distinct from real-device evidence.'
    )

    if ($isPartial) {
        $lines += 'STATUS=PARTIAL'
        $lines | Set-Content -LiteralPath $partialPath -Encoding UTF8
        Write-Warning "Technical Intelligence verification completed in PARTIAL mode: $partialPath"
    }
    else {
        $lines += 'STATUS=PASS'
        $lines | Set-Content -LiteralPath $completionPath -Encoding UTF8
        Write-Host "Technical Intelligence verification PASS: $completionPath" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
