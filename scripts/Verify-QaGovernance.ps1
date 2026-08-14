[CmdletBinding()]
param([string]$Configuration='Release')
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
foreach($c in 'git','dotnet'){if(-not(Get-Command $c -ErrorAction SilentlyContinue)){throw "$c is required."}}
$required=@(
  'src\Dip.Domain\Enums\QaGovernanceEnums.cs',
  'src\Dip.Domain\Entities\QaGovernanceEntities.cs',
  'src\Dip.Application\Features\QualityGovernance\QaGovernanceContracts.cs',
  'src\Dip.Application\Features\QualityGovernance\QaGovernanceService.cs',
  'src\Dip.Infrastructure\Persistence\Configurations\QaGovernanceConfigurations.cs',
  'src\Dip.Infrastructure\Persistence\Repositories\QaGovernanceRepository.cs',
  'src\Dip.Infrastructure\Persistence\Migrations\202608140006_AddQaGovernance.cs',
  'src\Dip.Infrastructure\QualityAutomation\QaBugMinimizer.cs',
  'src\Dip.Infrastructure\QualityAutomation\CodeOwnersOwnershipResolver.cs',
  'src\Dip.Api\Controllers\QaGovernanceController.cs'
)
foreach($f in $required){if(-not(Test-Path (Join-Path $root $f) -PathType Leaf)){throw "Missing QA Governance source: $f"}}
$out=Join-Path $root 'artifacts\qa-governance-verification';New-Item -ItemType Directory -Force -Path $out|Out-Null
Remove-Item (Join-Path $out 'QA_GOVERNANCE_VERIFICATION_PASS.txt') -Force -ErrorAction SilentlyContinue
& dotnet test (Join-Path $root 'tests\Dip.Domain.Tests\Dip.Domain.Tests.csproj') -c $Configuration --filter 'FullyQualifiedName~QualityGovernance'
if($LASTEXITCODE-ne0){throw 'QA Governance domain tests failed.'}
& dotnet test (Join-Path $root 'tests\Dip.Infrastructure.Tests\Dip.Infrastructure.Tests.csproj') -c $Configuration --filter 'FullyQualifiedName~MigrationMetadataTests|FullyQualifiedName~UniversalDiagnosticNormalizerTests'
if($LASTEXITCODE-ne0){throw 'QA Governance persistence/diagnostic tests failed.'}
$sha=(& git -C $root rev-parse HEAD|Out-String).Trim()
$payload=[ordered]@{schema='dip-qa-governance-verification-v1';sourceCommit=$sha;verifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');configuration=$Configuration;checks=@('Finding lifecycle/waiver','Test reliability/flaky classification','Regression baseline','Governance migration/model discovery','Diagnostic schema versioning')}
$payload|ConvertTo-Json -Depth 6|Set-Content (Join-Path $out 'qa-governance-verification.json') -Encoding UTF8
@('QA GOVERNANCE VERIFICATION PASS',"Commit=$sha","VerifiedAtUtc=$($payload.verifiedAtUtc)","Configuration=$Configuration")|Set-Content (Join-Path $out 'QA_GOVERNANCE_VERIFICATION_PASS.txt') -Encoding UTF8
Write-Host "QA Governance verification PASS ($sha)" -ForegroundColor Green
