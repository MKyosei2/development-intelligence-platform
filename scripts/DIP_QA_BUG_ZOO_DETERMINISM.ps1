# Purpose: Repeats the exact-revision QA Bug Zoo on Linux/LSan and proves stable findings/tool paths across runs.
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
$script:IsWindowsHost = $env:OS -eq 'Windows_NT'

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

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory=$true)][string]$File,
        [Parameter(Mandatory=$true)][string[]]$Arguments
    )
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$File $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Ensure-DockerLinuxEngine {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Docker CLI is required for Windows-hosted Linux/LSan determinism verification.'
    }

    $isLinux = $false
    try {
        $osType = (& docker info --format '{{.OSType}}' 2>$null | Out-String).Trim()
        $isLinux = $LASTEXITCODE -eq 0 -and $osType -eq 'linux'
    }
    catch {
        $isLinux = $false
    }
    if ($isLinux) { return }

    if (-not $script:IsWindowsHost) {
        throw 'Docker is available but the active engine is not Linux.'
    }

    $desktopCandidates = @(
        (Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Docker\Docker\Docker Desktop.exe')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }

    if ($desktopCandidates.Count -lt 1) {
        throw 'Docker Desktop is installed/required but Docker Desktop.exe could not be found.'
    }

    if (-not (Get-Process -Name 'Docker Desktop' -ErrorAction SilentlyContinue)) {
        Start-Process -FilePath $desktopCandidates[0] | Out-Null
    }

    for ($attempt = 1; $attempt -le 180; $attempt++) {
        Start-Sleep -Seconds 2
        try {
            $osType = (& docker info --format '{{.OSType}}' 2>$null | Out-String).Trim()
            if ($LASTEXITCODE -eq 0 -and $osType -eq 'linux') {
                return
            }
        }
        catch {
            # Retry until the bounded startup deadline; final failure remains fail-closed.
        }
    }

    throw 'Docker Desktop Linux engine did not become ready within the bounded startup window.'
}

function Get-RelativeRepositoryPath {
    param([Parameter(Mandatory=$true)][string]$Path)
    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@(92,47))
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = $root + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Docker determinism output must be inside RepositoryRoot. Output=$full Root=$root"
    }
    return $full.Substring($prefix.Length).Replace('\','/')
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
        if ([bool]$diag.infrastructureTimeout) {
            throw "Infrastructure timeout detected in $($file.FullName). It is neither a product finding nor PASS."
        }
        $exit = if ($null -eq $diag.exitCode) { '<null>' } else { [string]$diag.exitCode }
        $signatures.Add(("{0}|{1}|exit={2}|timedOut={3}|infraTimeout={4}" -f
            [string]$diag.tool,
            [string]$diag.executable,
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
    return ("category={0}|severity={1}|exception={2}|rule={3}|findings={4}|detected={5}|reproduced={6}|tools={7}" -f
        [string]$Case.matchedCategory,
        [string]$Case.matchedSeverity,
        [string]$Case.matchedExceptionType,
        [string]$Case.matchedRuleId,
        [int]$Case.findingCount,
        [bool]$Case.detected,
        [bool]$Case.reproduced,
        ($tools -join ';'))
}

$head = Invoke-GitText @('rev-parse','HEAD')
if ([string]::IsNullOrWhiteSpace($ExpectedSha)) { $ExpectedSha = $head }
if ($ExpectedSha -notmatch '^[0-9a-fA-F]{40}$') { throw "ExpectedSha must be a full 40-character SHA: $ExpectedSha" }
if ($head -ne $ExpectedSha) { throw "HEAD mismatch. expected=$ExpectedSha actual=$head" }

& git -C $RepositoryRoot fetch origin main --prune
if ($LASTEXITCODE -ne 0) { throw 'Could not fetch origin/main.' }
$originMain = Invoke-GitText @('rev-parse','origin/main')
if ($originMain -ne $ExpectedSha) {
    throw "Exact-revision determinism requires HEAD == origin/main. HEAD=$ExpectedSha origin/main=$originMain"
}

$trackedStatus = Invoke-GitText @('status','--porcelain','--untracked-files=no')
if (-not [string]::IsNullOrWhiteSpace($trackedStatus)) {
    throw "Tracked working tree must be clean before determinism verification:`n$trackedStatus"
}

$project = Join-Path $RepositoryRoot 'tools\Dip.QaBugZooVerifier\Dip.QaBugZooVerifier.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { throw "Bug Zoo verifier project is missing: $project" }

if (Test-Path -LiteralPath $OutputDirectory) { Remove-Item -LiteralPath $OutputDirectory -Recurse -Force }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$useDocker = $script:IsWindowsHost
$composePath = Join-Path $RepositoryRoot 'docker-compose.verification.yml'
$composeProject = 'dip-qa-det-' + $ExpectedSha.Substring(0,8)
if ($useDocker) {
    Ensure-DockerLinuxEngine
    if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) { throw "Missing compose file: $composePath" }
    Push-Location $RepositoryRoot
    try {
        Invoke-NativeChecked 'docker' @('compose','-p',$composeProject,'-f',$composePath,'build','verifier')
    }
    finally {
        Pop-Location
    }
}

$reference = @{}
$unstable = [Collections.Generic.List[object]]::new()
$runSummaries = [Collections.Generic.List[object]]::new()
$requiredCppCases = @('clean','memory_leak','buffer_overflow','double_free','use_after_free')

for ($run = 1; $run -le $Runs; $run++) {
    $runName = 'run-{0:D2}' -f $run
    $runRoot = Join-Path $OutputDirectory $runName
    New-Item -ItemType Directory -Force -Path $runRoot | Out-Null
    Write-Host "[$run/$Runs] QA Bug Zoo exact-revision run: $ExpectedSha" -ForegroundColor Cyan

    if ($useDocker) {
        $relativeRun = Get-RelativeRepositoryPath $runRoot
        $containerOutput = '/work/' + $relativeRun
        $containerCommand = @(
            'git config --global --add safe.directory /work',
            '&& dotnet run',
            '--project /work/tools/Dip.QaBugZooVerifier/Dip.QaBugZooVerifier.csproj',
            '--configuration Release',
            '--',
            '--root /work',
            "--output '$containerOutput'",
            "--expected-sha '$ExpectedSha'",
            "--require-languages '$($RequireLanguages -join ',')'"
        ) -join ' '

        Push-Location $RepositoryRoot
        try {
            Invoke-NativeChecked 'docker' @(
                'compose','-p',$composeProject,'-f',$composePath,
                'run','--rm','--no-deps','verifier',$containerCommand)
        }
        finally {
            Pop-Location
        }
    }
    else {
        $arguments = @(
            'run','--project',$project,'--configuration','Release','--',
            '--root',$RepositoryRoot,
            '--output',$runRoot,
            '--expected-sha',$ExpectedSha,
            '--require-languages',($RequireLanguages -join ',')
        )
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) { throw "Bug Zoo run $run failed with exit code $LASTEXITCODE. Evidence: $runRoot" }
    }

    $jsonPath = Join-Path $runRoot 'qa-bug-zoo-results.json'
    if (-not (Test-Path -LiteralPath $jsonPath -PathType Leaf)) { throw "Run $run did not produce $jsonPath" }
    $result = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json

    if ([string]$result.sourceCommit -ne $ExpectedSha) { throw "Run $run sourceCommit mismatch." }
    if ($result.sourceWorkingTreeDirty -ne $false) { throw "Run $run was measured from a dirty tracked source tree." }
    if (-not [bool]$result.passed) { throw "Run $run did not report passed=true." }
    if ([decimal]$result.detectionRecallPercent -ne 100) { throw "Run $run recall is not 100%." }
    if ([decimal]$result.falsePositiveRatePercent -ne 0) { throw "Run $run false-positive rate is not 0%." }
    if ([decimal]$result.reproductionRatePercent -ne 100) { throw "Run $run reproduction rate is not 100%." }
    if ([int]$result.blockingInfrastructureFailures -ne 0) { throw "Run $run has blocking infrastructure failures." }

    foreach ($diagFile in @(Get-ChildItem -LiteralPath $runRoot -Filter 'execution-diagnostics.json' -Recurse -File)) {
        $diag = Get-Content -LiteralPath $diagFile.FullName -Raw | ConvertFrom-Json
        if ([bool]$diag.infrastructureTimeout) {
            throw "Run $run contains infrastructure timeout evidence: $($diagFile.FullName)"
        }
    }

    $cppCases = @($result.results | Where-Object { [string]$_.language -ieq 'Cpp' })
    $executedCppCaseNames = @($cppCases | ForEach-Object { [string]$_.name })
    $missingCppCases = @($requiredCppCases | Where-Object { $_ -notin $executedCppCaseNames })
    if ($missingCppCases.Count -gt 0) {
        throw "Run $run skipped required C++ fixture(s): $($missingCppCases -join ', '). Linux/LSan execution is mandatory."
    }

    $cppClean = @($cppCases | Where-Object { [string]$_.name -eq 'clean' })
    if ($cppClean.Count -ne 1) { throw "Run $run did not execute exactly one cpp/clean case." }
    if (-not [bool]$cppClean[0].detected -or [int]$cppClean[0].findingCount -ne 0) {
        throw "Run $run cpp/clean violated the clean control. Detected=$($cppClean[0].detected) FindingCount=$($cppClean[0].findingCount)"
    }

    $memoryLeak = @($cppCases | Where-Object { [string]$_.name -eq 'memory_leak' })
    if ($memoryLeak.Count -ne 1) { throw "Run $run did not execute exactly one cpp/memory_leak case." }
    if (-not [bool]$memoryLeak[0].detected
        -or [string]$memoryLeak[0].matchedCategory -ne 'memory-leak'
        -or [string]$memoryLeak[0].matchedExceptionType -ne 'MemoryLeak'
        -or [string]$memoryLeak[0].matchedRuleId -ne 'lsan') {
        throw "Run $run cpp/memory_leak did not produce the required LeakSanitizer evidence."
    }

    foreach ($case in $cppCases) {
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
        cppMemoryLeakCategory = [string]$memoryLeak[0].matchedCategory
        cppMemoryLeakRule = [string]$memoryLeak[0].matchedRuleId
    })
}

