# Purpose: Serves as the single fail-closed completion gate for the exact main revision and produces final portfolio evidence only after every mandatory gate passes.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Version,
    [int]$WaitMinutes = 120,
    [int]$SoakSeconds = 900,
    [int]$SoakRequestsPerSecond = 25,
    [switch]$PublishRelease
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root
$script:IsWindowsHost = $env:OS -eq 'Windows_NT'

# Windows PowerShell 5.1 and Python subprocesses otherwise inherit the active ANSI code page.
$env:PYTHONUTF8 = '1'
$env:PYTHONIOENCODING = 'utf-8'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'

try {
    Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force -ErrorAction SilentlyContinue
}
catch {
    Write-Verbose "Process execution policy could not be changed: $($_.Exception.Message)"
}

function Write-Section([string]$Text) {
    Write-Host ''
    Write-Host ('=' * 78) -ForegroundColor DarkCyan
    Write-Host $Text -ForegroundColor Cyan
    Write-Host ('=' * 78) -ForegroundColor DarkCyan
}

function Assert-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Invoke-GitText([string[]]$Arguments) {
    $output = & git -C $root @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed:`n$($output -join [Environment]::NewLine)"
    }
    return (($output | Out-String).Trim())
}

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory=$true)][string]$File,
        [Parameter(Mandatory=$true)][string[]]$Arguments
    )
    $old = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $File @Arguments
        $exit = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $old
    }
    if ($exit -ne 0) {
        throw "$File $($Arguments -join ' ') failed with exit code $exit."
    }
}

function Test-DockerLinuxEngine {
    $old = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $value = (& docker info --format '{{.OSType}}' 2>$null | Out-String).Trim()
        return $LASTEXITCODE -eq 0 -and $value -eq 'linux'
    }
    catch { return $false }
    finally { $ErrorActionPreference = $old }
}

function Ensure-DockerLinuxEngine {
    Assert-Command 'docker'
    if (Test-DockerLinuxEngine) { return }

    if (-not $script:IsWindowsHost) {
        throw 'Docker is installed but a Linux engine is not available.'
    }

    $desktop = 'C:\Program Files\Docker\Docker\Docker Desktop.exe'
    if (-not (Test-Path -LiteralPath $desktop -PathType Leaf)) {
        throw "Docker Desktop is required but was not found: $desktop"
    }
    if (-not (Get-Process -Name 'Docker Desktop' -ErrorAction SilentlyContinue)) {
        Start-Process -FilePath $desktop | Out-Null
    }

    for ($attempt = 1; $attempt -le 180; $attempt++) {
        if (Test-DockerLinuxEngine) { return }
        Start-Sleep -Seconds 2
    }
    throw 'Docker Desktop Linux engine did not become ready.'
}

function Get-FreeTcpPort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Wait-PostgresContainer([string]$Container) {
    for ($attempt = 1; $attempt -le 120; $attempt++) {
        $old = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            & docker exec $Container pg_isready -U dip -d dip *> $null
            $exit = $LASTEXITCODE
        }
        finally { $ErrorActionPreference = $old }
        if ($exit -eq 0) { return }
        Start-Sleep -Seconds 1
    }
    throw "PostgreSQL container did not become ready: $Container"
}

function Assert-ExactSource([string]$ExpectedSha) {
    & git -C $root fetch origin main --prune
    if ($LASTEXITCODE -ne 0) { throw 'Could not fetch origin/main.' }
    $headNow = Invoke-GitText @('rev-parse','HEAD')
    $remoteNow = Invoke-GitText @('rev-parse','origin/main')
    if ($headNow -ne $ExpectedSha -or $remoteNow -ne $ExpectedSha) {
        throw "Exact source moved during verification. expected=$ExpectedSha HEAD=$headNow origin/main=$remoteNow"
    }
    $dirty = Invoke-GitText @('status','--porcelain','--untracked-files=no')
    if (-not [string]::IsNullOrWhiteSpace($dirty)) {
        throw "Tracked source tree became dirty:`n$dirty"
    }
}

