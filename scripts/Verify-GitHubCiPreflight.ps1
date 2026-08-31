[CmdletBinding()]
param(
    [switch]$SkipGui,
    [switch]$SkipIntegration,
    [switch]$SkipSecurity,
    [switch]$SkipNative,
    [switch]$SkipPython,
    [switch]$SkipDotNet,
    [switch]$SkipPortfolioStatic,
    [switch]$ContinueOnFailure
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$OutDir = Join-Path $Root 'artifacts\github-ci-preflight'
$LogDir = Join-Path $OutDir 'logs'
$ReportPath = Join-Path $OutDir 'report.json'
$SummaryPath = Join-Path $OutDir 'SUMMARY.md'

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

$results = [System.Collections.Generic.List[object]]::new()
$started = [DateTimeOffset]::UtcNow
$sourceCommit = ''
try { $sourceCommit = (git -C $Root rev-parse HEAD 2>$null).Trim() } catch { $sourceCommit = 'UNKNOWN' }

function Add-Result {
    param(
        [string]$Name,
        [string]$Status,
        [double]$Seconds,
        [string]$Log,
        [string]$Message = ''
    )
    $results.Add([pscustomobject]@{
        name = $Name
        status = $Status
        seconds = [math]::Round($Seconds, 3)
        log = $Log
        message = $Message
    }) | Out-Null
}

function Invoke-Phase {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Script
    )

    $safe = ($Name -replace '[^A-Za-z0-9._-]', '_')
    $logPath = Join-Path $LogDir "$safe.log"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    Write-Host "`n=== $Name ===" -ForegroundColor Cyan
    try {
        & $Script *>&1 | Tee-Object -FilePath $logPath
        if ($LASTEXITCODE -ne 0) {
            throw "$Name returned exit code $LASTEXITCODE."
        }
        $sw.Stop()
        Add-Result -Name $Name -Status 'PASS' -Seconds $sw.Elapsed.TotalSeconds -Log $logPath
        Write-Host "PASS: $Name" -ForegroundColor Green
    }
    catch {
        $sw.Stop()
        $msg = $_.Exception.Message
        $msg | Tee-Object -FilePath $logPath -Append | Out-Host
        Add-Result -Name $Name -Status 'FAIL' -Seconds $sw.Elapsed.TotalSeconds -Log $logPath -Message $msg
        Write-Host "FAIL: $Name" -ForegroundColor Red
        if (-not $ContinueOnFailure) {
            throw
        }
    }
}

