# Purpose: Local GitHub CI preflight with fail-closed .NET test evidence and optional Docker primary verification.
[CmdletBinding()]
param(
    [switch]$SkipGui,
    [switch]$SkipIntegration,
    [switch]$SkipSecurity,
    [switch]$SkipNative,
    [switch]$SkipPython,
    [switch]$SkipDotNet,
    [switch]$SkipPortfolioStatic,
    [switch]$ContinueOnFailure,
    [switch]$UseDockerPrimary
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'Invoke-DotNetTestStrict.ps1')

$OutDir = Join-Path $Root 'artifacts\github-ci-preflight'
$LogDir = Join-Path $OutDir 'logs'
$ReportPath = Join-Path $OutDir 'report.json'
$SummaryPath = Join-Path $OutDir 'SUMMARY.md'
$TestResults = Join-Path $OutDir 'TestResults'

New-Item -ItemType Directory -Force -Path $LogDir,$TestResults | Out-Null

$results = [System.Collections.Generic.List[object]]::new()
$started = [DateTimeOffset]::UtcNow
$sourceCommit = ''
try { $sourceCommit = (git -C $Root rev-parse HEAD 2>$null).Trim() } catch { $sourceCommit = 'UNKNOWN' }

function Add-Result {
    param([string]$Name,[string]$Status,[double]$Seconds,[string]$Log,[string]$Message = '')
    $results.Add([pscustomobject]@{
        name=$Name
        status=$Status
        seconds=[math]::Round($Seconds,3)
        log=$Log
        message=$Message
    }) | Out-Null
}

function Invoke-Phase {
    param([Parameter(Mandatory=$true)][string]$Name,[Parameter(Mandatory=$true)][scriptblock]$Script)

    $safe = ($Name -replace '[^A-Za-z0-9._-]','_')
    $logPath = Join-Path $LogDir "$safe.log"
    $sw = [Diagnostics.Stopwatch]::StartNew()

    Write-Host "`n=== $Name ===" -ForegroundColor Cyan
    try {
        & $Script *>&1 | Tee-Object -FilePath $logPath
        $sw.Stop()
        Add-Result $Name 'PASS' $sw.Elapsed.TotalSeconds $logPath
        Write-Host "PASS: $Name" -ForegroundColor Green
    }
    catch {
        $sw.Stop()
        $msg = $_.Exception.Message
        $msg | Tee-Object -FilePath $logPath -Append | Out-Host
        Add-Result $Name 'FAIL' $sw.Elapsed.TotalSeconds $logPath $msg
        Write-Host "FAIL: $Name" -ForegroundColor Red
        if(-not $ContinueOnFailure){ throw }
    }
}

function Require-Command([string]$Name) {
    if(-not(Get-Command $Name -ErrorAction SilentlyContinue)){
        throw "Required command is not available: $Name"
    }
}

