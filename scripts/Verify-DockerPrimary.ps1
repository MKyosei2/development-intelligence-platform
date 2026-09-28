# Purpose: Primary exact-revision verification on Linux Docker, isolated from Windows Application Control.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = '',
    [string]$ExpectedSha = '',
    [int]$SoakSeconds = 900,
    [int]$SoakRequestsPerSecond = 25,
    [switch]$SkipPlaywright,
    [switch]$KeepContainers
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'PostgreSqlRuntime.ps1')

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
} else {
    $RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
}

$composeFile = Join-Path $RepositoryRoot 'docker-compose.verification.yml'
if (-not (Test-Path -LiteralPath $composeFile -PathType Leaf)) {
    throw "Verification compose file is missing: $composeFile"
}

foreach ($command in @('git','docker')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command is required."
    }
}

Invoke-DipNativeChecked `
    -File 'docker' `
    -Arguments @('info') `
    -DiscardOutput `
    -FailureMessage 'Docker Engine is not running.'

function Get-FreeTcpPort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
    $listener.Start()
    try {
        return ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Invoke-DockerChecked {
    param([string[]]$Arguments)

    $result = Invoke-DipNativeCapture -File 'docker' -Arguments $Arguments
    if ($result.ExitCode -ne 0) {
        $message = "docker $($Arguments -join ' ') failed with exit code $($result.ExitCode)."
        if (-not [string]::IsNullOrWhiteSpace($result.Text)) {
            $message += "`n$($result.Text)"
        }
        throw $message
    }

    return @($result.Lines)
}

function Invoke-ComposeChecked {
    param([string[]]$Arguments)

    $prefix = @(
        'compose',
        '-p', $script:ComposeProject,
        '-f', $script:ComposeFile
    )

    Invoke-DockerChecked -Arguments ($prefix + $Arguments)
}

function Invoke-Verifier {
    param([Parameter(Mandatory=$true)][string]$Command)

    Invoke-ComposeChecked @(
        'run',
        '--rm',
        '--no-deps',
        'verifier',
        $Command
    )
}

function Invoke-EfVerifier {
    param([Parameter(Mandatory=$true)][string]$EfArguments)

    if ([string]::IsNullOrWhiteSpace($EfArguments)) {
        throw 'EF arguments are required.'
    }

    # Invoke-Verifier creates an ephemeral docker compose run --rm container.
    # Restore the local tool and execute dotnet-ef in the SAME container.
    Invoke-Verifier ("dotnet tool restore && dotnet ef " + $EfArguments)
}
function Wait-Http {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$Url,
        [int]$TimeoutSeconds = 180
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = ''

    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Write-Host "HTTP PASS: $Name $Url" -ForegroundColor Green
                return
            }
            $last = "HTTP $($response.StatusCode)"
        }
        catch {
            $last = $_.Exception.Message
        }

        Start-Sleep -Seconds 1
    }

    throw "$Name did not become healthy: $Url. Last=$last"
}

function Get-TrxCounter {
    param($Counters,[string]$Name)

    $attribute = $Counters.Attributes[$Name]
    if ($null -eq $attribute -or [string]::IsNullOrWhiteSpace($attribute.Value)) {
        return 0
    }

    return [int]$attribute.Value
}

function Assert-Trx {
    param(
        [Parameter(Mandatory=$true)][string]$Path,
        [int]$MinimumExecuted = 1
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "TRX does not exist: $Path"
    }

    [xml]$trx = Get-Content -LiteralPath $Path -Raw
    $counters = $trx.SelectSingleNode("//*[local-name()='Counters']")

    if ($null -eq $counters) {
        throw "TRX has no Counters node: $Path"
    }

    $executed = Get-TrxCounter $counters 'executed'
    $passed = Get-TrxCounter $counters 'passed'
    $failed = Get-TrxCounter $counters 'failed'
    $error = Get-TrxCounter $counters 'error'
    $timeout = Get-TrxCounter $counters 'timeout'
    $aborted = Get-TrxCounter $counters 'aborted'

    if ($executed -lt $MinimumExecuted) {
        throw "Zero/insufficient test execution is forbidden. TRX=$Path executed=$executed minimum=$MinimumExecuted"
    }

    if ($failed -gt 0 -or $error -gt 0 -or $timeout -gt 0 -or $aborted -gt 0) {
        throw "TRX is not clean. failed=$failed error=$error timeout=$timeout aborted=$aborted"
    }

    Write-Host "TRX PASS: $Path executed=$executed passed=$passed" -ForegroundColor Green
}

function Invoke-StrictDockerTest {
    param(
        [Parameter(Mandatory=$true)][string]$Project,
        [Parameter(Mandatory=$true)][string]$Name,
        [string]$Filter = ''
    )

    $trx = "/work/artifacts/docker-primary/TestResults/$Name.trx"
    $filterPart = ''

    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        if ($Filter -notmatch '^[A-Za-z0-9_.~|&!=() -]+$') {
            throw 'The strict-test filter contains unsupported shell metacharacters.'
        }

        $filterPart = " --filter '$Filter'"
    }

    $command =
        "dotnet test '$Project' -c Release --no-build" +
        $filterPart +
        " --logger 'trx;LogFileName=$Name.trx'" +
        " --results-directory /work/artifacts/docker-primary/TestResults"

    Invoke-Verifier $command

    Assert-Trx `
        -Path (Join-Path $RepositoryRoot "artifacts\docker-primary\TestResults\$Name.trx") `
        -MinimumExecuted 1
}

function Invoke-ComposePsql {
    param(
        [string]$Database = 'dip_verify',
        [Parameter(Mandatory=$true)][string]$Sql,
        [switch]$TuplesOnly
    )

    $args = @(
        'compose',
        '-p', $script:ComposeProject,
        '-f', $script:ComposeFile,
        'exec',
        '-T',
        '-e', "PGPASSWORD=$($script:PostgresPassword)",
        'postgres',
        'psql',
        '-U','dip',
        '-d',$Database,
        '-v','ON_ERROR_STOP=1',
        '-w'
    )

    if ($TuplesOnly) {
        $args += @('-A','-t')
    }

    $args += @('-f','-')

    $result = Invoke-DipNativeInputCapture `
        -File 'docker' `
        -Arguments $args `
        -InputText $Sql
    if ($result.ExitCode -ne 0) {
        $message = "psql failed for database '$Database'."
        if (-not [string]::IsNullOrWhiteSpace($result.Text)) {
            $message += "`n$($result.Text)"
        }
        throw $message
    }

    return @($result.Lines)
}

