# Purpose: Verifies Project Control source wiring and its .NET test surface with fail-closed test evidence.
[CmdletBinding()]
param(
    [switch]$RunIntegration,
    [switch]$RunEndToEnd
)

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Invoke-DotNetTestStrict.ps1')

$out=Join-Path $root 'artifacts\project-control-verification'
$testOut=Join-Path $out 'TestResults'
New-Item -ItemType Directory -Force -Path $out,$testOut | Out-Null

$pass=Join-Path $out 'PROJECT_CONTROL_SOURCE_VERIFICATION_PASS.txt'
$fail=Join-Path $out 'PROJECT_CONTROL_SOURCE_VERIFICATION_FAIL.txt'
Remove-Item -LiteralPath $pass,$fail -Force -ErrorAction SilentlyContinue

function Invoke-Dotnet([string[]]$Arguments){
    & $script:dotnet @Arguments
    if($LASTEXITCODE -ne 0){
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Get-FreeTcpPort {
    $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
    $listener.Start()
    try{return ([Net.IPEndPoint]$listener.LocalEndpoint).Port}
    finally{$listener.Stop()}
}

function Invoke-LiveEndToEnd {
    $verifySolution=Join-Path $PSScriptRoot 'Verify-Solution.ps1'
    if(-not(Test-Path -LiteralPath $verifySolution -PathType Leaf)){
        throw 'Verify-Solution.ps1 is missing.'
    }

    $hostName=if($env:POSTGRES_HOST){$env:POSTGRES_HOST}else{'127.0.0.1'}
    $port=if($env:POSTGRES_PORT){[int]$env:POSTGRES_PORT}else{5432}
    $user=if($env:POSTGRES_USER){$env:POSTGRES_USER}else{'dip'}
    $password=if($env:POSTGRES_PASSWORD){$env:POSTGRES_PASSWORD}else{'change-me-local-only'}
    $apiPort=Get-FreeTcpPort

    $engine = if (Get-Command powershell.exe -ErrorAction SilentlyContinue) {
        'powershell.exe'
    } elseif (Get-Command pwsh -ErrorAction SilentlyContinue) {
        'pwsh'
    } else {
        throw 'PowerShell runtime is unavailable.'
    }

    & $engine -NoProfile -ExecutionPolicy Bypass -File $verifySolution `
        -Configuration Release `
        -PostgreSqlHost $hostName `
        -PostgreSqlPort $port `
        -PostgreSqlUser $user `
        -PostgreSqlPassword $password `
        -ApiPort $apiPort

    if($LASTEXITCODE -ne 0){
        throw "Live end-to-end verification failed with exit code $LASTEXITCODE."
    }
}

try {
    $required=@(
        'src\Dip.Domain\Features\Projects\Entities\DevelopmentGraphNode.cs',
        'src\Dip.Domain\Features\Projects\Entities\DevelopmentGraphEdge.cs',
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
        'src\Dip.Application\Features\Management\DailyCockpitDto.cs',
        'src\Dip.Application\Features\Management\OperatingCadenceDto.cs',
        'src\Dip.Application\Features\Management\ManagementSyncCommand.cs',
        'src\Dip.Application\Features\Management\SyncStateOverviewDto.cs',
        'src\Dip.Application\Features\Management\SpreadsheetSchemaInference.cs',
        'src\Dip.Application\Features\Management\WorkManagementService.cs',
        'src\Dip.Application\Features\Management\ManagementAnalyticsService.cs',
        'src\Dip.Application\Features\Management\ManagementSpreadsheetService.cs',
        'src\Dip.Application\Features\Management\ManagementXlsxCodec.cs',
        'src\Dip.Application\Features\Dashboard\ManagementDashboardService.cs',
        'src\Dip.Application\Abstractions\Spreadsheets\IGoogleSheetsConnector.cs',
        'src\Dip.Infrastructure\Spreadsheets\GoogleSheetsRestConnector.cs',
        'docs\03-operations\COMMERCIAL_PROJECT_MANAGEMENT_AND_BRANCH_QA.md'
    )

    foreach($relative in $required){
        if(-not (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf)){
            throw "Missing Project Control source file: $relative"
        }
    }

    $script:dotnet=(Get-Command dotnet -ErrorAction Stop).Source
    $version=& $script:dotnet --version

    if($LASTEXITCODE -ne 0 -or -not $version.StartsWith('8.')){
        throw ".NET 8 SDK is required. Detected: $version"
    }

    Push-Location $root
    try {
        Invoke-Dotnet @('restore','.\DevelopmentIntelligencePlatform.sln')
        Invoke-Dotnet @('build','.\DevelopmentIntelligencePlatform.sln','--configuration','Release','--no-restore')

        foreach($entry in @(
            @('.\tests\Dip.Domain.Tests\Dip.Domain.Tests.csproj','project-control-domain'),
            @('.\tests\Dip.Application.Tests\Dip.Application.Tests.csproj','project-control-application'),
            @('.\tests\Dip.Infrastructure.Tests\Dip.Infrastructure.Tests.csproj','project-control-infrastructure')
        )){
            Invoke-DipDotNetTestStrict `
                -Project $entry[0] `
                -Configuration Release `
                -ResultsDirectory $testOut `
                -Name $entry[1] `
                -NoBuild `
                -MinimumExecuted 1 |
            Out-Null
        }

        if($RunIntegration){
            Invoke-DipDotNetTestStrict `
                -Project '.\tests\Dip.IntegrationTests\Dip.IntegrationTests.csproj' `
                -Configuration Release `
                -ResultsDirectory $testOut `
                -Name 'project-control-integration' `
                -NoBuild `
                -MinimumExecuted 1 |
            Out-Null
        }

        if($RunEndToEnd){
            Invoke-LiveEndToEnd
        }
    }
    finally {
        Pop-Location
    }

    @(
        'PROJECT_CONTROL_SOURCE_VERIFICATION=PASS',
        "DOTNET_SDK=$version",
        'STRICT_TEST_GATE=PASS',
        'MIGRATION=202608120004_AddProjectControlGraph',
        "INTEGRATION_EXECUTED=$([bool]$RunIntegration)",
        "E2E_EXECUTED=$([bool]$RunEndToEnd)",
        "UTC=$([DateTimeOffset]::UtcNow.ToString('O'))"
    ) | Set-Content -LiteralPath $pass -Encoding UTF8

    Write-Host "PASS: $pass" -ForegroundColor Green
}
catch {
    @(
        'PROJECT_CONTROL_SOURCE_VERIFICATION=FAIL',
        "ERROR=$($_.Exception.Message)",
        "UTC=$([DateTimeOffset]::UtcNow.ToString('O'))"
    ) | Set-Content -LiteralPath $fail -Encoding UTF8

    throw
}
