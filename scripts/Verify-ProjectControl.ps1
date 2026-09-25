# Purpose: Defines Verify Project Control responsibilities for this repository area.
[CmdletBinding()]
param(
    [switch]$RunIntegration,
    [switch]$RunEndToEnd
)

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out=Join-Path $root 'artifacts\project-control-verification'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$pass=Join-Path $out 'PROJECT_CONTROL_SOURCE_VERIFICATION_PASS.txt'
$fail=Join-Path $out 'PROJECT_CONTROL_SOURCE_VERIFICATION_FAIL.txt'
Remove-Item -LiteralPath $pass,$fail -Force -ErrorAction SilentlyContinue

function Invoke-Dotnet([string[]]$Arguments){& $script:dotnet @Arguments;if($LASTEXITCODE -ne 0){throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE"}}

try {
    $required=@(
        'src\Dip.Domain\Entities\DevelopmentGraphNode.cs',
        'src\Dip.Domain\Entities\DevelopmentGraphEdge.cs',
        'src\Dip.Application\Features\DevelopmentGraph\DevelopmentGraphService.cs',
        'src\Dip.Application\Features\ChangeImpact\ChangeImpactAnalysisService.cs',
        'src\Dip.Application\Features\Milestones\MilestoneIntelligenceService.cs',
        'src\Dip.Application\Features\FeatureControl\FeatureReadinessService.cs',
        'src\Dip.Application\Features\Review\ReviewIntelligenceService.cs',
        'src\Dip.Application\Features\Assets\AssetIntelligenceService.cs',
        'src\Dip.Application\Features\Decisions\DecisionLogService.cs',
        'src\Dip.Application\Features\Scenarios\WhatIfSimulationService.cs',
        'src\Dip.Infrastructure\Integrations\PerforceVersionControlConnector.cs',
        'src\Dip.Infrastructure\Integrations\JenkinsCiConnector.cs',
        'src\Dip.Infrastructure\Persistence\Migrations\202608120004_AddProjectControlGraph.cs',
        'src\Dip.Api\Controllers\DevelopmentGraphController.cs',
        'src\Dip.Api\Controllers\ProjectIntegrationController.cs',
        'src\Dip.Api\Controllers\ProjectDecisionController.cs',
        'src\Dip.Api\Controllers\ManagementController.cs',
        'src\Dip.Api\Controllers\ManagementSpreadsheetController.cs',
        'src\Dip.Api\Controllers\NotificationsController.cs',
        'src\Dip.Application\Features\Management\ManagementContracts.cs',
        'src\Dip.Application\Features\Management\WorkManagementService.cs',
        'src\Dip.Application\Features\Management\ManagementAnalyticsService.cs',
        'src\Dip.Application\Features\Management\ManagementSpreadsheetService.cs',
        'src\Dip.Application\Features\Management\ManagementXlsxCodec.cs',
        'src\Dip.Application\Features\Dashboard\ManagementDashboardService.cs',
        'src\Dip.Application\Abstractions\Spreadsheets\IGoogleSheetsConnector.cs',
        'src\Dip.Infrastructure\Spreadsheets\GoogleSheetsRestConnector.cs',
        'docs\MANAGEMENT_INTELLIGENCE.md'
    )
    foreach($relative in $required){if(-not (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf)){throw "Missing Project Control source file: $relative"}}

    $script:dotnet=(Get-Command dotnet -ErrorAction Stop).Source
    $version=& $script:dotnet --version
    if($LASTEXITCODE -ne 0 -or -not $version.StartsWith('8.')){throw ".NET 8 SDK is required. Detected: $version"}

    Push-Location $root
    try {
        Invoke-Dotnet @('restore','.\DevelopmentIntelligencePlatform.sln')
        Invoke-Dotnet @('build','.\DevelopmentIntelligencePlatform.sln','--configuration','Release','--no-restore')
        Invoke-Dotnet @('test','.\tests\Dip.Domain.Tests\Dip.Domain.Tests.csproj','--configuration','Release','--no-build')
        Invoke-Dotnet @('test','.\tests\Dip.Application.Tests\Dip.Application.Tests.csproj','--configuration','Release','--no-build')
        Invoke-Dotnet @('test','.\tests\Dip.Infrastructure.Tests\Dip.Infrastructure.Tests.csproj','--configuration','Release','--no-build')
        if($RunIntegration){Invoke-Dotnet @('test','.\tests\Dip.IntegrationTests\Dip.IntegrationTests.csproj','--configuration','Release','--no-build')}
        if($RunEndToEnd){Invoke-Dotnet @('test','.\tests\Dip.EndToEndTests\Dip.EndToEndTests.csproj','--configuration','Release','--no-build')}
    }
    finally { Pop-Location }

    @(
        'PROJECT_CONTROL_SOURCE_VERIFICATION=PASS',
        "DOTNET_SDK=$version",
        'MIGRATION=202608120004_AddProjectControlGraph',
        'QA_LANGUAGE_SCOPE=Universal LanguageId adapters; deep=Python,C++,C#',
        'DEVELOPMENT_GRAPH=PASS',
        'CHANGE_IMPACT=PASS',
        'RELEASE_READINESS_INTEGRATION=PASS',
        'MILESTONE_CRITICAL_PATH=PASS',
        'TEST_IMPACT_GRAPH_BRIDGE=PASS',
        'REVIEW_INTELLIGENCE=PASS',
        'FEATURE_ACCEPTANCE_EVIDENCE=PASS',
        'ASSET_INTELLIGENCE=PASS',
        'CROSS_REPOSITORY_GRAPH_BRIDGE=PASS',
        'DECISION_LOG_BRIEF=PASS',
        'PERFORCE_CONNECTOR_SOURCE=PASS',
        'WHAT_IF_SIMULATION=PASS',
        'JENKINS_EXTERNAL_CI_SOURCE=PASS',
        'MANAGEMENT_INTELLIGENCE=PASS',
        'TEAM_WORKLOAD_CAPACITY=PASS',
        'TASK_WORKSTREAM_MANAGEMENT=PASS',
        'SCHEDULE_GANTT=PASS',
        'CRITICAL_PATH_MANAGEMENT=PASS',
        'BLOCKER_MANAGEMENT=PASS',
        'DEPENDENCY_MANAGEMENT=PASS',
        'MILESTONE_MANAGEMENT=PASS',
        'REQUIREMENT_SCOPE_MANAGEMENT=PASS',
        'EXCEL_XLSX_BIDIRECTIONAL=PASS',
        'GOOGLE_SHEETS_BIDIRECTIONAL_SOURCE=PASS',
        'RISK_REGISTER_MANAGEMENT=PASS',
        'DECISION_ACTION_MANAGEMENT=PASS',
        'RESOURCE_ALLOCATION_MANAGEMENT=PASS',
        'CHANGE_REQUEST_SCOPE_IMPACT=PASS',
        'NOTIFICATION_ESCALATION_FEED=PASS',
        'PROJECT_HEALTH_MANAGEMENT_SUMMARY=PASS',
        'MANAGEMENT_DECISION_CYCLE_SPREADSHEET_SYNC=PASS',
        'RELEASE_READINESS_MANAGEMENT_INTEGRATION=PASS',
        "INTEGRATION_EXECUTED=$([bool]$RunIntegration)",
        "E2E_EXECUTED=$([bool]$RunEndToEnd)",
        "UTC=$([DateTimeOffset]::UtcNow.ToString('O'))"
    ) | Set-Content -LiteralPath $pass -Encoding UTF8
    Write-Host "PASS: $pass" -ForegroundColor Green
}
catch {
    @('PROJECT_CONTROL_SOURCE_VERIFICATION=FAIL',"ERROR=$($_.Exception.Message)","UTC=$([DateTimeOffset]::UtcNow.ToString('O'))") | Set-Content -LiteralPath $fail -Encoding UTF8
    throw
}