function Wait-ComposePostgresReady {
    param([int]$TimeoutSeconds = 120)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $attempt = 0
    $last = ''

    while ((Get-Date) -lt $deadline) {
        $attempt++
        try {
            $probe = (
                Invoke-ComposePsql -Sql 'SELECT 1;' -TuplesOnly |
                Out-String
            ).Trim()

            if ($probe -eq '1') {
                Write-Host "PostgreSQL readiness PASS after $attempt attempt(s)." -ForegroundColor Green
                return $attempt
            }

            $last = "Unexpected probe result: $probe"
        }
        catch {
            $last = $_.Exception.Message
        }

        Start-Sleep -Seconds 1
    }

    throw "PostgreSQL did not become ready after $attempt attempt(s). Last=$last"
}

function Write-NativeDiagnosticLog {
    param(
        [Parameter(Mandatory=$true)]$Result,
        [Parameter(Mandatory=$true)][string]$Path
    )

    @($Result.Lines) |
        Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-PythonAnalysisFailureClassification {
    param(
        [string]$BuildText,
        [int]$BuildExitCode,
        [string]$PipText,
        [int]$PipExitCode
    )

    if ($BuildExitCode -eq 0) { return 'PASS' }

    $all = $BuildText + [Environment]::NewLine + $PipText

    if ($all -match 'Temporary failure in name resolution|Name or service not known|Could not resolve host|Network is unreachable|ReadTimeout|Connection.*timed out') {
        return 'NETWORK_OR_DNS_FAILURE'
    }
    if ($all -match 'No matching distribution found|Could not find a version that satisfies the requirement|ResolutionImpossible') {
        return 'DEPENDENCY_RESOLUTION_FAILURE'
    }
    if ($all -match 'Failed building wheel|gcc.*error|clang.*error') {
        return 'NATIVE_WHEEL_OR_BUILD_FAILURE'
    }
    if ($all -match 'Multiple top-level packages discovered|package directory .* does not exist') {
        return 'PACKAGING_OR_PYPROJECT_FAILURE'
    }
    if ($BuildText -match 'COPY failed|failed to compute cache key') {
        return 'DOCKER_BUILD_CONTEXT_OR_COPY_FAILURE'
    }
    if ($PipExitCode -ne 0) {
        return 'PIP_INSTALL_FAILURE_REVIEW_VERBOSE_LOG'
    }

    return 'DOCKERFILE_OR_BUILDKIT_FAILURE_PIP_INSTALL_IS_HEALTHY'
}

function Invoke-PythonAnalysisBuildWithDiagnostics {
    $diagnosticRoot = Join-Path $script:OutDir 'python-analysis-build'
    New-Item -ItemType Directory -Force -Path $diagnosticRoot | Out-Null

    $composeArgs = @(
        'compose',
        '-p', $script:ComposeProject,
        '-f', $script:ComposeFile,
        'build',
        '--progress', 'plain',
        'python-analysis'
    )

    $compose = Invoke-DipNativeCapture -File 'docker' -Arguments $composeArgs
    Write-NativeDiagnosticLog -Result $compose -Path (Join-Path $diagnosticRoot 'compose-build.log')

    if ($compose.ExitCode -eq 0) {
        [ordered]@{
            schema = 'dip.python-analysis-build-diagnostic.v1'
            sourceCommit = $script:SourceSha
            status = 'PASS'
            classification = 'COMPOSE_BUILD_PASS'
            composeExitCode = 0
            directBuildExitCode = $null
            pipProbeExitCode = $null
        } |
            ConvertTo-Json -Depth 5 |
            Set-Content (Join-Path $diagnosticRoot 'diagnostic.json') -Encoding UTF8

        @($compose.Lines) | ForEach-Object { Write-Host $_ }
        return
    }

    $dockerfile = Join-Path $RepositoryRoot 'infrastructure\docker\analysis.Dockerfile'
    $tag = 'dip-analysis-diagnostic:' + $script:SourceSha.Substring(0,12).ToLowerInvariant()
    $direct = Invoke-DipNativeCapture `
        -File 'docker' `
        -Arguments @(
            'build',
            '--no-cache',
            '--progress=plain',
            '-f', $dockerfile,
            '-t', $tag,
            $RepositoryRoot
        )
    Write-NativeDiagnosticLog -Result $direct -Path (Join-Path $diagnosticRoot 'direct-build.log')

    $pythonRoot = Join-Path $RepositoryRoot 'services\python-analysis'
    $mount = "type=bind,source=$pythonRoot,target=/src,readonly"
    $pipCommand = 'set -eu; python --version; python -m pip --version; python -m pip install --upgrade pip; python -m pip install -vvv .; python -c "import app; import fastapi; import numpy; import pandas; import sklearn; print(''PYTHON_IMPORT_PROBE_OK'')"'
    $pip = Invoke-DipNativeCapture `
        -File 'docker' `
        -Arguments @(
            'run','--rm',
            '--mount',$mount,
            '-w','/src',
            'python:3.12-slim',
            'sh','-lc',$pipCommand
        )
    Write-NativeDiagnosticLog -Result $pip -Path (Join-Path $diagnosticRoot 'pip-probe.log')

    $classification = Get-PythonAnalysisFailureClassification `
        -BuildText ($compose.Text + [Environment]::NewLine + $direct.Text) `
        -BuildExitCode $direct.ExitCode `
        -PipText $pip.Text `
        -PipExitCode $pip.ExitCode

    [ordered]@{
        schema = 'dip.python-analysis-build-diagnostic.v1'
        sourceCommit = $script:SourceSha
        status = 'FAIL'
        classification = $classification
        composeExitCode = $compose.ExitCode
        directBuildExitCode = $direct.ExitCode
        pipProbeExitCode = $pip.ExitCode
        dockerfileChangedByThisFix = $false
        pyprojectChangedByThisFix = $false
    } |
        ConvertTo-Json -Depth 5 |
        Set-Content (Join-Path $diagnosticRoot 'diagnostic.json') -Encoding UTF8

    throw "Python Analysis image build failed. Classification=$classification. See $diagnosticRoot."
}

function Get-P95([double[]]$Values) {
    if ($Values.Count -eq 0) { return 0 }
    $sorted = @($Values | Sort-Object)
    $index = [Math]::Max(0,[Math]::Ceiling($sorted.Count * .95) - 1)
    return [Math]::Round($sorted[$index],3)
}

function Get-Percentile([double[]]$Values,[double]$P) {
    if ($Values.Count -eq 0) { return 0 }
    $sorted = @($Values | Sort-Object)
    $index = [Math]::Max(0,[Math]::Ceiling($sorted.Count * $P) - 1)
    return [Math]::Round($sorted[$index],3)
}

function Invoke-PacedSoak {
    param([string]$Url,[int]$Seconds,[int]$Rps)

    $count = $Seconds * $Rps
    $latencies = [Collections.Generic.List[double]]::new()
    $errors = 0
    $wall = [Diagnostics.Stopwatch]::StartNew()

    for ($index = 0; $index -lt $count; $index++) {
        $targetMs = $index * (1000.0 / $Rps)

        while ($wall.Elapsed.TotalMilliseconds -lt $targetMs) {
            $remaining = $targetMs - $wall.Elapsed.TotalMilliseconds
            if ($remaining -gt 2) {
                Start-Sleep -Milliseconds ([Math]::Min(10,[int]$remaining))
            }
        }

        $watch = [Diagnostics.Stopwatch]::StartNew()
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 10
            if ($response.StatusCode -ne 200) { $errors++ }
        }
        catch {
            $errors++
        }
        finally {
            $watch.Stop()
            $latencies.Add($watch.Elapsed.TotalMilliseconds)
        }
    }

    $wall.Stop()

    return [ordered]@{
        requests = $count
        targetRequestsPerSecond = $Rps
        elapsedSeconds = [Math]::Round($wall.Elapsed.TotalSeconds,3)
        actualRequestsPerSecond = [Math]::Round($count / [Math]::Max(.001,$wall.Elapsed.TotalSeconds),3)
        errors = $errors
        errorRatePercent = [Math]::Round($errors * 100.0 / [Math]::Max(1,$count),4)
        p50Ms = Get-Percentile -Values $latencies.ToArray() -P 0.50
        p95Ms = Get-Percentile -Values $latencies.ToArray() -P 0.95
        p99Ms = Get-Percentile -Values $latencies.ToArray() -P 0.99
    }
}