$passCount = @($runSummaries | Where-Object { $_.passed }).Count
$summary = [ordered]@{
    schema = 'dip.qa-bug-zoo-determinism.v1'
    sourceCommit = $ExpectedSha
    originMain = $originMain
    trackedTreeClean = $true
    runtime = if ($useDocker) { 'linux-docker-lsan' } else { 'linux-host-lsan' }
    runCount = $Runs
    passCount = $passCount
    detectionRecallPercent = 100
    falsePositiveRatePercent = 0
    reproductionRatePercent = 100
    unstableCaseCount = $unstable.Count
    status = if ($passCount -eq $Runs -and $unstable.Count -eq 0) { 'PASS' } else { 'FAIL' }
    referenceCppSignatures = $reference
    unstableCases = @($unstable)
    runs = @($runSummaries)
}

$summaryPath = Join-Path $OutputDirectory 'qa-bug-zoo-determinism-summary.json'
$summary | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

if ($passCount -ne $Runs) { throw "Bug Zoo determinism failed: passCount=$passCount/$Runs" }
if ($unstable.Count -ne 0) { throw "Bug Zoo determinism failed: UnstableCaseCount=$($unstable.Count). Summary=$summaryPath" }

@(
    'DIP QA BUG ZOO DETERMINISM PASS'
    "SourceCommit=$ExpectedSha"
    "Runtime=$($summary.runtime)"
    "Runs=$Runs"
    "PassCount=$passCount"
    'Recall=100%'
    'FalsePositiveRate=0%'
    'Reproduction=100%'
    'UnstableCaseCount=0'
    'CppCleanFindingCount=0'
    'MemoryLeakRule=lsan'
) | Set-Content -LiteralPath (Join-Path $OutputDirectory 'DIP_QA_BUG_ZOO_DETERMINISM_PASS.txt') -Encoding UTF8

Write-Host "Bug Zoo determinism PASS: $Runs/$Runs runs, Recall=100%, FP=0%, Reproduction=100%, UnstableCaseCount=0" -ForegroundColor Green
Write-Host "Summary: $summaryPath"
