# Purpose: Packages exact-revision portfolio evidence into one independently hashable ZIP after all mandatory gates have passed.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = '',
    [string]$ExpectedSha = '',
    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
} else {
    $RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $RepositoryRoot 'artifacts\portfolio-evidence-package'
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'git is required.' }

function Invoke-GitText {
    param([Parameter(Mandatory=$true)][string[]]$Arguments)
    $text = & git -C $RepositoryRoot @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed:`n$($text -join [Environment]::NewLine)"
    }
    return (($text | Out-String).Trim())
}

function Read-JsonObject {
    param([Parameter(Mandatory=$true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required evidence is missing: $Path" }
    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
}

function Assert-SourceCommit {
    param(
        [Parameter(Mandatory=$true)][string]$Label,
        [Parameter(Mandatory=$true)]$Value,
        [Parameter(Mandatory=$true)][string]$Property
    )
    $actual = [string]$Value.$Property
    if ($actual -ne $ExpectedSha) { throw "$Label SHA mismatch. expected=$ExpectedSha actual=$actual" }
}

function Get-RelativePathSafe {
    param(
        [Parameter(Mandatory=$true)][string]$Root,
        [Parameter(Mandatory=$true)][string]$Path
    )
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd([char[]]@(92,47))
    $pathFull = [IO.Path]::GetFullPath($Path)
    $prefix = $rootFull + [IO.Path]::DirectorySeparatorChar
    if (-not $pathFull.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the staging root. root=$rootFull path=$pathFull"
    }
    return $pathFull.Substring($prefix.Length).Replace('\','/')
}

$head = Invoke-GitText @('rev-parse','HEAD')
if ([string]::IsNullOrWhiteSpace($ExpectedSha)) { $ExpectedSha = $head }
if ($ExpectedSha -notmatch '^[0-9a-fA-F]{40}$') { throw "ExpectedSha must be a full 40-character SHA: $ExpectedSha" }
if ($head -ne $ExpectedSha) { throw "HEAD mismatch. expected=$ExpectedSha actual=$head" }

& git -C $RepositoryRoot fetch origin main --prune
if ($LASTEXITCODE -ne 0) { throw 'Could not fetch origin/main.' }
$originMain = Invoke-GitText @('rev-parse','origin/main')
if ($originMain -ne $ExpectedSha) {
    throw "Evidence packaging requires HEAD == origin/main. HEAD=$ExpectedSha origin/main=$originMain"
}
$dirty = Invoke-GitText @('status','--porcelain','--untracked-files=no')
if (-not [string]::IsNullOrWhiteSpace($dirty)) { throw "Evidence packaging requires a clean tracked tree:`n$dirty" }

$finalRoot = Join-Path $RepositoryRoot ("artifacts\final-evidence\" + $ExpectedSha)
$finalSummaryPath = Join-Path $finalRoot 'FINAL_EVIDENCE_SUMMARY.json'
$final = Read-JsonObject $finalSummaryPath
Assert-SourceCommit 'Final evidence' $final 'sourceCommit'
if ([string]$final.status -ne 'PASS') { throw "Final evidence is not PASS: $($final.status)" }
if ([string]$final.requiredCi -ne '11/11 Green') { throw "Final evidence does not bind 11/11 Green CI: $($final.requiredCi)" }
if ($final.benchmarkPassed -ne $true -or $final.qaBugZooPassed -ne $true -or $final.projectTruthScenarioPassed -ne $true) {
    throw 'Final evidence is missing a mandatory benchmark, QA Bug Zoo, or Project Truth PASS.'
}

$determinismPath = Join-Path $RepositoryRoot 'artifacts\qa-bug-zoo-determinism\qa-bug-zoo-determinism-summary.json'
$determinism = Read-JsonObject $determinismPath
Assert-SourceCommit 'Bug Zoo determinism' $determinism 'sourceCommit'
if ([string]$determinism.status -ne 'PASS'
    -or [int]$determinism.runCount -ne 10
    -or [int]$determinism.passCount -ne 10
    -or [decimal]$determinism.detectionRecallPercent -ne 100
    -or [decimal]$determinism.falsePositiveRatePercent -ne 0
    -or [decimal]$determinism.reproductionRatePercent -ne 100
    -or [int]$determinism.unstableCaseCount -ne 0) {
    throw 'Bug Zoo determinism evidence does not meet the 10/10 exact-revision acceptance criteria.'
}

$dockerRoot = Join-Path $RepositoryRoot 'artifacts\docker-primary'
$dockerSummaryPath = Join-Path $dockerRoot 'docker-primary-verification.json'
$dockerSummary = Read-JsonObject $dockerSummaryPath
Assert-SourceCommit 'Docker Primary' $dockerSummary 'sourceCommit'
if ([string]$dockerSummary.status -ne 'PASS') { throw "Docker Primary verification is not PASS: $($dockerSummary.status)" }

$dockerPassPath = Join-Path $dockerRoot 'DOCKER_PRIMARY_VERIFICATION_PASS.txt'
if (-not (Test-Path -LiteralPath $dockerPassPath -PathType Leaf)) { throw "Docker Primary PASS sentinel is missing: $dockerPassPath" }
$dockerPass = Get-Content -LiteralPath $dockerPassPath -Raw
if ($dockerPass -notmatch [regex]::Escape("Commit=$ExpectedSha")) { throw 'Docker Primary PASS sentinel is not bound to the expected SHA.' }

$dockerBenchmarkPath = Join-Path $dockerRoot 'portfolio-benchmark-results.json'
$dockerBenchmark = Read-JsonObject $dockerBenchmarkPath
Assert-SourceCommit 'Docker Primary benchmark' $dockerBenchmark 'sourceCommit'
if ($dockerBenchmark.passed -ne $true) { throw 'Docker Primary benchmark/soak did not pass.' }

$governancePath = Join-Path $dockerRoot 'governance-load-results.json'
$governance = Read-JsonObject $governancePath
Assert-SourceCommit 'Governance load' $governance 'sourceCommit'
if ($governance.passed -ne $true) { throw 'Governance load did not pass.' }

foreach ($name in @('publication-safety.json','portfolio-readiness.json')) {
    $path = Join-Path $dockerRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required Docker Primary evidence is missing: $path" }
}

foreach ($name in @('ci-matrix.json','portfolio-benchmark-results.json','qa-bug-zoo-results.json','project-truth-evidence.json','SHA256SUMS.txt')) {
    $path = Join-Path $finalRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Final evidence bundle is incomplete: $path" }
}

$currentReadinessPath = Join-Path $RepositoryRoot 'artifacts\current-revision-readiness.json'
$currentReadiness = Read-JsonObject $currentReadinessPath
if ([string]$currentReadiness.status -ne 'PASS') { throw 'Current revision portfolio readiness is not PASS.' }

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$staging = Join-Path $OutputDirectory ('.staging-' + $ExpectedSha)
if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path $staging | Out-Null

try {
    $finalDest = Join-Path $staging 'final-evidence'
    New-Item -ItemType Directory -Force -Path $finalDest | Out-Null
    Copy-Item -Path (Join-Path $finalRoot '*') -Destination $finalDest -Recurse -Force

    $detDest = Join-Path $staging 'qa-bug-zoo-determinism'
    New-Item -ItemType Directory -Force -Path $detDest | Out-Null
    Copy-Item -LiteralPath $determinismPath -Destination (Join-Path $detDest 'qa-bug-zoo-determinism-summary.json') -Force
    $detPass = Join-Path (Split-Path $determinismPath -Parent) 'DIP_QA_BUG_ZOO_DETERMINISM_PASS.txt'
    if (-not (Test-Path -LiteralPath $detPass -PathType Leaf)) { throw "Bug Zoo determinism PASS sentinel is missing: $detPass" }
    Copy-Item -LiteralPath $detPass -Destination (Join-Path $detDest 'DIP_QA_BUG_ZOO_DETERMINISM_PASS.txt') -Force

    $dockerDest = Join-Path $staging 'docker-primary'
    New-Item -ItemType Directory -Force -Path $dockerDest | Out-Null
    foreach ($name in @(
        'docker-primary-verification.json',
        'DOCKER_PRIMARY_VERIFICATION_PASS.txt',
        'portfolio-benchmark-results.json',
        'governance-load-results.json',
        'publication-safety.json',
        'portfolio-readiness.json'
    )) {
        Copy-Item -LiteralPath (Join-Path $dockerRoot $name) -Destination (Join-Path $dockerDest $name) -Force
    }

    Copy-Item -LiteralPath $currentReadinessPath -Destination (Join-Path $staging 'CURRENT_REVISION_READINESS.json') -Force

    $manifestPath = Join-Path $staging 'EVIDENCE_PACKAGE_MANIFEST.json'
    $manifest = [ordered]@{
        schema = 'dip.portfolio-evidence-package.v2'
        status = 'PASS'
        sourceCommit = $ExpectedSha
        originMain = $originMain
        trackedTreeClean = $true
        requiredCi = '11/11 Green'
        finalEvidenceStatus = [string]$final.status
        dockerPrimaryStatus = [string]$dockerSummary.status
        currentRevisionReadiness = [string]$currentReadiness.status
        bugZooDeterminism = [ordered]@{
            runtime = [string]$determinism.runtime
            runs = [int]$determinism.runCount
            passCount = [int]$determinism.passCount
            recallPercent = [decimal]$determinism.detectionRecallPercent
            falsePositiveRatePercent = [decimal]$determinism.falsePositiveRatePercent
            reproductionRatePercent = [decimal]$determinism.reproductionRatePercent
            unstableCaseCount = [int]$determinism.unstableCaseCount
        }
        engineRuntime = [ordered]@{ unity = 'NOT_EVALUATED'; unreal = 'NOT_EVALUATED' }
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    }
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    $sumPath = Join-Path $staging 'SHA256SUMS.txt'
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($file in @(Get-ChildItem -LiteralPath $staging -File -Recurse | Where-Object { $_.FullName -ne $sumPath } | Sort-Object FullName)) {
        $relative = Get-RelativePathSafe -Root $staging -Path $file.FullName
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $lines.Add("$hash  $relative")
    }
    $lines | Set-Content -LiteralPath $sumPath -Encoding ASCII

    $zipPath = Join-Path $OutputDirectory ("DIP_PORTFOLIO_EVIDENCE_" + $ExpectedSha + '.zip')
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force
    if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) { throw "Evidence ZIP was not created: $zipPath" }

    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $zipHashPath = $zipPath + '.sha256'
    "$zipHash  $([IO.Path]::GetFileName($zipPath))" | Set-Content -LiteralPath $zipHashPath -Encoding ASCII

    Write-Host 'PORTFOLIO EVIDENCE PACKAGE: PASS' -ForegroundColor Green
    Write-Host "SourceCommit=$ExpectedSha"
    Write-Host "ZIP=$zipPath"
    Write-Host "SHA256=$zipHashPath"
}
finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