function Wait-HttpBatch {
    param(
        [System.Collections.ArrayList]$Pending,
        [System.Collections.Generic.List[double]]$Latencies,
        [System.Collections.Generic.List[string]]$Errors
    )

    while ($Pending.Count -gt 0) {
        for ($index = $Pending.Count - 1; $index -ge 0; $index--) {
            $entry = $Pending[$index]
            if (-not $entry.Task.IsCompleted) { continue }

            $entry.Stopwatch.Stop()
            $Latencies.Add($entry.Stopwatch.Elapsed.TotalMilliseconds)

            try {
                $response = $entry.Task.GetAwaiter().GetResult()
                if (-not $response.IsSuccessStatusCode) {
                    $Errors.Add("$($entry.Kind):$([int]$response.StatusCode)")
                }
                $response.Dispose()
            }
            catch {
                $Errors.Add("$($entry.Kind)-ex:$($_.Exception.Message)")
            }

            if ($entry.Content) { $entry.Content.Dispose() }
            $Pending.RemoveAt($index)
        }

        if ($Pending.Count -gt 0) { Start-Sleep -Milliseconds 2 }
    }
}

function Get-ApiContainerMemoryMb {
    $containerId = (
        Invoke-DipNativeChecked `
            -File 'docker' `
            -Arguments @('compose','-p',$script:ComposeProject,'-f',$script:ComposeFile,'ps','-q','api') `
            -FailureMessage 'Could not query API container for memory measurement.' |
        Out-String
    ).Trim()
    if ([string]::IsNullOrWhiteSpace($containerId)) {
        throw 'Could not resolve API container for memory measurement.'
    }

    $usage = (
        Invoke-DipNativeChecked `
            -File 'docker' `
            -Arguments @('stats','--no-stream','--format','{{.MemUsage}}',$containerId) `
            -FailureMessage 'docker stats failed for API container.' |
        Out-String
    ).Trim()
    if ([string]::IsNullOrWhiteSpace($usage)) {
        throw 'docker stats returned no memory usage.'
    }

    $left = ($usage -split '/')[0].Trim()
    if ($left -match '^([0-9.]+)GiB$') { return [double]$Matches[1] * 1024.0 }
    if ($left -match '^([0-9.]+)MiB$') { return [double]$Matches[1] }
    if ($left -match '^([0-9.]+)KiB$') { return [double]$Matches[1] / 1024.0 }
    if ($left -match '^([0-9.]+)B$') { return [double]$Matches[1] / 1MB }
    throw "Unsupported docker memory format: $left"
}

