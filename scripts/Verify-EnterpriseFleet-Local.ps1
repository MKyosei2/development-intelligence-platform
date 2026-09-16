# Purpose: Automates the V er if y-E nt er pr is eF le et-L oc al repository workflow.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repo
try {
  Write-Host 'DIP Phase 5 local verification' -ForegroundColor Cyan
  & (Join-Path $PSScriptRoot 'Generate-EnterpriseFleet-Migration.ps1')
  dotnet restore DevelopmentIntelligencePlatform.sln; if($LASTEXITCODE -ne 0){throw 'solution restore failed.'}
  dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore; if($LASTEXITCODE -ne 0){throw 'server Release build failed.'}
  dotnet restore 'src\Dip.Agent.Windows\Dip.Agent.Windows.csproj'; if($LASTEXITCODE -ne 0){throw 'agent restore failed.'}
  dotnet build 'src\Dip.Agent.Windows\Dip.Agent.Windows.csproj' -c Release --no-restore; if($LASTEXITCODE -ne 0){throw 'agent build failed.'}
  dotnet test 'tests\Dip.Application.Tests\Dip.Application.Tests.csproj' -c Release --no-restore; if($LASTEXITCODE -ne 0){throw 'Application tests failed.'}
  dotnet restore 'tests\Dip.Agent.Core.Tests\Dip.Agent.Core.Tests.csproj'; if($LASTEXITCODE -ne 0){throw 'Agent tests restore failed.'}
  dotnet test 'tests\Dip.Agent.Core.Tests\Dip.Agent.Core.Tests.csproj' -c Release --no-restore; if($LASTEXITCODE -ne 0){throw 'Agent tests failed.'}
  $required=@('src\Dip.Application\Features\Fleet\AgentFleetService.cs','src\Dip.Application\Features\Enterprise\EvidenceIntegrityService.cs','src\Dip.Application\Features\Enterprise\DistributedIngestionService.cs','src\Dip.Application\Features\Enterprise\AgentPolicyDistributionService.cs','src\Dip.Infrastructure\Security\EnterpriseIntegrationCredentialStore.cs','src\Dip.Web\Pages\Admin\Fleet.razor','src\Dip.Web\Pages\Admin\EnterpriseGovernance.razor')
  foreach($path in $required){if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw "Missing Phase 5 file: $path"}}
  Write-Host '[PASS] Phase 5 compile/unit/static verification completed.' -ForegroundColor Green
  Write-Host '[INFO] No commit, push, reset, restore or GitHub Actions rerun was performed.' -ForegroundColor Green
} finally {Pop-Location}
