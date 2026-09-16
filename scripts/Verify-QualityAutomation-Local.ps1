# Purpose: Automates the V er if y-Q ua li ty Au to ma ti on-L oc al repository workflow.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repo
try {
    Write-Host 'DIP Phase 4 local verification' -ForegroundColor Cyan

    & (Join-Path $PSScriptRoot 'Generate-QualityAutomation-Migration.ps1')

    dotnet restore DevelopmentIntelligencePlatform.sln
    if ($LASTEXITCODE -ne 0) { throw 'solution restore failed.' }
    dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'server solution Release build failed.' }

    dotnet restore 'src\Dip.Agent.Windows\Dip.Agent.Windows.csproj'
    if ($LASTEXITCODE -ne 0) { throw 'agent restore failed.' }
    dotnet build 'src\Dip.Agent.Windows\Dip.Agent.Windows.csproj' -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'agent build failed.' }

    dotnet test 'tests\Dip.Application.Tests\Dip.Application.Tests.csproj' -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Application tests failed.' }

    dotnet restore 'tests\Dip.Agent.Core.Tests\Dip.Agent.Core.Tests.csproj'
    if ($LASTEXITCODE -ne 0) { throw 'Agent Core tests restore failed.' }
    dotnet test 'tests\Dip.Agent.Core.Tests\Dip.Agent.Core.Tests.csproj' -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Agent Core tests failed.' }

    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($python) {
        & $python.Source -m py_compile 'plugins\Maya\DipMayaPlugin\dip_maya_plugin.py'
        if ($LASTEXITCODE -ne 0) { throw 'Maya plug-in Python syntax validation failed.' }
        Write-Host '[PASS] Maya Python syntax check' -ForegroundColor Green
    }
    else { Write-Host '[WARN] python not found; Maya syntax check skipped.' -ForegroundColor Yellow }

    $required = @(
        'src\Dip.Application\Features\QualityAutomation\VisualQa\VisualQaService.cs',
        'src\Dip.Application\Features\QualityAutomation\VisualQa\VisualComparator.cs',
        'src\Dip.Application\Features\QualityAutomation\AssetQa\AssetQaRuleEngine.cs',
        'src\Dip.Application\Features\ProjectTruth\ClaimEvidenceAssessmentService.cs',
        'src\Dip.Application\Features\Management\ProducerIntelligenceService.cs',
        'src\Dip.Web\Pages\Dashboard\QualityAutomation.razor',
        'src\Dip.Web\Pages\Dashboard\ProducerIntelligence.razor',
        'src\Dip.Web\Pages\Dashboard\ClaimAssessments.razor',
        'plugins\Unity\com.dip.collector\Editor\DipCollector.cs',
        'plugins\Unreal\DIPCollector\Source\DIPCollector\Private\DIPCollectorModule.cpp'
    )
    foreach ($path in $required) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing Phase 4 file: $path" } }

    Write-Host ''
    Write-Host '[PASS] Phase 4 compile/unit/static verification completed.' -ForegroundColor Green
    Write-Host '[INFO] Unity and Unreal plug-ins still require compilation in the actual target editor versions.' -ForegroundColor Yellow
    Write-Host '[INFO] No commit, push, or GitHub Actions rerun was performed.' -ForegroundColor Green
}
finally { Pop-Location }