function Invoke-PortfolioBenchmarkExternal {
    param(
        [Parameter(Mandatory=$true)][string]$ApiBase,
        [int]$ProjectCount = 250,
        [int]$ProjectListIterations = 50,
        [int]$CollectorIterations = 30,
        [int]$MetricQueryIterations = 10,
        [int]$MetricRows = 1000000,
        [int]$BurstRequests = 100,
        [int]$BurstTargetRequestsPerSecond = 100,
        [double]$MaxSoakErrorRatePercent = 0.1,
        [double]$MaxSoakMemoryGrowthMb = 256
    )

    $project = $null
    for ($index = 0; $index -lt $ProjectCount; $index++) {
        $body = @{
            name = "Docker Portfolio Benchmark $index"
            repositoryUrl = "https://github.com/example/dip-docker-benchmark-$index"
            defaultBranch = 'main'
            releaseDateUtc = $null
        } | ConvertTo-Json

        $created = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/projects" -ContentType 'application/json' -Body $body
        if ($index -eq 0) { $project = $created }
    }

    1..10 | ForEach-Object { Invoke-WebRequest -UseBasicParsing "$ApiBase/api/projects" | Out-Null }

    $projectMs = [Collections.Generic.List[double]]::new()
    1..$ProjectListIterations | ForEach-Object {
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-WebRequest -UseBasicParsing "$ApiBase/api/projects"
        $watch.Stop()
        if ($response.StatusCode -ne 200) { throw 'Project list benchmark failed.' }
        $projectMs.Add($watch.Elapsed.TotalMilliseconds)
    }

    $collectorMs = [Collections.Generic.List[double]]::new()
    $lastPipeline = $null
    1..$CollectorIterations | ForEach-Object {
        $now = [DateTimeOffset]::UtcNow
        $payload = @{
            schemaVersion = '1.0'
            reportId = "docker-bench-$($_)-$([guid]::NewGuid().ToString('N'))"
            externalRunId = "docker-bench-run-$($_)-$([guid]::NewGuid().ToString('N'))"
            attemptNumber = 1
            collectorType = 'portfolio-benchmark'
            collectorVersion = '1.0.0'
            projectId = $project.id
            repository = [string]$project.repositoryUrl
            commitSha = $script:SourceSha
            branch = 'main'
            startedAt = $now.AddSeconds(-1).ToString('o')
            finishedAt = $now.ToString('o')
            status = 'Succeeded'
            metrics = @(@{
                name='frame_time'; value=16.0; unit='ms'; baselineValue=16.6;
                environmentId='docker-primary'; scenarioId='benchmark'; platform='linux';
                buildConfiguration='Release'; benchmarkVersion='1'
            })
            issues = @()
        } | ConvertTo-Json -Depth 8

        $watch = [Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/v1/collector-reports" -ContentType 'application/json' -Body $payload
        $watch.Stop()
        $collectorMs.Add($watch.Elapsed.TotalMilliseconds)
        $lastPipeline = $response.pipelineRunId
    }

    if (-not $lastPipeline) { throw 'Collector benchmark returned no pipeline id.' }

    Add-Type -AssemblyName System.Net.Http
    $client = [Net.Http.HttpClient]::new()
    $tasks = New-Object System.Collections.ArrayList
    $burst = [Diagnostics.Stopwatch]::StartNew()

    for ($index = 0; $index -lt $BurstRequests; $index++) {
        $targetMs = $index * (1000.0 / $BurstTargetRequestsPerSecond)
        while ($burst.Elapsed.TotalMilliseconds -lt $targetMs) { Start-Sleep -Milliseconds 1 }
        [void]$tasks.Add($client.GetAsync("$ApiBase/api/projects",[Net.Http.HttpCompletionOption]::ResponseHeadersRead))
    }

    [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks.ToArray([Threading.Tasks.Task]))
    $burst.Stop()
    $burstErrors = 0
    foreach ($task in $tasks) {
        if (-not $task.Result.IsSuccessStatusCode) { $burstErrors++ }
        $task.Result.Dispose()
    }
    $client.Dispose()

    $burstErrorRate = [Math]::Round($burstErrors * 100.0 / [Math]::Max(1,$BurstRequests),4)

    $seedSql = 'INSERT INTO "Metrics" ("Id","PipelineRunId","BaselinePipelineRunId","Name","Value","Unit","BaselineValue","Difference","DifferencePercent","Severity","ComparisonStatus","EnvironmentId","ScenarioId","Platform","BuildConfiguration","BenchmarkVersion") SELECT gen_random_uuid(),''{0}''::uuid,NULL,''portfolio.metric.''||(g%100),(g%1000)::numeric,''ms'',NULL,NULL,NULL,0,0,''docker-primary'',''scale-query'',''linux'',''Release'',''1'' FROM generate_series(1,{1}) g;' -f $lastPipeline,$MetricRows
    Invoke-ComposePsql -Sql $seedSql | Out-Null

    $querySql = 'SELECT "Name",AVG("Value") FROM "Metrics" WHERE "PipelineRunId"=''{0}''::uuid GROUP BY "Name" ORDER BY "Name";' -f $lastPipeline
    $metricMs = [Collections.Generic.List[double]]::new()
    1..$MetricQueryIterations | ForEach-Object {
        $watch = [Diagnostics.Stopwatch]::StartNew()
        Invoke-ComposePsql -Sql $querySql | Out-Null
        $watch.Stop()
        $metricMs.Add($watch.Elapsed.TotalMilliseconds)
    }

    $memoryBefore = [Math]::Round((Get-ApiContainerMemoryMb),3)
    $soak = Invoke-PacedSoak -Url "$ApiBase/api/projects" -Seconds $SoakSeconds -Rps $SoakRequestsPerSecond
    $memoryAfter = [Math]::Round((Get-ApiContainerMemoryMb),3)
    $memoryGrowth = [Math]::Round($memoryAfter - $memoryBefore,3)

    $projectP50 = Get-Percentile -Values $projectMs.ToArray() -P 0.50
    $projectP95 = Get-Percentile -Values $projectMs.ToArray() -P 0.95
    $projectP99 = Get-Percentile -Values $projectMs.ToArray() -P 0.99
    $collectorP95 = Get-Percentile -Values $collectorMs.ToArray() -P 0.95
    $metricP95 = Get-Percentile -Values $metricMs.ToArray() -P 0.95

    $passed = $projectP95 -lt 500 -and $collectorP95 -lt 250 -and $burstErrorRate -eq 0 -and $metricP95 -lt 1000 -and $soak.errorRatePercent -le $MaxSoakErrorRatePercent -and $memoryGrowth -le $MaxSoakMemoryGrowthMb

    $result = [ordered]@{
        schema='dip-portfolio-benchmark-v6-docker'
        runtime='linux-docker'
        sourceCommit=$script:SourceSha
        measuredAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
        scenarios=[ordered]@{
            projectList=[ordered]@{population=$ProjectCount;iterations=$ProjectListIterations;p50Ms=$projectP50;p95Ms=$projectP95;p99Ms=$projectP99;targetP95Ms=500;passed=$projectP95 -lt 500}
            collector=[ordered]@{iterations=$CollectorIterations;p95Ms=$collectorP95;targetP95Ms=250;passed=$collectorP95 -lt 250}
            burst=[ordered]@{requests=$BurstRequests;targetRequestsPerSecond=$BurstTargetRequestsPerSecond;elapsedSeconds=[Math]::Round($burst.Elapsed.TotalSeconds,3);errorRatePercent=$burstErrorRate;targetErrorRatePercent=0;passed=$burstErrorRate -eq 0}
            metricScale=[ordered]@{rows=$MetricRows;iterations=$MetricQueryIterations;p95Ms=$metricP95;targetP95Ms=1000;passed=$metricP95 -lt 1000}
            soak=$soak
            memory=[ordered]@{beforeMb=$memoryBefore;afterMb=$memoryAfter;growthMb=$memoryGrowth;maxGrowthMb=$MaxSoakMemoryGrowthMb;passed=$memoryGrowth -le $MaxSoakMemoryGrowthMb}
        }
        passed=$passed
    }

    $result | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $script:OutDir 'portfolio-benchmark-results.json') -Encoding UTF8
    if (-not $passed) { throw 'One or more Docker primary benchmark/soak gates failed.' }
}

