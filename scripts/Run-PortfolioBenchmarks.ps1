[CmdletBinding()]
param(
    [int]$ProjectCount = 250,
    [int]$ProjectListIterations = 50,
    [int]$CollectorIterations = 30,
    [int]$MetricQueryIterations = 10,
    [int]$MetricRows = 1000000,
    [int]$BurstRequests = 100,
    [int]$BurstTargetRequestsPerSecond = 100,
    [int]$SoakSeconds = 900,
    [int]$SoakTargetRequestsPerSecond = 25,
    [double]$MaxSoakErrorRatePercent = 0.1,
    [double]$MaxSoakMemoryGrowthMb = 256,
    [switch]$UpdateDocumentation
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($ProjectCount -lt 1) { throw 'ProjectCount must be at least 1.' }
if ($MetricRows -lt 1000) { throw 'MetricRows must be at least 1000.' }
if ($BurstRequests -lt 1) { throw 'BurstRequests must be at least 1.' }
if ($BurstTargetRequestsPerSecond -lt 1) { throw 'BurstTargetRequestsPerSecond must be at least 1.' }
if ($SoakSeconds -lt 1) { throw 'SoakSeconds must be at least 1.' }
if ($SoakTargetRequestsPerSecond -lt 1) { throw 'SoakTargetRequestsPerSecond must be at least 1.' }

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root 'artifacts\portfolio-benchmark'
New-Item -ItemType Directory -Force -Path $out | Out-Null

foreach ($command in @('git', 'dotnet', 'docker')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command is required."
    }
}

Add-Type -AssemblyName System.Net.Http

function Get-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()
    return $port
}

function Get-Percentile([double[]]$Values, [double]$Percentile) {
    if ($Values.Count -eq 0) { return 0 }
    $sorted = @($Values | Sort-Object)
    $index = [Math]::Max(0, [Math]::Ceiling($sorted.Count * $Percentile) - 1)
    return [Math]::Round($sorted[$index], 3)
}

function Wait-Http([string]$Url) {
    $deadline = (Get-Date).AddSeconds(90)
    do {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 3
            if ($response.StatusCode -eq 200) { return }
        }
        catch {}
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    throw "Timed out waiting for $Url"
}

function Invoke-PacedSoak(
    [string]$Url,
    [int]$DurationSeconds,
    [int]$TargetRequestsPerSecond) {

    $requestCount = $DurationSeconds * $TargetRequestsPerSecond
    $latencies = @()
    $errors = 0
    $wall = [Diagnostics.Stopwatch]::StartNew()

    for ($i = 0; $i -lt $requestCount; $i++) {
        $targetMs = $i * (1000.0 / $TargetRequestsPerSecond)
        while ($wall.Elapsed.TotalMilliseconds -lt $targetMs) {
            $remaining = $targetMs - $wall.Elapsed.TotalMilliseconds
            if ($remaining -gt 2) { Start-Sleep -Milliseconds ([Math]::Min(10, [int]$remaining)) }
        }

        $requestTimer = [Diagnostics.Stopwatch]::StartNew()
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 10
            if ($response.StatusCode -ne 200) { $errors++ }
        }
        catch {
            $errors++
        }
        finally {
            $requestTimer.Stop()
            $latencies += $requestTimer.Elapsed.TotalMilliseconds
        }
    }

    $wall.Stop()
    return [ordered]@{
        requests = $requestCount
        targetRequestsPerSecond = $TargetRequestsPerSecond
        elapsedSeconds = [Math]::Round($wall.Elapsed.TotalSeconds, 3)
        actualRequestsPerSecond = if ($wall.Elapsed.TotalSeconds -gt 0) { [Math]::Round($requestCount / $wall.Elapsed.TotalSeconds, 3) } else { 0 }
        errors = $errors
        errorRatePercent = [Math]::Round(($errors * 100.0) / $requestCount, 4)
        p50Ms = Get-Percentile $latencies 0.50
        p95Ms = Get-Percentile $latencies 0.95
        p99Ms = Get-Percentile $latencies 0.99
    }
}

$sha = (& git -C $root rev-parse HEAD | Out-String).Trim()
$dirty = [bool]((& git -C $root status --porcelain | Out-String).Trim())
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$compose = "dip-bench-$stamp"
$pgPort = Get-FreePort
$redisPort = Get-FreePort
$apiPort = Get-FreePort
$apiBase = "http://127.0.0.1:$apiPort"
$apiProcess = $null

