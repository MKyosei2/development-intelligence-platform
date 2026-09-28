# Purpose: Verifies QA governance source boundaries and filtered tests with fail-closed TRX validation.
[CmdletBinding()]
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Invoke-DotNetTestStrict.ps1')

foreach ($command in @('git', 'dotnet')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command is required."
    }
}

$required = @(
    'src/Dip.Domain/Features/Quality/Enums/QaGovernanceEnums.cs',
    'src/Dip.Domain/Features/Evidence/Entities/QaGovernanceEntities.cs',
    'src/Dip.Domain/Features/Evidence/Entities/QaFindingLifecycle.cs',
    'src/Dip.Domain/Features/Evidence/Entities/QaWaiver.cs',
    'src/Dip.Domain/Features/Evidence/Entities/QaReliability.cs',
    'src/Dip.Domain/Features/Evidence/Entities/QaPolicy.cs',
    'src/Dip.Domain/Features/Evidence/Entities/QaProvenance.cs',
    'src/Dip.Domain/Features/Evidence/Entities/QaGovernanceAutomation.cs',
    'src/Dip.Domain/Features/Identity/Entities/QaGovernanceHardeningEntities.cs',
    'src/Dip.Application/Features/QualityGovernance/IQaGovernanceExecutionCoordinator.cs',
    'src/Dip.Application/Features/QualityGovernance/QaGovernanceCoreEngine.cs',
    'src/Dip.Application/Features/QualityGovernance/QaGovernanceAdministrationService.cs',
    'src/Dip.Application/Features/QualityGovernance/QaGovernanceRunProcessor.cs',
    'src/Dip.Application/Features/QualityGovernance/QaGovernanceQueryService.cs',
    'src/Dip.Api/Authorization/ProjectScopeAuthorization.cs',
    'src/Dip.Api/Authorization/ProductionSecurityGuard.cs',
    'src/Dip.Infrastructure/Persistence/QaGovernanceExecutionCoordinator.cs',
    'src/Dip.Infrastructure/Persistence/Configurations/QaGovernanceConfigurations.cs',
    'src/Dip.Infrastructure/Persistence/Configurations/QaGovernanceHardeningConfigurations.cs',
    'src/Dip.Infrastructure/Persistence/Repositories/QaGovernanceRepository.cs',
    'src/Dip.Api/Controllers/QaGovernanceController.cs',
    'scripts/Verify-DatabaseMigrations.ps1',
    'scripts/Verify-DisasterRecovery.ps1',
    'scripts/Run-GovernanceLoadTest.ps1'
)

foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf)) {
        throw "Missing QA Governance source: $relative"
    }
}

$out = Join-Path $root 'artifacts/qa-governance-verification'
$testOut = Join-Path $out 'TestResults'
New-Item -ItemType Directory -Force -Path $testOut | Out-Null
Remove-Item (Join-Path $out 'QA_GOVERNANCE_VERIFICATION_PASS.txt') -Force -ErrorAction SilentlyContinue

Invoke-DipDotNetTestStrict `
    -Project (Join-Path $root 'tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj') `
    -Configuration $Configuration `
    -Filter 'FullyQualifiedName~QualityGovernance' `
    -ResultsDirectory $testOut `
    -Name 'qa-governance-domain' `
    -MinimumExecuted 1 |
Out-Null

Invoke-DipDotNetTestStrict `
    -Project (Join-Path $root 'tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj') `
    -Configuration $Configuration `
    -Filter 'FullyQualifiedName~MigrationMetadataTests|FullyQualifiedName~GovernanceHardeningTests|FullyQualifiedName~UniversalDiagnosticNormalizerTests|FullyQualifiedName~CppDiagnosticNormalizationTests|FullyQualifiedName~ProcessExecutorSandboxTests' `
    -ResultsDirectory $testOut `
    -Name 'qa-governance-infrastructure' `
    -MinimumExecuted 1 |
Out-Null

$sha = (& git -C $root rev-parse HEAD | Out-String).Trim()

$payload = [ordered]@{
    schema = 'dip-qa-governance-verification-v5'
    sourceCommit = $sha
    verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    configuration = $Configuration
    testSemantics = 'fail-closed-trx-nonzero-executed'
    sourceLayout = 'responsibility-split'
}

$payload |
    ConvertTo-Json -Depth 6 |
    Set-Content (Join-Path $out 'qa-governance-verification.json') -Encoding UTF8

@(
    'QA GOVERNANCE VERIFICATION PASS',
    "Commit=$sha",
    "VerifiedAtUtc=$($payload.verifiedAtUtc)",
    "Configuration=$Configuration",
    'StrictTestGate=PASS'
) | Set-Content (Join-Path $out 'QA_GOVERNANCE_VERIFICATION_PASS.txt') -Encoding UTF8

Write-Host "QA Governance verification PASS ($sha)" -ForegroundColor Green