function Invoke-GovernanceLoadExternal {
    param(
        [Parameter(Mandatory=$true)][string]$ApiBase,
        [int]$ProjectCount = 10,
        [int]$FindingsPerProject = 5000,
        [int]$ConcurrentReaders = 64,
        [int]$ConcurrentWriters = 32,
        [int]$ReadRequests = 500,
        [int]$WriteRequests = 200
    )

    $projectIds = @()
    for ($index=0;$index -lt $ProjectCount;$index++) {
        $body = @{name="Docker Governance Load $index";repositoryUrl="https://example.invalid/docker-load-$index";defaultBranch='main';releaseDateUtc=$null} | ConvertTo-Json
        $project = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/projects" -ContentType 'application/json' -Body $body
        $projectIds += [Guid]$project.id
    }

    foreach ($projectId in $projectIds) {
        $sql = @"
INSERT INTO qa_finding_states(id,project_id,fingerprint,last_finding_id,subject_key,last_commit_sha,status,occurrences,team,workstream,ownership_source,disposition_reason,updated_by,first_seen_at_utc,last_seen_at_utc,updated_at_utc,version)
SELECT gen_random_uuid(),'$projectId'::uuid,md5('${projectId}:'||g::text)||md5('${projectId}:x:'||g::text),gen_random_uuid(),'load:'||g,'0123456789abcdef0123456789abcdef01234567','Confirmed',1,'Load Team','Load','Rule',NULL,NULL,clock_timestamp()+(g||' microseconds')::interval,clock_timestamp()+(g||' microseconds')::interval,now(),1 FROM generate_series(1,$FindingsPerProject) g;
"@
        Invoke-ComposePsql -Sql $sql | Out-Null
    }

    Add-Type -AssemblyName System.Net.Http
    $handler = [Net.Http.HttpClientHandler]::new()
    $client = [Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(30)
    $readLat = [Collections.Generic.List[double]]::new()
    $writeLat = [Collections.Generic.List[double]]::new()
    $errors = [Collections.Generic.List[string]]::new()

    for($base=0;$base -lt $ReadRequests;$base += $ConcurrentReaders){
        $pending = [Collections.ArrayList]::new()
        $count = [Math]::Min($ConcurrentReaders,$ReadRequests-$base)
        for($offset=0;$offset -lt $count;$offset++){
            $requestIndex=$base+$offset
            $projectId=$projectIds[$requestIndex % $projectIds.Count]
            $watch=[Diagnostics.Stopwatch]::StartNew()
            $task=$client.GetAsync("$ApiBase/api/v1/qa-governance/projects/$projectId/findings?pageSize=100")
            [void]$pending.Add([pscustomobject]@{Task=$task;Stopwatch=$watch;Kind='read';Content=$null})
        }
        Wait-HttpBatch -Pending $pending -Latencies $readLat -Errors $errors
    }

    $targets=@()
    foreach($projectId in $projectIds){
        $page=Invoke-RestMethod "$ApiBase/api/v1/qa-governance/projects/$projectId/findings?pageSize=$([Math]::Min(100,$WriteRequests))"
        foreach($item in $page.items){
            $targets += @{projectId=$projectId;fingerprint=$item.fingerprint;version=$item.version}
            if($targets.Count -ge $WriteRequests){break}
        }
        if($targets.Count -ge $WriteRequests){break}
    }

    for($base=0;$base -lt $targets.Count;$base += $ConcurrentWriters){
        $pending=[Collections.ArrayList]::new()
        $count=[Math]::Min($ConcurrentWriters,$targets.Count-$base)
        for($offset=0;$offset -lt $count;$offset++){
            $target=$targets[$base+$offset]
            $payload=@{projectId=$target.projectId;fingerprint=$target.fingerprint;status='Assigned';reason='Docker primary governance load';expectedVersion=$target.version}|ConvertTo-Json
            $content=[Net.Http.StringContent]::new($payload,[Text.Encoding]::UTF8,'application/json')
            $watch=[Diagnostics.Stopwatch]::StartNew()
            $task=$client.PostAsync("$ApiBase/api/v1/qa-governance/findings/transition",$content)
            [void]$pending.Add([pscustomobject]@{Task=$task;Stopwatch=$watch;Kind='write';Content=$content})
        }
        Wait-HttpBatch -Pending $pending -Latencies $writeLat -Errors $errors
    }

    $client.Dispose()
    $readP95=Get-P95 $readLat.ToArray()
    $writeP95=Get-P95 $writeLat.ToArray()
    $errorRate=[Math]::Round(100.0*$errors.Count/[Math]::Max(1,$ReadRequests+$targets.Count),3)
    $passed=$readP95 -lt 750 -and $writeP95 -lt 1000 -and $errorRate -lt 1

    [ordered]@{
        schema='dip-governance-load-v5-docker';runtime='linux-docker';sourceCommit=$script:SourceSha;measuredAtUtc=[DateTimeOffset]::UtcNow.ToString('o');
        projects=$ProjectCount;findingStates=$ProjectCount*$FindingsPerProject;readRequests=$ReadRequests;writeRequests=$targets.Count;
        concurrentReaders=$ConcurrentReaders;concurrentWriters=$ConcurrentWriters;readP95Ms=$readP95;writeP95Ms=$writeP95;errorRatePercent=$errorRate;
        targets=[ordered]@{readP95Ms=750;writeP95Ms=1000;errorRatePercent=1};passed=$passed;errors=@($errors|Select-Object -First 20)
    } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $script:OutDir 'governance-load-results.json') -Encoding UTF8

    if(-not $passed){ throw "Governance load failed. readP95=$readP95 writeP95=$writeP95 errorRate=$errorRate" }
}

