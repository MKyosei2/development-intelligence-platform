# Purpose: Verifies QA governance domain, persistence, authorization, operations, observability, and responsibility-split source boundaries.
[CmdletBinding()]
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

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
    'src/Dip.Application/Features/Authorization/IProjectMutationCoordinator.cs',
    'src/Dip.Application/Features/Authorization/ProjectResourceAuthorization.cs',
    'src/Dip.Infrastructure/Persistence/ProjectMutationCoordinator.cs',
    'src/Dip.Infrastructure/Persistence/ProjectResourceProjectResolver.cs',
    'src/Dip.Infrastructure/Persistence/QaGovernanceExecutionCoordinator.cs',
    'src/Dip.Infrastructure/Persistence/Configurations/QaGovernanceConfigurations.cs',
    'src/Dip.Infrastructure/Persistence/Configurations/QaGovernanceHardeningConfigurations.cs',
    'src/Dip.Infrastructure/Persistence/Repositories/QaGovernanceRepository.cs',
    'src/Dip.Infrastructure/Persistence/Migrations/20260814000600_AddQaGovernance.cs',
    'src/Dip.Infrastructure/Persistence/Migrations/20260814000700_QaGovernanceHardening.cs',
    'src/Dip.Infrastructure/QualityAutomation/QaBugMinimizer.cs',
    'src/Dip.Infrastructure/QualityAutomation/CodeOwnersOwnershipResolver.cs',
    'src/Dip.Api/Controllers/QaGovernanceController.cs',
    'scripts/Backup-Dip.ps1',
    'scripts/Restore-Dip.ps1',
    'scripts/Bootstrap-ProjectMemberships.ps1',
    'scripts/Verify-DatabaseMigrations.ps1',
    'scripts/Verify-DisasterRecovery.ps1',
    'scripts/Run-GovernanceLoadTest.ps1',
    'infrastructure/monitoring/grafana/dashboards/dip-qa-governance.json',
    'infrastructure/monitoring/prometheus/qa-governance-rules.yml'
)

foreach ($relative in $required) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing QA Governance source: $relative"
    }
}

$splitMarkers = @{
    'src/Dip.Domain/Features/Evidence/Entities/QaFindingLifecycle.cs' = @('QaFindingState', 'FindingLifecycleStatus')
    'src/Dip.Domain/Features/Evidence/Entities/QaWaiver.cs' = @('QaWaiver', 'WaiverStatus')
    'src/Dip.Domain/Features/Evidence/Entities/QaReliability.cs' = @('QaTestReliability', 'QaRegressionBaseline')
    'src/Dip.Domain/Features/Evidence/Entities/QaPolicy.cs' = @('QaPolicy', 'QaPolicyEvaluation', 'QaOwnershipRule')
    'src/Dip.Domain/Features/Evidence/Entities/QaProvenance.cs' = @('QaEvidenceProvenance', 'QaSystemHealthSnapshot', 'QaQualityTrendSnapshot')
    'src/Dip.Domain/Features/Evidence/Entities/QaGovernanceAutomation.cs' = @('QaTestImpactRule', 'QaArtifactRetentionRule', 'QaRunCostRecord', 'QaBugMinimizationRecord')
}

foreach ($entry in $splitMarkers.GetEnumerator()) {
    $path = Join-Path $root $entry.Key
    $raw = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    foreach ($marker in $entry.Value) {
        if (-not $raw.Contains($marker)) {
            throw "QA Governance responsibility marker '$marker' is missing from $($entry.Key)."
        }
    }
}

$out = Join-Path $root 'artifacts/qa-governance-verification'
New-Item -ItemType Directory -Force -Path $out | Out-Null
Remove-Item (Join-Path $out 'QA_GOVERNANCE_VERIFICATION_PASS.txt') -Force -ErrorAction SilentlyContinue

& dotnet test (Join-Path $root 'tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj') `
    -c $Configuration `
    --filter 'FullyQualifiedName~QualityGovernance'
if ($LASTEXITCODE -ne 0) {
    throw 'QA Governance domain tests failed.'
}

& dotnet test (Join-Path $root 'tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj') `
    -c $Configuration `
    --filter 'FullyQualifiedName~MigrationMetadataTests|FullyQualifiedName~GovernanceHardeningTests|FullyQualifiedName~UniversalDiagnosticNormalizerTests|FullyQualifiedName~ProcessExecutorSandboxTests'
if ($LASTEXITCODE -ne 0) {
    throw 'QA Governance persistence/diagnostic tests failed.'
}

$sha = (& git -C $root rev-parse HEAD | Out-String).Trim()

$payload = [ordered]@{
    schema = 'dip-qa-governance-verification-v4'
    sourceCommit = $sha
    verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    configuration = $Configuration
    sourceLayout = 'responsibility-split'
    checks = @(
        'Project-scoped authorization source',
        'Indirect resource ownership authorization including artifact downloads',
        'Server-derived audit actor',
        'Project mutation serialization',
        'Finding lifecycle/two-person waiver',
        'Policy lifecycle',
        'Release override',
        'Exactly-once run ledger',
        'Optimistic concurrency',
        'Governance migration/model discovery',
        'Diagnostic schema versioning',
        'Sandbox boundary/Windows Job Object',
        'Operational backup/restore/bootstrap scripts',
        'Governance observability dashboard and alerts',
        'Responsibility-split QA governance source layout'
    )
}

$payload |
    ConvertTo-Json -Depth 6 |
    Set-Content (Join-Path $out 'qa-governance-verification.json') -Encoding UTF8

@(
    'QA GOVERNANCE VERIFICATION PASS',
    "Commit=$sha",
    "VerifiedAtUtc=$($payload.verifiedAtUtc)",
    "Configuration=$Configuration",
    'SourceLayout=responsibility-split'
) | Set-Content (Join-Path $out 'QA_GOVERNANCE_VERIFICATION_PASS.txt') -Encoding UTF8

Write-Host "QA Governance verification PASS ($sha)" -ForegroundColor Green