function Read-JsonObject([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required JSON evidence is missing: $Path" }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

foreach ($command in @('git','python','gh','dotnet','docker')) { Assert-Command $command }
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') { throw "Invalid semantic version: $Version" }

Write-Section 'Precondition: one exact main revision'
$branch = Invoke-GitText @('branch','--show-current')
if ($branch -ne 'main') { throw "Final completion gate must run from main, not '$branch'." }
$dirty = Invoke-GitText @('status','--porcelain','--untracked-files=no')
if ($dirty) { throw "Final completion gate requires a clean tracked working tree.`n$dirty" }

& git -C $root fetch origin main --prune
if ($LASTEXITCODE -ne 0) { throw 'Could not fetch origin/main.' }
$sha = Invoke-GitText @('rev-parse','HEAD')
$remoteSha = Invoke-GitText @('rev-parse','origin/main')
if ($sha -ne $remoteSha) { throw "Local main must already be pushed. local=$sha origin/main=$remoteSha" }
if ($sha -notmatch '^[0-9a-fA-F]{40}$') { throw "Invalid final SHA: $sha" }

Ensure-DockerLinuxEngine
Invoke-NativeChecked 'gh' @('auth','status')
Write-Host "FINAL SOURCE SHA: $sha" -ForegroundColor Green

Write-Section 'Dispatch the 11 required exact-SHA workflows'
$workflows = @(
    'commit-message.yml',
    'dotnet-ci.yml',
    'python-ci.yml',
    'native-collector-ci.yml',
    'integration-test.yml',
    'gui-e2e.yml',
    'docker-build.yml',
    'security-scan.yml',
    'universal-qa-benchmark.yml',
    'unreal-collector-ci.yml',
    'publication-readiness.yml'
)
foreach ($workflow in $workflows) {
    Write-Host "Dispatching $workflow"
    Invoke-NativeChecked 'gh' @('workflow','run',$workflow,'--ref','main')
}

Write-Section 'Docker Primary: migration / E2E / GUI / DR / benchmark+soak / governance / dependency audit'
& (Join-Path $PSScriptRoot 'Verify-DockerPrimary.ps1') `
    -RepositoryRoot $root `
    -ExpectedSha $sha `
    -SoakSeconds $SoakSeconds `
    -SoakRequestsPerSecond $SoakRequestsPerSecond
if ($LASTEXITCODE -ne 0) { throw 'Docker Primary verification failed.' }

$dockerSummary = Read-JsonObject (Join-Path $root 'artifacts\docker-primary\docker-primary-verification.json')
if ([string]$dockerSummary.sourceCommit -ne $sha -or [string]$dockerSummary.status -ne 'PASS') {
    throw 'Docker Primary evidence is not exact-SHA PASS.'
}
Assert-ExactSource $sha

Write-Section 'Linux/LSan QA Bug Zoo determinism: 10/10'
& (Join-Path $PSScriptRoot 'DIP_QA_BUG_ZOO_DETERMINISM.ps1') `
    -RepositoryRoot $root `
    -ExpectedSha $sha `
    -Runs 10
if ($LASTEXITCODE -ne 0) { throw '10-run QA Bug Zoo determinism failed.' }

$detPath = Join-Path $root 'artifacts\qa-bug-zoo-determinism\qa-bug-zoo-determinism-summary.json'
$det = Read-JsonObject $detPath
if ([string]$det.sourceCommit -ne $sha) {
    throw 'Bug Zoo determinism sourceCommit does not match the exact SHA.'
}
if ([string]$det.status -ne 'PASS') {
    throw 'Bug Zoo determinism status is not PASS.'
}
if ([int]$det.runCount -ne 10) {
    throw 'Bug Zoo determinism runCount is not 10.'
}
if ([int]$det.passCount -ne 10) {
    throw 'Bug Zoo determinism passCount is not 10.'
}
if ([decimal]$det.detectionRecallPercent -ne 100) {
    throw 'Bug Zoo determinism detection recall is not 100%.'
}
if ([decimal]$det.falsePositiveRatePercent -ne 0) {
    throw 'Bug Zoo determinism false-positive rate is not 0%.'
}
if ([decimal]$det.reproductionRatePercent -ne 100) {
    throw 'Bug Zoo determinism reproduction rate is not 100%.'
}
if ([int]$det.unstableCaseCount -ne 0) {
    throw 'Bug Zoo determinism unstableCaseCount is not 0.'
}

# Reuse the measured Linux/LSan run as canonical QA Bug Zoo evidence for the finalizer.
$canonicalQa = Join-Path $root 'artifacts\qa-bug-zoo'
if (Test-Path -LiteralPath $canonicalQa) { Remove-Item -LiteralPath $canonicalQa -Recurse -Force }
$lastRun = Join-Path $root 'artifacts\qa-bug-zoo-determinism\run-10'
if (-not (Test-Path -LiteralPath $lastRun -PathType Container)) { throw "Determinism run-10 evidence is missing: $lastRun" }
Copy-Item -LiteralPath $lastRun -Destination $canonicalQa -Recurse -Force
Assert-ExactSource $sha

Write-Section 'Wait for exact-SHA required CI: 11/11 Green'
$ciPath = Join-Path $root 'artifacts\final-evidence\ci-matrix.json'
& python tools/verify_final_ci_matrix.py `
    --sha $sha `
    --wait-minutes $WaitMinutes `
    --poll-seconds 20 `
    --output $ciPath
if ($LASTEXITCODE -ne 0) { throw 'Required exact-SHA CI matrix is not fully Green.' }
$ci = Read-JsonObject $ciPath
if ([int]$ci.requiredCount -ne 11 -or [int]$ci.greenCount -ne 11 -or $ci.allGreen -ne $true) {
    throw "Required CI is not 11/11 Green. required=$($ci.requiredCount) green=$($ci.greenCount)"
}
Assert-ExactSource $sha

Write-Section 'Canonical benchmark and Project Truth evidence'
$postgresPort = Get-FreeTcpPort
$postgresContainer = 'dip-final-postgres-' + $sha.Substring(0,12)
$postgresPassword = 'dip-final-verification-password'
$oldPostgresHost = $env:POSTGRES_HOST
$oldPostgresPort = $env:POSTGRES_PORT
$oldPostgresDb = $env:POSTGRES_DB
$oldPostgresUser = $env:POSTGRES_USER
$oldPostgresPassword = $env:POSTGRES_PASSWORD
$oldPostgresContainer = $env:DIP_POSTGRES_CONTAINER
$startedDipHere = $false

try {
    $old = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & docker rm -f $postgresContainer *> $null
    }
    finally { $ErrorActionPreference = $old }

    Invoke-NativeChecked 'docker' @(
        'run','--name',$postgresContainer,
        '-e','POSTGRES_DB=dip',
        '-e','POSTGRES_USER=dip',
        '-e',"POSTGRES_PASSWORD=$postgresPassword",
        '-p',("{0}:5432" -f $postgresPort),
        '-d','postgres:16-alpine')
    Wait-PostgresContainer $postgresContainer

    $env:POSTGRES_HOST = '127.0.0.1'
    $env:POSTGRES_PORT = [string]$postgresPort
    $env:POSTGRES_DB = 'dip'
    $env:POSTGRES_USER = 'dip'
    $env:POSTGRES_PASSWORD = $postgresPassword
    $env:DIP_POSTGRES_CONTAINER = $postgresContainer

    & (Join-Path $PSScriptRoot 'Run-PortfolioBenchmarks.ps1') `
        -ExpectedSha $sha `
        -SoakSeconds $SoakSeconds `
        -SoakTargetRequestsPerSecond $SoakRequestsPerSecond
    if ($LASTEXITCODE -ne 0) { throw 'Exact-SHA canonical benchmark failed.' }

    $benchmark = Read-JsonObject (Join-Path $root 'artifacts\portfolio-benchmark\portfolio-benchmark-results.json')
    if ([string]$benchmark.sourceCommit -ne $sha -or $benchmark.sourceWorkingTreeDirty -ne $false -or $benchmark.passed -ne $true) {
        throw 'Canonical benchmark is not a clean exact-SHA PASS.'
    }

    $healthy = $false
    try {
        $health = Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:5080/health' -TimeoutSec 2
        $healthy = $health.StatusCode -eq 200
    }
    catch { $healthy = $false }

    if (-not $healthy) {
        & (Join-Path $PSScriptRoot 'Start-DIP.ps1') -NoBrowser
        if ($LASTEXITCODE -ne 0) { throw 'Could not start DIP for Project Truth verification.' }
        $startedDipHere = $true
    }

    & python tools/publish_qa_scenario_evidence.py `
        --api-base 'http://127.0.0.1:5080' `
        --expected-dip-sha $sha `
        --output 'artifacts/qa-project-truth'
    if ($LASTEXITCODE -ne 0) { throw 'QA Scenario -> DIP -> Project Truth verification failed.' }
}
finally {
    if ($startedDipHere) {
        try { & (Join-Path $PSScriptRoot 'Stop-DIP.ps1') } catch { Write-Warning $_.Exception.Message }
    }
    $old = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & docker rm -f $postgresContainer *> $null
    }
    finally { $ErrorActionPreference = $old }

    $env:POSTGRES_HOST = $oldPostgresHost
    $env:POSTGRES_PORT = $oldPostgresPort
    $env:POSTGRES_DB = $oldPostgresDb
    $env:POSTGRES_USER = $oldPostgresUser
    $env:POSTGRES_PASSWORD = $oldPostgresPassword
    $env:DIP_POSTGRES_CONTAINER = $oldPostgresContainer
}
Assert-ExactSource $sha

Write-Section 'Final exact-revision evidence binding'
$projectTruthPath = Join-Path $root 'artifacts\qa-project-truth\project-truth-evidence.json'
& python tools/finalize_portfolio_evidence.py `
    --sha $sha `
    --ci 'artifacts/final-evidence/ci-matrix.json' `
    --benchmark 'artifacts/portfolio-benchmark/portfolio-benchmark-results.json' `
    --qa-bug-zoo 'artifacts/qa-bug-zoo/qa-bug-zoo-results.json' `
    --project-truth 'artifacts/qa-project-truth/project-truth-evidence.json' `
    --output-root 'artifacts/final-evidence'
if ($LASTEXITCODE -ne 0) { throw 'Final evidence binding failed.' }

$finalRoot = Join-Path $root ("artifacts\final-evidence\" + $sha)
$finalSummaryPath = Join-Path $finalRoot 'FINAL_EVIDENCE_SUMMARY.json'
$final = Read-JsonObject $finalSummaryPath
if ([string]$final.status -ne 'PASS' -or [string]$final.sourceCommit -ne $sha -or [string]$final.requiredCi -ne '11/11 Green') {
    throw 'Final evidence summary is not exact-SHA PASS with 11/11 Green.'
}
if ($final.benchmarkPassed -ne $true -or $final.qaBugZooPassed -ne $true -or $final.projectTruthScenarioPassed -ne $true) {
    throw 'Final evidence summary is missing a mandatory PASS.'
}
if ([string]$final.projectTruthExpectedOutcome.qualityGateStatus -ne 'Failed') {
    throw 'Project Truth expected quality gate status is not Failed.'
}
if ([string]$final.projectTruthExpectedOutcome.claimVerificationStatus -ne 'Contradicted') {
    throw 'Project Truth expected claim verification status is not Contradicted.'
}

Write-Section 'Current revision readiness'
$currentReadinessPath = Join-Path $root 'artifacts\current-revision-readiness.json'
& python tools/validate_portfolio_readiness.py `
    --root $root `
    --require-current-final-evidence `
    --output $currentReadinessPath
if ($LASTEXITCODE -ne 0) { throw 'Current revision portfolio readiness failed.' }
$currentReadiness = Read-JsonObject $currentReadinessPath
if ([string]$currentReadiness.status -ne 'PASS') { throw 'Current revision readiness is not PASS.' }
Assert-ExactSource $sha

Write-Section 'Package exact-revision evidence and verify ZIP SHA-256'
& (Join-Path $PSScriptRoot 'Package-PortfolioEvidence.ps1') -RepositoryRoot $root -ExpectedSha $sha
if ($LASTEXITCODE -ne 0) { throw 'Portfolio evidence packaging failed.' }

$zipPath = Join-Path $root ("artifacts\portfolio-evidence-package\DIP_PORTFOLIO_EVIDENCE_" + $sha + '.zip')
$zipHashPath = $zipPath + '.sha256'
if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf) -or -not (Test-Path -LiteralPath $zipHashPath -PathType Leaf)) {
    throw 'Evidence ZIP or its SHA256 sidecar is missing.'
}
$actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$declaredHash = ((Get-Content -LiteralPath $zipHashPath -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
if ($actualHash -ne $declaredHash) { throw "Evidence ZIP SHA256 mismatch. actual=$actualHash declared=$declaredHash" }
Assert-ExactSource $sha

if ($PublishRelease) {
    Write-Section 'Publish optional GitHub Release'
    $dispatchTime = [DateTimeOffset]::UtcNow
    Invoke-NativeChecked 'gh' @('workflow','run','release.yml','--ref','main','-f',"version=$Version",'-f',"expected_sha=$sha")

    $releaseRunId = $null
    $deadline = (Get-Date).AddMinutes(5)
    while (-not $releaseRunId -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        $json = & gh run list --workflow release.yml --branch main --limit 20 --json databaseId,headSha,status,conclusion,createdAt
        if ($LASTEXITCODE -ne 0) { throw 'Could not query Release workflow runs.' }
        $candidate = @($json | ConvertFrom-Json) | Where-Object {
            $_.headSha -eq $sha -and [DateTimeOffset]$_.createdAt -ge $dispatchTime.AddMinutes(-1)
        } | Sort-Object createdAt -Descending | Select-Object -First 1
        if ($candidate) { $releaseRunId = [string]$candidate.databaseId }
    }
    if (-not $releaseRunId) { throw 'Could not identify the dispatched Release run.' }
    Invoke-NativeChecked 'gh' @('run','watch',$releaseRunId,'--exit-status')

    $tag = "v$Version"
    $uploadFiles = @(
        $finalSummaryPath,
        (Join-Path $finalRoot 'SHA256SUMS.txt'),
        (Join-Path $root 'artifacts\portfolio-benchmark\portfolio-benchmark-results.json'),
        (Join-Path $root 'artifacts\qa-bug-zoo\qa-bug-zoo-results.json'),
        $projectTruthPath,
        $zipPath,
        $zipHashPath
    )
    foreach ($file in $uploadFiles) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Release evidence file missing: $file" }
    }
    & gh release upload $tag @uploadFiles --clobber
    if ($LASTEXITCODE -ne 0) { throw "Could not upload exact-revision evidence to $tag." }
}

# No completion declaration is written before every assertion above has succeeded.
$completionPath = Join-Path $root 'artifacts\portfolio-evidence-package\FINAL_COMPLETION_DECLARATION.txt'
@(
    'DIP PORTFOLIO FINAL COMPLETION GATE: PASS'
    "SourceCommit=$sha"
    "Version=$Version"
    'TrackedTreeClean=true'
    'GitHubActions=11/11 Green'
    'DockerPrimary=PASS'
    'MigrationApplyRollbackReapply=PASS'
    'IntegrationE2E=PASS'
    'GuiPlaywright=PASS'
    'DisasterRecovery=PASS'
    'BenchmarkAndSoak=PASS'
    'Governance=PASS'
    'DependencyAudit=PASS'
    'BugZooDeterminism=10/10 PASS'
    'DetectionRecall=100%'
    'FalsePositiveRate=0%'
    'ReproductionRate=100%'
    'UnstableCaseCount=0'
    'ProjectTruthExpectedFailureScenario=PASS'
    'FinalEvidenceBinding=PASS'
    'CurrentRevisionReadiness=PASS'
    "EvidenceZip=$zipPath"
    "EvidenceZipSha256=$actualHash"
    'UnityRealEditorRuntime=NOT_EVALUATED'
    'UnrealRealEditorRuntime=NOT_EVALUATED'
    "VerifiedAtUtc=$([DateTimeOffset]::UtcNow.ToString('o'))"
) | Set-Content -LiteralPath $completionPath -Encoding UTF8

Write-Host ''
Write-Host '######################################################################' -ForegroundColor Green
Write-Host '#        DIP PORTFOLIO FINAL COMPLETION GATE: PASS                   #' -ForegroundColor Green
Write-Host '######################################################################' -ForegroundColor Green
Write-Host "Final SHA: $sha" -ForegroundColor Cyan
Write-Host 'GitHub Actions: 11/11 Green' -ForegroundColor Green
Write-Host 'Docker Primary: PASS' -ForegroundColor Green
Write-Host 'Bug Zoo determinism: 10/10 PASS' -ForegroundColor Green
Write-Host 'Benchmark / Soak: PASS' -ForegroundColor Green
Write-Host 'Project Truth: PASS' -ForegroundColor Green
Write-Host 'Current Revision Readiness: PASS' -ForegroundColor Green
Write-Host "Evidence ZIP: $zipPath" -ForegroundColor Cyan
Write-Host "ZIP SHA256: $actualHash" -ForegroundColor Cyan
Write-Host 'Unity real Editor runtime: NOT_EVALUATED' -ForegroundColor Yellow
Write-Host 'Unreal real Editor runtime: NOT_EVALUATED' -ForegroundColor Yellow