$sourceSha = (Invoke-DipNativeChecked -File 'git' -Arguments @('-C',$RepositoryRoot,'rev-parse','HEAD') -FailureMessage 'Could not resolve exact repository SHA.' | Out-String).Trim()
$script:SourceSha = $sourceSha

if ($sourceSha -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'Could not resolve exact repository SHA.'
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedSha) -and $ExpectedSha -ne $sourceSha) {
    throw "Exact SHA mismatch. Expected=$ExpectedSha Actual=$sourceSha"
}

$dirty = (Invoke-DipNativeChecked -File 'git' -Arguments @('-C',$RepositoryRoot,'status','--porcelain') -FailureMessage 'Could not inspect repository working tree.' | Out-String).Trim()
if ($dirty) {
    throw "Primary verification requires a clean working tree.`n$dirty"
}

$script:ComposeFile = $composeFile
$script:ComposeProject = 'dipverify' + $sourceSha.Substring(0,8) + (Get-Date -Format 'HHmmss')
$script:PostgresPassword = 'dip-verify-' + [guid]::NewGuid().ToString('N')

$env:DIP_VERIFY_POSTGRES_PASSWORD = $script:PostgresPassword
$env:DIP_VERIFY_POSTGRES_PORT = [string](Get-FreeTcpPort)
$env:DIP_VERIFY_API_PORT = [string](Get-FreeTcpPort)
$env:DIP_VERIFY_WEB_PORT = [string](Get-FreeTcpPort)
$env:DIP_VERIFY_ANALYSIS_PORT = [string](Get-FreeTcpPort)
$env:DIP_VERIFY_WORKER_PORT = [string](Get-FreeTcpPort)

$out = Join-Path $RepositoryRoot 'artifacts\docker-primary'
$script:OutDir = $out
$testOut = Join-Path $out 'TestResults'
New-Item -ItemType Directory -Force -Path $out,$testOut | Out-Null

$script:TranscriptStarted = $false
try {
    Start-Transcript -Path (Join-Path $out 'verification-transcript.txt') -Force | Out-Null
    $script:TranscriptStarted = $true
}
catch {
    Write-Warning ("Could not start verification transcript: " + $_.Exception.Message)
}

$summary = [ordered]@{
    schema = 'dip.docker-primary-verification.v1'
    sourceCommit = $sourceSha
    startedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    runtime = 'linux-docker'
    windowsApplicationControlAffectsPrimaryResult = $false
    stages = [Collections.Generic.List[object]]::new()
}

function Invoke-Stage {
    param([string]$Name,[scriptblock]$Action)

    $sw = [Diagnostics.Stopwatch]::StartNew()
    try {
        Write-Host "`n=== $Name ===" -ForegroundColor Cyan
        & $Action
        $sw.Stop()
        $summary.stages.Add([pscustomobject]@{name=$Name;status='PASS';seconds=[Math]::Round($sw.Elapsed.TotalSeconds,3);message=''}) | Out-Null
        Write-Host "PASS: $Name" -ForegroundColor Green
    }
    catch {
        $sw.Stop()
        $summary.stages.Add([pscustomobject]@{name=$Name;status='FAIL';seconds=[Math]::Round($sw.Elapsed.TotalSeconds,3);message=$_.Exception.Message}) | Out-Null
        throw
    }
}

