# Purpose: Repeats the exact-revision QA Bug Zoo and proves stable findings/tool paths across runs.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = '',
    [string]$ExpectedSha = '',
    [ValidateRange(2,100)][int]$Runs = 10,
    [string]$OutputDirectory = '',
    [string[]]$RequireLanguages = @('CSharp','Cpp','Python')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
} else {
    $RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $RepositoryRoot 'artifacts\qa-bug-zoo-determinism'
}

foreach ($command in @('git','dotnet')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command is required."
    }
}

function Invoke-GitText {
    param([Parameter(Mandatory=$true)][string[]]$Arguments)
    $text = & git -C $RepositoryRoot @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed:`n$($text -join [Environment]::NewLine)"
    }
    return (($text | Out-String).Trim())
}

function Get-DiagnosticToolSignature {
    param(
        [Parameter(Mandatory=$true)][string]$RunRoot,
        [Parameter(Mandatory=$true)]$Case
    )

    $caseRoot = Join-Path $RunRoot ("cases\{0}\{1}\first-native" -f $Case.language,$Case.name)
    if (-not (Test-Path -LiteralPath $caseRoot -PathType Container)) {
        return @()
    }

    $signatures = [Collections.Generic.List[string]]::new()
    foreach ($file in @(Get-ChildItem -LiteralPath $caseRoot -Filter 'execution-diagnostics.json' -Recurse -File | Sort-Object FullName)) {
        $diag = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        $exe = [string]$diag.executable
        $exit = if ($null -eq $diag.exitCode) { '<null>' } else { [string]$diag.exitCode }
        $signatures.Add(("{0}|{1}|exit={2}|timedOut={3}|infraTimeout={4}" -f
            [string]$diag.tool,
            $exe,
            $exit,
            [bool]$diag.timedOut,
            [bool]$diag.infrastructureTimeout))
    }
    return @($signatures | Sort-Object)
}

function Get-CaseSignature {
    param(
        [Parameter(Mandatory=$true)][string]$RunRoot,
        [Parameter(Mandatory=$true)]$Case
    )

    $tools = Get-DiagnosticToolSignature -RunRoot $RunRoot -Case $Case
    $toolText = $tools -join ';'
    return ("category={0}|severity={1}|exception={2}|rule={3}|findings={4}|detected={5}|reproduced={6}|tools={7}" -f
        [string]$Case.matchedCategory,
        [string]$Case.matchedSeverity,
        [string]$Case.matchedExceptionType,
        [string]$Case.matchedRuleId,
        [int]$Case.findingCount,
        [bool]$Case.detected,
        [bool]$Case.reproduced,
        $toolText)
}

$head = Invoke-GitText @('rev-parse','HEAD')
if ([string]::IsNullOrWhiteSpace($ExpectedSha)) { $ExpectedSha = $head }
if ($ExpectedSha -notmatch '^[0-9a-fA-F]{40}$') { throw "ExpectedSha must be a full 40-character SHA: $ExpectedSha" }
if ($head -ne $ExpectedSha) { throw "HEAD mismatch. expected=$ExpectedSha actual=$head" }

$originMain = Invoke-GitText @('rev-parse','origin/main')
if ($originMain -ne $ExpectedSha) {
    throw "Exact-revision determinism requires HEAD == origin/main. HEAD=$ExpectedSha origin/main=$originMain"
}

$trackedStatus = Invoke-GitText @('status','--porcelain','--untracked-files=no')
if (-not [string]::IsNullOrWhiteSpace($trackedStatus)) {
    throw "Tracked working tree must be clean before determinism verification:`n$trackedStatus"
}

$project = Join-Path $RepositoryRoot 'tools\Dip.QaBugZooVerifier\Dip.QaBugZooVerifier.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "Bug Zoo verifier project is missing: $project"
}

if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$reference = @{}
$unstable = [Collections.Generic.List[object]]::new()
$runSummaries = [Collections.Generic.List[object]]::new()