function Require-Command {
    param([Parameter(Mandatory)][string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command is not available: $Name"
    }
}

Push-Location $Root
try {
    Invoke-Phase 'Environment' {
        Require-Command git
        Require-Command dotnet
        Require-Command python
        Require-Command pwsh

        $dotnetVersion = (dotnet --version).Trim()
        if (-not $dotnetVersion.StartsWith('8.')) {
            throw "GitHub CI expects .NET 8.x but local dotnet is $dotnetVersion."
        }

        $pythonVersion = (& python -c "import sys; print('.'.join(map(str,sys.version_info[:2])))").Trim()
        if ($pythonVersion -ne '3.12') {
            throw "GitHub CI expects Python 3.12 but local Python is $pythonVersion."
        }

        if (-not (Test-Path 'global.json')) { throw 'global.json is missing.' }
        if (-not (Test-Path 'DevelopmentIntelligencePlatform.sln')) { throw 'Solution file is missing.' }
    }

    if (-not $SkipPortfolioStatic) {
        Invoke-Phase 'Portfolio publication safety' {
            python tools/verify_publication_safety.py --root . --output artifacts/publication-safety/publication-safety.json
            if ($LASTEXITCODE -ne 0) { throw 'verify_publication_safety.py failed.' }
            python tools/validate_portfolio_readiness.py --root . --output artifacts/portfolio-readiness/static.json
            if ($LASTEXITCODE -ne 0) { throw 'validate_portfolio_readiness.py failed.' }

            $readme = Get-Content README.md -Raw
            foreach ($required in @(
                'Project Truth',
                'Evidence-Grounded Autonomous Debugging',
                'Flexible Spreadsheet Intelligence',
                'Historical PASS ≠ Current Revision PASS'
            )) {
                if (-not $readme.Contains($required)) {
                    throw "README capability contract missing: $required"
                }
            }
        }
    }

    if (-not $SkipDotNet) {
        Invoke-Phase '.NET restore' {
            dotnet restore DevelopmentIntelligencePlatform.sln
        }
        Invoke-Phase '.NET build Release' {
            dotnet build DevelopmentIntelligencePlatform.sln --configuration Release --no-restore
        }
        foreach ($test in @(
            'tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj',
            'tests/Dip.Application.Tests/Dip.Application.Tests.csproj',
            'tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj'
        )) {
            Invoke-Phase ".NET test $(Split-Path $test -LeafBase)" {
                dotnet test $test --configuration Release --no-build --logger "trx" --results-directory artifacts/github-ci-preflight/TestResults
            }
        }
    }

    if (-not $SkipPython) {
        Invoke-Phase 'Python analysis CI' {
            Push-Location 'services/python-analysis'
            try {
                python -m pip install --upgrade pip
                if ($LASTEXITCODE -ne 0) { throw 'pip upgrade failed.' }
                python -m pip install -e ".[dev]"
                if ($LASTEXITCODE -ne 0) { throw 'Python dev install failed.' }
                python -c "import app; import app.main; print(app.__file__)"
                if ($LASTEXITCODE -ne 0) { throw 'Python import smoke failed.' }
                python -m compileall app tests
                if ($LASTEXITCODE -ne 0) { throw 'Python compileall failed.' }
                python -m pytest --junitxml=../../artifacts/github-ci-preflight/python-test-results.xml
                if ($LASTEXITCODE -ne 0) { throw 'pytest failed.' }
            }
            finally { Pop-Location }
        }
    }

    if (-not $SkipNative) {
        Invoke-Phase 'Native collector build/test' {
            Require-Command cmake
            Push-Location 'collectors/native-collector'
            try {
                foreach ($relative in @(
                    '../../Directory.Build.props',
                    '../../Directory.Build.targets',
                    '../../Directory.Packages.props'
                )) {
                    if (-not (Test-Path $relative)) { throw "Missing repository build file: $relative" }
                    [xml](Get-Content $relative -Raw) | Out-Null
                }

                if (Test-Path 'build-preflight') {
                    Remove-Item 'build-preflight' -Recurse -Force
                }
                cmake -S . -B build-preflight -DCMAKE_BUILD_TYPE=Release -DBUILD_TESTING=ON
                if ($LASTEXITCODE -ne 0) { throw 'CMake configure failed.' }
                cmake --build build-preflight --config Release --parallel
                if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
                ctest --test-dir build-preflight -C Release --output-on-failure
                if ($LASTEXITCODE -ne 0) { throw 'Native tests failed.' }
            }
            finally { Pop-Location }
        }
    }

    if (-not $SkipSecurity) {
        Invoke-Phase '.NET dependency vulnerability audit' {
            $output = dotnet list DevelopmentIntelligencePlatform.sln package --vulnerable --include-transitive 2>&1 | Out-String
            $output | Write-Host
            if ($LASTEXITCODE -ne 0) { throw 'NuGet vulnerability audit failed.' }
            if ($output -match 'has the following vulnerable packages') {
                throw 'Vulnerable NuGet package(s) detected.'
            }
        }

        Invoke-Phase 'Python dependency vulnerability audit' {
            python -m pip install --upgrade pip-audit
            if ($LASTEXITCODE -ne 0) { throw 'pip-audit install failed.' }
            python -m pip_audit -r services/python-analysis/requirements.txt
            if ($LASTEXITCODE -ne 0) { throw 'Python dependency vulnerability audit failed.' }
        }

        if (Get-Command gitleaks -ErrorAction SilentlyContinue) {
            Invoke-Phase 'Secret scan' {
                gitleaks git --redact --no-banner .
            }
        }
        else {
            Add-Result -Name 'Secret scan' -Status 'BLOCKED' -Seconds 0 -Log '' -Message 'gitleaks is not installed locally. Install it before publication; GitHub Security Scan runs gitleaks against full history.'
            if (-not $ContinueOnFailure) {
                throw 'gitleaks is required for a complete local preflight.'
            }
        }

        Invoke-Phase 'C# static-analysis fallback' {
            dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
        }
        Invoke-Phase 'Python static-analysis fallback' {
            python -m compileall -q services/python-analysis/app collectors/unreal-collector
        }
    }

    if (-not $SkipIntegration) {
        Invoke-Phase 'Database migrations' {
            $script = Join-Path $PSScriptRoot 'Verify-DatabaseMigrations.ps1'
            if (-not (Test-Path $script)) { throw 'Verify-DatabaseMigrations.ps1 is missing.' }
            & $script
            if ($LASTEXITCODE -ne 0) { throw 'Database migration verification failed.' }
        }

        Invoke-Phase 'Integration and E2E' {
            $verifySolution = Join-Path $PSScriptRoot 'Verify-Solution.ps1'
            if (-not (Test-Path $verifySolution)) { throw 'Verify-Solution.ps1 is missing.' }
            & $verifySolution -Configuration Release
            if ($LASTEXITCODE -ne 0) { throw 'Verify-Solution.ps1 failed.' }
        }
    }

    if (-not $SkipGui) {
        Invoke-Phase 'GUI E2E / Playwright' {
            $script = Join-Path $PSScriptRoot 'Verify-GuiRuntime.ps1'
            if (-not (Test-Path $script)) { throw 'Verify-GuiRuntime.ps1 is missing.' }
            & $script -RunPlaywright -StartupTimeoutSeconds 900
            if ($LASTEXITCODE -ne 0) { throw 'GUI runtime verification failed.' }
        }
    }
}
catch {
    Write-Host "`nPreflight stopped: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Pop-Location

    $finished = [DateTimeOffset]::UtcNow
    $status = if (($results | Where-Object status -ne 'PASS').Count -eq 0) { 'PASS' } else { 'FAIL' }

    $payload = [ordered]@{
        schema = 'dip.github-ci-preflight.v1'
        sourceCommit = $sourceCommit
        startedAtUtc = $started.ToString('o')
        finishedAtUtc = $finished.ToString('o')
        status = $status
        phases = $results
    }

    $payload | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding UTF8

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('# GitHub CI local preflight')
    $lines.Add('')
    $lines.Add("- Source commit: ``$sourceCommit``")
    $lines.Add("- Status: **$status**")
    $lines.Add('')
    $lines.Add('| Phase | Status | Seconds |')
    $lines.Add('|---|---:|---:|')
    foreach ($r in $results) {
        $lines.Add("| $($r.name) | $($r.status) | $($r.seconds) |")
    }
    $lines.Add('')
    $lines.Add('Only a complete PASS should be used as the signal to spend GitHub-hosted runner minutes on the publication SHA.')
    $lines | Set-Content -LiteralPath $SummaryPath -Encoding UTF8

    Write-Host "`nReport: $ReportPath"
    Write-Host "Summary: $SummaryPath"
    if ($status -ne 'PASS') { exit 1 }
}