Push-Location $Root
try {
    Invoke-Phase 'Environment' {
        Require-Command git
        Require-Command python
        if(-not $UseDockerPrimary){ Require-Command dotnet }
        if(-not(Test-Path 'DevelopmentIntelligencePlatform.sln')){ throw 'Solution file is missing.' }
    }

    if($UseDockerPrimary){
        Invoke-Phase 'Docker primary exact-revision verification' {
            & (Join-Path $PSScriptRoot 'Verify-DockerPrimary.ps1') -RepositoryRoot $Root -ExpectedSha $sourceCommit -SkipPlaywright:$SkipGui
            if($LASTEXITCODE -ne 0){ throw 'Docker primary verification failed.' }
        }
    }
    else {
        if(-not $SkipPortfolioStatic){
            Invoke-Phase 'Portfolio publication safety' {
                python tools/verify_publication_safety.py --root . --output artifacts/publication-safety/publication-safety.json
                if($LASTEXITCODE -ne 0){ throw 'verify_publication_safety.py failed.' }
                python tools/validate_portfolio_readiness.py --root . --output artifacts/portfolio-readiness/static.json
                if($LASTEXITCODE -ne 0){ throw 'validate_portfolio_readiness.py failed.' }
            }
        }

        if(-not $SkipDotNet){
            Invoke-Phase '.NET restore' {
                dotnet restore DevelopmentIntelligencePlatform.sln
                if($LASTEXITCODE -ne 0){ throw '.NET restore failed.' }
            }

            Invoke-Phase '.NET build Release' {
                dotnet build DevelopmentIntelligencePlatform.sln --configuration Release --no-restore
                if($LASTEXITCODE -ne 0){ throw '.NET build failed.' }
            }

            foreach($entry in @(
                @('tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj','Dip.Domain.Tests'),
                @('tests/Dip.Application.Tests/Dip.Application.Tests.csproj','Dip.Application.Tests'),
                @('tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj','Dip.Infrastructure.Tests')
            )){
                Invoke-Phase ".NET test $($entry[1])" {
                    Invoke-DipDotNetTestStrict `
                        -Project $entry[0] `
                        -Configuration Release `
                        -ResultsDirectory $TestResults `
                        -Name $entry[1] `
                        -NoBuild `
                        -MinimumExecuted 1 |
                    Out-Null
                }
            }
        }

        if(-not $SkipPython){
            Invoke-Phase 'Python analysis CI' {
                Push-Location 'services/python-analysis'
                try {
                    python -m compileall app tests
                    if($LASTEXITCODE -ne 0){ throw 'Python compileall failed.' }
                    python -m pytest --junitxml=../../artifacts/github-ci-preflight/python-test-results.xml
                    if($LASTEXITCODE -ne 0){ throw 'pytest failed.' }
                }
                finally { Pop-Location }
            }
        }

        if(-not $SkipIntegration){
            Invoke-Phase 'Database migrations' {
                & (Join-Path $PSScriptRoot 'Verify-DatabaseMigrations.ps1')
                if($LASTEXITCODE -ne 0){ throw 'Database migration verification failed.' }
            }

            Invoke-Phase 'Integration and E2E' {
                & (Join-Path $PSScriptRoot 'Verify-Solution.ps1') -Configuration Release
                if($LASTEXITCODE -ne 0){ throw 'Verify-Solution.ps1 failed.' }
            }
        }

        if(-not $SkipGui){
            Invoke-Phase 'GUI E2E / Playwright' {
                & (Join-Path $PSScriptRoot 'Verify-GuiRuntime.ps1') -RunPlaywright -StartupTimeoutSeconds 900
                if($LASTEXITCODE -ne 0){ throw 'GUI runtime verification failed.' }
            }
        }
    }

    if(-not $SkipSecurity){
        Invoke-Phase '.NET dependency vulnerability audit' {
            if($UseDockerPrimary){
                Write-Host 'Covered by Docker primary dependency audit.'
            } else {
                $audit = & dotnet list DevelopmentIntelligencePlatform.sln package --vulnerable --include-transitive 2>&1
                $code = $LASTEXITCODE
                $audit | Out-Host
                if($code -ne 0){ throw 'NuGet vulnerability audit failed.' }
                if(($audit | Out-String) -match 'has the following vulnerable packages'){
                    throw 'Vulnerable NuGet package(s) detected.'
                }
            }
        }
    }
}
catch {
    Write-Host "`nPreflight stopped: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Pop-Location

    $finished = [DateTimeOffset]::UtcNow
    $nonPass = @($results | Where-Object { $_.status -ne 'PASS' })
    $status = if($nonPass.Count -eq 0){ 'PASS' } else { 'FAIL' }

    [ordered]@{
        schema='dip.github-ci-preflight.v2'
        sourceCommit=$sourceCommit
        startedAtUtc=$started.ToString('o')
        finishedAtUtc=$finished.ToString('o')
        status=$status
        dockerPrimary=[bool]$UseDockerPrimary
        strictZeroTestPolicy=$true
        phases=$results
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding UTF8

    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('# GitHub CI local preflight') | Out-Null
    $lines.Add('') | Out-Null
    $lines.Add("- Source commit: ``$sourceCommit``") | Out-Null
    $lines.Add("- Status: **$status**") | Out-Null
    $lines.Add("- Docker primary: **$([bool]$UseDockerPrimary)**") | Out-Null
    $lines.Add("- Strict zero-test policy: **enabled**") | Out-Null
    $lines.Add('') | Out-Null
    $lines.Add('| Phase | Status | Seconds |') | Out-Null
    $lines.Add('|---|---:|---:|') | Out-Null

    foreach($r in $results){
        $lines.Add("| $($r.name) | $($r.status) | $($r.seconds) |") | Out-Null
    }

    $lines | Set-Content -LiteralPath $SummaryPath -Encoding UTF8

    if($status -ne 'PASS'){ exit 1 }
}