try {
    Set-Location -LiteralPath $RepositoryRoot

    Invoke-Stage 'Build verifier image' {
        Invoke-ComposeChecked @('build','verifier') | Out-Null
    }

    Invoke-Stage 'Verifier bind mount exact SHA' {
        $probeDir = Join-Path $out 'infrastructure-probe'
        New-Item -ItemType Directory -Force -Path $probeDir | Out-Null

        # Keep verifier commands simple across Windows PowerShell -> Docker Compose -> bash -lc.
        Invoke-Verifier 'test -d /work/.git' | Out-Null
        Invoke-Verifier 'test -f /work/DevelopmentIntelligencePlatform.sln' | Out-Null

        $shaCandidates = @(
            Invoke-Verifier 'git -c safe.directory=/work rev-parse HEAD' |
            ForEach-Object { $_.ToString().Trim() } |
            Where-Object { $_ -match '^[0-9a-fA-F]{40}$' }
        )

        if ($shaCandidates.Count -ne 1) {
            throw "Verifier container returned an unexpected exact-SHA result. Candidates=$($shaCandidates -join ',')"
        }

        $containerSha = $shaCandidates[0]
        if ($containerSha -ne $sourceSha) {
            throw "Verifier container SHA mismatch. Expected=$sourceSha Actual=$containerSha"
        }

        Invoke-Verifier 'echo container-write-ok > /work/artifacts/docker-primary/infrastructure-probe/container-write.txt' | Out-Null

        $hostProbe = Join-Path $probeDir 'container-write.txt'
        if (-not (Test-Path -LiteralPath $hostProbe -PathType Leaf)) {
            throw 'Verifier bind-mount probe did not create the host-visible evidence file.'
        }

        $probeText = (Get-Content -LiteralPath $hostProbe -Raw).Trim()
        if ($probeText -ne 'container-write-ok') {
            throw "Verifier bind-mount probe content mismatch: $probeText"
        }

        [ordered]@{
            schema = 'dip.verifier-bind-mount-exact-sha.v2'
            sourceCommit = $sourceSha
            containerCommit = $containerSha
            gitDirectoryVisible = $true
            solutionVisible = $true
            containerToHostWrite = 'PASS'
            verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        } |
            ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath (Join-Path $probeDir 'verifier-bind-mount.json') -Encoding UTF8
    }

    Invoke-Stage 'Build Python Analysis image' {
        Invoke-PythonAnalysisBuildWithDiagnostics
    }

    Invoke-Stage 'Build API Worker Web images' {
        Invoke-ComposeChecked @('build','api','worker','web') | Out-Null
    }

    Invoke-Stage 'Start PostgreSQL' {
        Invoke-ComposeChecked @('up','-d','postgres') | Out-Null
        [void](Wait-ComposePostgresReady -TimeoutSeconds 120)
    }

    Invoke-Stage 'Restore Build Format' {
        Invoke-Verifier "dotnet restore DevelopmentIntelligencePlatform.sln"
        Invoke-Verifier "dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore --nologo"
        Invoke-Verifier "dotnet format DevelopmentIntelligencePlatform.sln --verify-no-changes --no-restore"
    }

    Invoke-Stage 'Domain tests strict' {
        Invoke-StrictDockerTest 'tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj' 'domain'
    }

    Invoke-Stage 'Application tests strict' {
        Invoke-StrictDockerTest 'tests/Dip.Application.Tests/Dip.Application.Tests.csproj' 'application'
    }

    Invoke-Stage 'Infrastructure tests strict' {
        Invoke-StrictDockerTest 'tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj' 'infrastructure'
    }

    Invoke-Stage 'Generative AI QA strict' {
        $filter = 'FullyQualifiedName~P2Intelligence.GenerativeQaRedactorTests|FullyQualifiedName~P2Intelligence.GenerativeQaResponseParserTests|FullyQualifiedName~P2Intelligence.OpenAiCompatibleFixProposalAgentTests|FullyQualifiedName~P2Intelligence.DeterministicLocalDebugAgentTests'
        Invoke-StrictDockerTest 'tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj' 'generative-ai-qa' $filter
    }

    Invoke-Stage 'Integration tests strict' {
        Invoke-StrictDockerTest 'tests/Dip.IntegrationTests/Dip.IntegrationTests.csproj' 'integration'
    }

    Invoke-Stage 'Universal QA Bug Zoo' {
        Invoke-Verifier "dotnet run --project tools/Dip.QaBugZooVerifier/Dip.QaBugZooVerifier.csproj -c Release -- --root /work --output /work/artifacts/docker-primary/qa-bug-zoo --require-languages python,csharp,cpp,javascript,customlang"
    }

    Invoke-Stage 'Migration exact apply rollback reapply' {
        Invoke-EfVerifier "database update --project src/Dip.Infrastructure --startup-project src/Dip.Api"

        $expected = @(
            Invoke-EfVerifier "migrations list --project src/Dip.Infrastructure --startup-project src/Dip.Api --no-connect"
        ) | Where-Object { $_ -match '^\d{8,}_.+' } | ForEach-Object { $_.ToString().Trim() }

        if($expected.Count -lt 1){ throw 'No EF migrations were discovered inside verifier container.' }

        $initialIds = @(
            Invoke-ComposePsql -Sql 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' -TuplesOnly |
            ForEach-Object { $_.ToString().Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        )
        if($initialIds.Count -ne $expected.Count){ throw "Initial migration count mismatch. Expected=$($expected.Count) Actual=$($initialIds.Count)" }
        for($i=0;$i -lt $expected.Count;$i++){ if($initialIds[$i] -ne $expected[$i]){ throw "Initial migration id mismatch at $i. Expected=$($expected[$i]) Actual=$($initialIds[$i])" } }

        Invoke-EfVerifier "database update 0 --project src/Dip.Infrastructure --startup-project src/Dip.Api"

        $exists = (Invoke-ComposePsql -Sql 'SELECT CASE WHEN to_regclass(''public."__EFMigrationsHistory"'') IS NULL THEN 0 ELSE 1 END;' -TuplesOnly | Out-String).Trim()
        if($exists -ne '0'){
            $count = (Invoke-ComposePsql -Sql 'SELECT count(*) FROM "__EFMigrationsHistory";' -TuplesOnly | Out-String).Trim()
            if($count -ne '0'){ throw "Rollback to zero left migration history rows: $count" }
        }

        Invoke-EfVerifier "database update --project src/Dip.Infrastructure --startup-project src/Dip.Api"
        Invoke-EfVerifier "database update --project src/Dip.Infrastructure --startup-project src/Dip.Api"

        $reappliedIds = @(
            Invoke-ComposePsql -Sql 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' -TuplesOnly |
            ForEach-Object { $_.ToString().Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        )
        if($reappliedIds.Count -ne $expected.Count){ throw "Reapplied migration count mismatch. Expected=$($expected.Count) Actual=$($reappliedIds.Count)" }
        for($i=0;$i -lt $expected.Count;$i++){ if($reappliedIds[$i] -ne $expected[$i]){ throw "Reapplied migration id mismatch at $i." } }
    }

    Invoke-Stage 'Start API Worker Web Analysis' {
        Invoke-ComposeChecked @('up','-d','python-analysis','api','worker','web')
        Wait-Http 'Analysis' "http://127.0.0.1:$($env:DIP_VERIFY_ANALYSIS_PORT)/health"
        Wait-Http 'API' "http://127.0.0.1:$($env:DIP_VERIFY_API_PORT)/health"
        Wait-Http 'Web' "http://127.0.0.1:$($env:DIP_VERIFY_WEB_PORT)/"
        Wait-Http 'Worker metrics' "http://127.0.0.1:$($env:DIP_VERIFY_WORKER_PORT)/metrics"
    }

    Invoke-Stage 'E2E tests strict' {
        Invoke-StrictDockerTest 'tests/Dip.EndToEndTests/Dip.EndToEndTests.csproj' 'e2e'
    }

    if(-not $SkipPlaywright){
        Invoke-Stage 'Playwright GUI' {
            Invoke-ComposeChecked @(
                'run','--rm','--no-deps','playwright',
                'if [ -f package-lock.json ]; then npm ci --no-audit --no-fund; else npm install --package-lock=false --no-audit --no-fund; fi && npx playwright test tests/gui-onboarding.spec.mjs --retries=0'
            )
        }
    }

    Invoke-Stage 'Disaster Recovery' {
        $postgresId = (
            Invoke-DipNativeChecked `
                -File 'docker' `
                -Arguments @('compose','-p',$script:ComposeProject,'-f',$script:ComposeFile,'ps','-q','postgres') `
                -FailureMessage 'Could not query verification PostgreSQL container.' |
            Out-String
        ).Trim()
        if([string]::IsNullOrWhiteSpace($postgresId)){ throw 'Could not resolve verification PostgreSQL container.' }

        $name = (
            Invoke-DipNativeChecked `
                -File 'docker' `
                -Arguments @('inspect','--format','{{.Name}}',$postgresId) `
                -FailureMessage 'Could not inspect verification PostgreSQL container.' |
            Out-String
        ).Trim().TrimStart([char[]]@(47))

        $oldContainer = $env:DIP_POSTGRES_CONTAINER
        try {
            $env:DIP_POSTGRES_CONTAINER = $name
            Invoke-DipNativeChecked `
                -File 'powershell.exe' `
                -Arguments @(
                    '-NoProfile',
                    '-ExecutionPolicy','Bypass',
                    '-File',(Join-Path $PSScriptRoot 'Verify-DisasterRecovery.ps1'),
                    '-DatabaseHost','127.0.0.1',
                    '-DatabasePort',[string]([int]$env:DIP_VERIFY_POSTGRES_PORT),
                    '-DatabaseUser','dip',
                    '-DatabasePassword',$script:PostgresPassword
                ) `
                -DiscardOutput `
                -FailureMessage 'Disaster Recovery verification failed.'
        }
        finally {
            $env:DIP_POSTGRES_CONTAINER = $oldContainer
        }
    }

    Invoke-Stage 'Reset runtime database for performance evidence' {
        Invoke-ComposeChecked @('down','-v','--remove-orphans') | Out-Null
        Invoke-ComposeChecked @('up','-d','postgres') | Out-Null
        [void](Wait-ComposePostgresReady -TimeoutSeconds 120)
        Invoke-EfVerifier "database update --project src/Dip.Infrastructure --startup-project src/Dip.Api"
        Invoke-ComposeChecked @('up','-d','python-analysis','api','worker','web')
        Wait-Http 'Benchmark API' "http://127.0.0.1:$($env:DIP_VERIFY_API_PORT)/health"
    }

    Invoke-Stage 'Portfolio benchmark and soak' {
        Invoke-PortfolioBenchmarkExternal -ApiBase "http://127.0.0.1:$($env:DIP_VERIFY_API_PORT)"
    }

    Invoke-Stage 'Governance load' {
        Invoke-GovernanceLoadExternal -ApiBase "http://127.0.0.1:$($env:DIP_VERIFY_API_PORT)"
    }

    Invoke-Stage 'Publication safety' {
        Invoke-Verifier "python3 tools/verify_publication_safety.py --root . --output artifacts/docker-primary/publication-safety.json"
        Invoke-Verifier "python3 tools/validate_portfolio_readiness.py --root . --output artifacts/docker-primary/portfolio-readiness.json"
    }

    Invoke-Stage 'Dependency audit' {
        Invoke-Verifier "dotnet list DevelopmentIntelligencePlatform.sln package --vulnerable --include-transitive"
        Invoke-Verifier "python3 -m pip_audit -r services/python-analysis/requirements.txt"
    }

    $dirtyFinal = (Invoke-DipNativeChecked -File 'git' -Arguments @('-C',$RepositoryRoot,'status','--porcelain') -FailureMessage 'Could not inspect final repository working tree.' | Out-String).Trim()
    if($dirtyFinal){
        throw "Verification mutated the source tree.`n$dirtyFinal"
    }

    $summary.finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    $summary.status = 'PASS'
    $summary |
        ConvertTo-Json -Depth 10 |
        Set-Content (Join-Path $out 'docker-primary-verification.json') -Encoding UTF8

    @(
        'DOCKER PRIMARY VERIFICATION PASS',
        "Commit=$sourceSha",
        'Runtime=linux-docker',
        'WindowsApplicationControlImpactOnPrimaryResult=false',
        'StrictZeroTestPolicy=enabled'
    ) | Set-Content (Join-Path $out 'DOCKER_PRIMARY_VERIFICATION_PASS.txt') -Encoding UTF8

    Write-Host "`nDOCKER PRIMARY VERIFICATION PASS: $sourceSha" -ForegroundColor Green
}
catch {
    $summary.finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    $summary.status = 'FAIL'
    $summary.failure = $_.Exception.Message
    $summary |
        ConvertTo-Json -Depth 10 |
        Set-Content (Join-Path $out 'docker-primary-verification.json') -Encoding UTF8
    throw
}
finally {
    try {
        $logResult = Invoke-DipNativeCapture `
            -File 'docker' `
            -Arguments @('compose','-p',$script:ComposeProject,'-f',$script:ComposeFile,'logs','--no-color')
        @($logResult.Lines) |
            Set-Content (Join-Path $out 'docker-compose.log') -Encoding UTF8
    }
    catch {
    }

    if(-not $KeepContainers){
        try {
            $cleanup = Invoke-DipNativeCapture `
                -File 'docker' `
                -Arguments @('compose','-p',$script:ComposeProject,'-f',$script:ComposeFile,'down','-v','--remove-orphans')
            @(
                "ExitCode=$($cleanup.ExitCode)"
                $cleanup.Lines
            ) |
                Set-Content (Join-Path $out 'docker-cleanup.log') -Encoding UTF8
        }
        catch {
        }
    }

    if ($script:TranscriptStarted) {
        try {
            Stop-Transcript | Out-Null
        }
        catch {
        }
    }

    try {
        $hashPath = Join-Path $out 'SHA256SUMS.txt'
        Get-ChildItem -LiteralPath $out -File -Recurse |
            Where-Object { $_.FullName -ne $hashPath } |
            Sort-Object FullName |
            ForEach-Object {
                $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
                $relative = $_.FullName.Substring($out.Length).TrimStart([char[]]@(92,47))
                "$($hash.Hash.ToLowerInvariant())  $relative"
            } |
            Set-Content -LiteralPath $hashPath -Encoding ASCII
    }
    catch {
        Write-Warning ("Could not create SHA256SUMS.txt: " + $_.Exception.Message)
    }
}