for ($run = 1; $run -le $Runs; $run++) {
    $runName = 'run-{0:D2}' -f $run
    $runRoot = Join-Path $OutputDirectory $runName
    New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

    Write-Host "[$run/$Runs] QA Bug Zoo exact-revision run: $ExpectedSha" -ForegroundColor Cyan
    $arguments = @(
        'run',
        '--project', $project,
        '--configuration', 'Release',
        '--',
        '--root', $RepositoryRoot,
        '--output', $runRoot,
        '--expected-sha', $ExpectedSha,
        '--require-languages', ($RequireLanguages -join ',')
    )

    & dotnet @arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "Bug Zoo run $run failed with exit code $exitCode. Evidence: $runRoot"
    }

    $jsonPath = Join-Path $runRoot 'qa-bug-zoo-results.json'
    if (-not (Test-Path -LiteralPath $jsonPath -PathType Leaf)) {
        throw "Run $run did not produce $jsonPath"
    }

    $result = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
    if ([string]$result.sourceCommit -ne $ExpectedSha) { throw "Run $run sourceCommit mismatch." }
    if (-not [bool]$result.passed) { throw "Run $run did not report passed=true." }
    if ([decimal]$result.detectionRecallPercent -ne 100) { throw "Run $run recall is not 100%." }
    if ([decimal]$result.falsePositiveRatePercent -ne 0) { throw "Run $run false-positive rate is not 0%." }
    if ([decimal]$result.reproductionRatePercent -ne 100) { throw "Run $run reproduction rate is not 100%." }
    if ([int]$result.blockingInfrastructureFailures -ne 0) { throw "Run $run has blocking infrastructure failures." }

    # Fail closed when CTest orchestration timed out. QaParsing intentionally keeps this
    # out of product findings, so the determinism verifier must independently reject it
    # as infrastructure evidence rather than allowing a clean control to appear PASS.
    $infraTimeoutDiagnostics = @()
    foreach ($diagFile in @(Get-ChildItem -LiteralPath $runRoot -Filter 'execution-diagnostics.json' -Recurse -File)) {
        $diag = Get-Content -LiteralPath $diagFile.FullName -Raw | ConvertFrom-Json
        if ([bool]$diag.infrastructureTimeout) {
            $infraTimeoutDiagnostics += $diagFile.FullName
        }
    }
    if ($infraTimeoutDiagnostics.Count -gt 0) {
        throw ("Run $run contains CTest infrastructure timeout evidence; it is neither a product finding nor a PASS:`n" + ($infraTimeoutDiagnostics -join "`n"))
    }

    # The P0 acceptance target explicitly includes memory_leak. On Windows the historical
    # fixture is optional because MSVC ASan does not provide the Linux LeakSanitizer path.
    # Full determinism evidence therefore must run where every required C++ fixture executes.
    $requiredCppCases = @('clean','memory_leak','buffer_overflow','double_free','use_after_free')
    $executedCppCaseNames = @($result.results | Where-Object { [string]$_.language -ieq 'Cpp' } | ForEach-Object { [string]$_.name })
    $missingCppCases = @($requiredCppCases | Where-Object { $_ -notin $executedCppCaseNames })
    if ($missingCppCases.Count -gt 0) {
        throw ("Run $run skipped required C++ determinism fixture(s): " + ($missingCppCases -join ', ') + ". Run the full P0 determinism gate on a Linux/LSan-capable environment.")
    }

    $cppClean = @($result.results | Where-Object {
        [string]$_.language -ieq 'Cpp' -and [string]$_.name -eq 'clean'
    })
    if ($cppClean.Count -ne 1) { throw "Run $run did not execute exactly one cpp/clean case." }
    if (-not [bool]$cppClean[0].detected -or [int]$cppClean[0].findingCount -ne 0) {
        throw "Run $run cpp/clean violated clean control: Detected=$($cppClean[0].detected) FindingCount=$($cppClean[0].findingCount)"
    }

    foreach ($case in @($result.results | Where-Object { [string]$_.language -ieq 'Cpp' })) {
        $key = "cpp/$([string]$case.name)"
        $signature = Get-CaseSignature -RunRoot $runRoot -Case $case
        if ($run -eq 1) {
            $reference[$key] = $signature
        }
        elseif (-not $reference.ContainsKey($key) -or $reference[$key] -ne $signature) {
            $unstable.Add([ordered]@{
                run = $run
                case = $key
                expected = if ($reference.ContainsKey($key)) { $reference[$key] } else { '<missing-reference>' }
                actual = $signature
            })
        }
    }

    $runSummaries.Add([ordered]@{
        run = $run
        sourceCommit = [string]$result.sourceCommit
        passed = [bool]$result.passed
        recallPercent = [decimal]$result.detectionRecallPercent
        falsePositiveRatePercent = [decimal]$result.falsePositiveRatePercent
        reproductionRatePercent = [decimal]$result.reproductionRatePercent
        blockingInfrastructureFailures = [int]$result.blockingInfrastructureFailures
        cppCleanDetected = [bool]$cppClean[0].detected
        cppCleanFindingCount = [int]$cppClean[0].findingCount
    })
}

$summary = [ordered]@{
    schema = 'dip.qa-bug-zoo-determinism.v1'
    sourceCommit = $ExpectedSha
    originMain = $originMain
    trackedTreeClean = $true
    runCount = $Runs
    passCount = @($runSummaries | Where-Object { $_.passed }).Count
    detectionRecallPercent = 100
    falsePositiveRatePercent = 0
    reproductionRatePercent = 100
    unstableCaseCount = $unstable.Count
    status = if ($unstable.Count -eq 0) { 'PASS' } else { 'FAIL' }
    referenceCppSignatures = $reference
    unstableCases = @($unstable)
    runs = @($runSummaries)
}

$summaryPath = Join-Path $OutputDirectory 'qa-bug-zoo-determinism-summary.json'
$summary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

if ($unstable.Count -ne 0) {
    throw "Bug Zoo determinism failed: UnstableCaseCount=$($unstable.Count). Summary=$summaryPath"
}

@(
    'DIP QA BUG ZOO DETERMINISM PASS'
    "SourceCommit=$ExpectedSha"
    "Runs=$Runs"
    'Recall=100%'
    'FalsePositiveRate=0%'
    'Reproduction=100%'
    'UnstableCaseCount=0'
) | Set-Content -LiteralPath (Join-Path $OutputDirectory 'DIP_QA_BUG_ZOO_DETERMINISM_PASS.txt') -Encoding UTF8

Write-Host "Bug Zoo determinism PASS: $Runs/$Runs runs, UnstableCaseCount=0" -ForegroundColor Green
Write-Host "Summary: $summaryPath"