$names = @(
    'POSTGRES_PORT',
    'REDIS_PORT',
    'POSTGRES_DB',
    'POSTGRES_USER',
    'POSTGRES_PASSWORD',
    'ASPNETCORE_ENVIRONMENT',
    'ASPNETCORE_URLS',
    'ConnectionStrings__PostgreSql',
    'Redis__ConnectionString',
    'Qa__WorkspaceRoot',
    'Qa__ArtifactRoot',
    'Authentication__RequireAuthenticatedUser'
)
$old = @{}
foreach ($name in $names) {
    $old[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

try {
    $env:POSTGRES_PORT = "$pgPort"
    $env:REDIS_PORT = "$redisPort"
    $env:POSTGRES_DB = 'dipbench'
    $env:POSTGRES_USER = 'dip'
    $env:POSTGRES_PASSWORD = 'dip-benchmark-password'

    Push-Location $root
    try {
        & docker compose -p $compose up -d postgres redis
        if ($LASTEXITCODE -ne 0) { throw 'Failed to start PostgreSQL/Redis.' }

        $ready = $false
        for ($attempt = 1; $attempt -le 60; $attempt++) {
            & docker compose -p $compose exec -T postgres pg_isready -U dip -d dipbench *> $null
            if ($LASTEXITCODE -eq 0) {
                $ready = $true
                break
            }
            Start-Sleep -Milliseconds 500
        }
        if (-not $ready) { throw 'PostgreSQL did not become ready.' }

        & dotnet build DevelopmentIntelligencePlatform.sln -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    }
    finally {
        Pop-Location
    }

    # Dedicated non-Development performance profile. The manifest records that local loopback auth is disabled.
    $env:ASPNETCORE_ENVIRONMENT = 'Benchmark'
    $env:ASPNETCORE_URLS = $apiBase
    $env:Authentication__RequireAuthenticatedUser = 'false'
    $env:ConnectionStrings__PostgreSql = "Host=127.0.0.1;Port=$pgPort;Database=dipbench;Username=dip;Password=dip-benchmark-password"
    $env:Redis__ConnectionString = "127.0.0.1:$redisPort"
    $env:Qa__WorkspaceRoot = $root
    $env:Qa__ArtifactRoot = (Join-Path $root 'artifacts\qa')

    $dll = Join-Path $root 'src\Dip.Api\bin\Release\net8.0\Dip.Api.dll'
    $apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($dll) -WorkingDirectory $root -PassThru -WindowStyle Hidden
    Wait-Http "$apiBase/health"

    $project = $null
    for ($i = 0; $i -lt $ProjectCount; $i++) {
        $body = @{
            name = "Portfolio Benchmark $i"
            repositoryUrl = "https://github.com/example/dip-portfolio-benchmark-$i"
            defaultBranch = 'main'
            releaseDateUtc = $null
        } | ConvertTo-Json
        $created = Invoke-RestMethod -Method Post -Uri "$apiBase/api/projects" -ContentType 'application/json' -Body $body
        if ($i -eq 0) { $project = $created }
    }

    1..10 | ForEach-Object {
        Invoke-WebRequest -UseBasicParsing -Uri "$apiBase/api/projects" | Out-Null
    }

    $projectMs = @()
    1..$ProjectListIterations | ForEach-Object {
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-WebRequest -UseBasicParsing -Uri "$apiBase/api/projects" -TimeoutSec 20
        $timer.Stop()
        if ($response.StatusCode -ne 200) { throw 'Project list request failed.' }
        $projectMs += $timer.Elapsed.TotalMilliseconds
    }

    $collectorMs = @()
    $lastPipeline = $null
    1..$CollectorIterations | ForEach-Object {
        $now = [DateTimeOffset]::UtcNow
        $payload = @{
            schemaVersion = '1.0'
            reportId = "bench-report-$stamp-$($_)-$([Guid]::NewGuid().ToString('N'))"
            externalRunId = "bench-run-$stamp-$($_)-$([Guid]::NewGuid().ToString('N'))"
            attemptNumber = 1
            collectorType = 'portfolio-benchmark'
            collectorVersion = '1.0.0'
            projectId = $project.id
            repository = $project.repositoryUrl
            commitSha = '0123456789abcdef0123456789abcdef01234567'
            branch = 'main'
            startedAt = $now.AddSeconds(-1)
            finishedAt = $now
            status = 'Succeeded'
            metrics = @(@{
                name = 'frame_time'
                value = 16.0
                unit = 'ms'
                baselineValue = 16.6
                environmentId = 'portfolio'
                scenarioId = 'benchmark'
                platform = 'windows'
                buildConfiguration = 'Release'
                benchmarkVersion = '1'
            })
            issues = @()
        } | ConvertTo-Json -Depth 8

        $timer = [Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-RestMethod -Method Post -Uri "$apiBase/api/v1/collector-reports" -ContentType 'application/json' -Body $payload
        $timer.Stop()
        $collectorMs += $timer.Elapsed.TotalMilliseconds
        $lastPipeline = $response.pipelineRunId
    }
    if (-not $lastPipeline) { throw 'Collector benchmark did not return a PipelineRunId.' }

    # Burst load: schedule requests at a controlled target rate and use headers-only
    # completion to avoid the benchmark client becoming a memory bottleneck.
    $client = [Net.Http.HttpClient]::new()
    $tasks = New-Object System.Collections.ArrayList
    $burst = [Diagnostics.Stopwatch]::StartNew()
    for ($i = 0; $i -lt $BurstRequests; $i++) {
        $targetMs = $i * (1000.0 / $BurstTargetRequestsPerSecond)
        while ($burst.Elapsed.TotalMilliseconds -lt $targetMs) {
            Start-Sleep -Milliseconds 1
        }
        [void]$tasks.Add($client.GetAsync(
            "$apiBase/api/projects",
            [Net.Http.HttpCompletionOption]::ResponseHeadersRead))
    }
    [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks.ToArray([Threading.Tasks.Task]))
    $burst.Stop()

    $burstErrors = 0
    foreach ($task in $tasks) {
        if (-not $task.Result.IsSuccessStatusCode) { $burstErrors++ }
        $task.Result.Dispose()
    }
    $client.Dispose()
    $burstErrorRate = [Math]::Round(($burstErrors * 100.0) / $BurstRequests, 4)
    $burstActualRps = if ($burst.Elapsed.TotalSeconds -gt 0) {
        [Math]::Round($BurstRequests / $burst.Elapsed.TotalSeconds, 3)
    }
    else { 0 }

    $seed = @"
INSERT INTO "Metrics" ("Id","PipelineRunId","BaselinePipelineRunId","Name","Value","Unit","BaselineValue","Difference","DifferencePercent","Severity","ComparisonStatus","EnvironmentId","ScenarioId","Platform","BuildConfiguration","BenchmarkVersion")
SELECT gen_random_uuid(),'$lastPipeline'::uuid,NULL,'portfolio.metric.'||(g%100),(g%1000)::numeric,'ms',NULL,NULL,NULL,0,0,'portfolio','scale-query','windows','Release','1' FROM generate_series(1,$MetricRows) g;
"@
    Push-Location $root
    try {
        $seed | & docker compose -p $compose exec -T postgres psql -U dip -d dipbench -v ON_ERROR_STOP=1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "$MetricRows metric seed failed." }
    }
    finally {
        Pop-Location
    }

    $metricMs = @()
    $query = "SELECT \"Name\",AVG(\"Value\") FROM \"Metrics\" WHERE \"PipelineRunId\"='$lastPipeline'::uuid GROUP BY \"Name\" ORDER BY \"Name\";"
    1..$MetricQueryIterations | ForEach-Object {
        $timer = [Diagnostics.Stopwatch]::StartNew()
        Push-Location $root
        try {
            $query | & docker compose -p $compose exec -T postgres psql -U dip -d dipbench -q -t -v ON_ERROR_STOP=1 | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Metric scale query failed.' }
        }
        finally {
            Pop-Location
        }
        $timer.Stop()
        $metricMs += $timer.Elapsed.TotalMilliseconds
    }

    $apiProcess.Refresh()
    $memoryBeforeMb = [Math]::Round($apiProcess.WorkingSet64 / 1MB, 3)
    $soak = Invoke-PacedSoak `
        -Url "$apiBase/api/projects" `
        -DurationSeconds $SoakSeconds `
        -TargetRequestsPerSecond $SoakTargetRequestsPerSecond
    $apiProcess.Refresh()
    if ($apiProcess.HasExited) { throw 'API process exited during soak test.' }
    $memoryAfterMb = [Math]::Round($apiProcess.WorkingSet64 / 1MB, 3)
    $memoryGrowthMb = [Math]::Round($memoryAfterMb - $memoryBeforeMb, 3)

    $projectP50 = Get-Percentile $projectMs 0.50
    $projectP95 = Get-Percentile $projectMs 0.95
    $projectP99 = Get-Percentile $projectMs 0.99
    $collectorP95 = Get-Percentile $collectorMs 0.95
    $metricP95 = Get-Percentile $metricMs 0.95

    $result = [ordered]@{
        schema = 'dip-portfolio-benchmark-v2'
        sourceCommit = $sha
        sourceWorkingTreeDirty = $dirty
        measuredAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        environment = [ordered]@{
            os = [Environment]::OSVersion.VersionString
            machine = $env:COMPUTERNAME
            processorCount = [Environment]::ProcessorCount
            dotnet = (& dotnet --version | Out-String).Trim()
            aspnetEnvironment = $env:ASPNETCORE_ENVIRONMENT
            runtimeProfile = 'ProductionLikeBenchmark'
            loopbackAuthenticationDisabled = $true
        }
        scenarios = [ordered]@{
            projectList = [ordered]@{
                population = $ProjectCount
                iterations = $ProjectListIterations
                p50Ms = $projectP50
                p95Ms = $projectP95
                p99Ms = $projectP99
                targetP95Ms = 500
                passed = $projectP95 -lt 500
            }
            collector = [ordered]@{
                iterations = $CollectorIterations
                p95Ms = $collectorP95
                targetP95Ms = 1000
                passed = $collectorP95 -lt 1000
            }
            burstLoad = [ordered]@{
                requests = $BurstRequests
                targetRequestsPerSecond = $BurstTargetRequestsPerSecond
                actualRequestsPerSecond = $burstActualRps
                elapsedMs = [Math]::Round($burst.Elapsed.TotalMilliseconds, 3)
                errors = $burstErrors
                errorRatePercent = $burstErrorRate
                targetErrorRatePercent = 1
                passed = $burstErrorRate -lt 1
            }
            metricScaleQuery = [ordered]@{
                rows = $MetricRows
                iterations = $MetricQueryIterations
                p95Ms = $metricP95
                targetP95Ms = 2000
                passed = $metricP95 -lt 2000
            }
            soak = [ordered]@{
                durationSeconds = $SoakSeconds
                requests = $soak.requests
                targetRequestsPerSecond = $soak.targetRequestsPerSecond
                actualRequestsPerSecond = $soak.actualRequestsPerSecond
                errors = $soak.errors
                errorRatePercent = $soak.errorRatePercent
                p50Ms = $soak.p50Ms
                p95Ms = $soak.p95Ms
                p99Ms = $soak.p99Ms
                memoryBeforeMb = $memoryBeforeMb
                memoryAfterMb = $memoryAfterMb
                memoryGrowthMb = $memoryGrowthMb
                maxErrorRatePercent = $MaxSoakErrorRatePercent
                maxMemoryGrowthMb = $MaxSoakMemoryGrowthMb
                passed = ($soak.errorRatePercent -le $MaxSoakErrorRatePercent) -and ($memoryGrowthMb -le $MaxSoakMemoryGrowthMb)
            }
        }
    }

    $jsonPath = Join-Path $out 'portfolio-benchmark-results.json'
    $result | ConvertTo-Json -Depth 12 | Set-Content $jsonPath -Encoding UTF8

    $markdown = @(
        '# Benchmark Results',
        '',
        "Measured evidence for commit ``$sha`` at $($result.measuredAtUtc).",
        '',
        '| Scenario | Target | Measured | Result |',
        '|---|---:|---:|---|',
        "| Project list p95 ($ProjectCount projects) | <500 ms | $projectP95 ms | $(if ($result.scenarios.projectList.passed) {'PASS'} else {'FAIL'}) |",
        "| Collector p95 | <1000 ms | $collectorP95 ms | $(if ($result.scenarios.collector.passed) {'PASS'} else {'FAIL'}) |",
        "| Burst error rate ($BurstTargetRequestsPerSecond req/s target) | <1% | $burstErrorRate% | $(if ($result.scenarios.burstLoad.passed) {'PASS'} else {'FAIL'}) |",
        "| $MetricRows Metric query p95 | <2000 ms | $metricP95 ms | $(if ($result.scenarios.metricScaleQuery.passed) {'PASS'} else {'FAIL'}) |",
        "| Soak ($SoakSeconds s @ $SoakTargetRequestsPerSecond req/s) | <=$MaxSoakErrorRatePercent% errors / <=$MaxSoakMemoryGrowthMb MB growth | $($soak.errorRatePercent)% / $memoryGrowthMb MB | $(if ($result.scenarios.soak.passed) {'PASS'} else {'FAIL'}) |",
        '',
        'Raw evidence: ``artifacts/portfolio-benchmark/portfolio-benchmark-results.json``.'
    ) -join "`n"

    $markdown | Set-Content (Join-Path $out 'portfolio-benchmark-results.md') -Encoding UTF8
    if ($UpdateDocumentation) {
        $markdown | Set-Content (Join-Path $root 'docs\09-portfolio\benchmark-results.md') -Encoding UTF8
    }

    Write-Host "Portfolio benchmark evidence: $jsonPath" -ForegroundColor Green

    $allPassed =
        $result.scenarios.projectList.passed `
        -and $result.scenarios.collector.passed `
        -and $result.scenarios.burstLoad.passed `
        -and $result.scenarios.metricScaleQuery.passed `
        -and $result.scenarios.soak.passed
    if (-not $allPassed) { exit 3 }
}
finally {
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }

    Push-Location $root
    try {
        & docker compose -p $compose down -v --remove-orphans *> $null
    }
    catch {}
    finally {
        Pop-Location
    }

    foreach ($name in $names) {
        [Environment]::SetEnvironmentVariable($name, $old[$name], 'Process')
    }
}
