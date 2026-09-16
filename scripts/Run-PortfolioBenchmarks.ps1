# Purpose: Automates the R un-P or tf ol io Be nc hm ar ks repository workflow.
[CmdletBinding()]
param(
    [string]$ExpectedSha = '',
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
    [switch]$UpdateDocumentation,
    [string]$DatabaseHost = $(if ($env:POSTGRES_HOST) { $env:POSTGRES_HOST } else { '127.0.0.1' }),
    [int]$DatabasePort = $(if ($env:POSTGRES_PORT) { [int]$env:POSTGRES_PORT } else { 5432 }),
    [string]$DatabaseUser = $(if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'dip' }),
    [string]$DatabasePassword = $(if ($env:POSTGRES_PASSWORD) { $env:POSTGRES_PASSWORD } else { '' })
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root 'artifacts\portfolio-benchmark'
New-Item -ItemType Directory -Force -Path $out | Out-Null

foreach ($name in @('git','dotnet','psql','pg_isready')) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "$name is required."
    }
}

foreach ($value in @(
    $ProjectCount,$ProjectListIterations,$CollectorIterations,$MetricQueryIterations,
    $MetricRows,$BurstRequests,$BurstTargetRequestsPerSecond,$SoakSeconds,$SoakTargetRequestsPerSecond
)) {
    if ($value -lt 1) { throw 'Benchmark numeric parameters must be positive.' }
}

$sourceSha = (& git -C $root rev-parse HEAD | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceSha -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'Could not resolve the exact source SHA.'
}

$dirtyText = (& git -C $root status --porcelain | Out-String).Trim()
if ($dirtyText) {
    throw "Exact-revision benchmark requires a clean working tree.`n$dirtyText"
}

if ($ExpectedSha -and $sourceSha -ne $ExpectedSha) {
    throw "Exact-revision mismatch. Expected=$ExpectedSha Actual=$sourceSha"
}

function Get-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Get-Percentile([double[]]$Values, [double]$P) {
    if ($Values.Count -eq 0) { return 0 }
    $sorted = @($Values | Sort-Object)
    $index = [Math]::Max(0, [Math]::Ceiling($sorted.Count * $P) - 1)
    return [Math]::Round($sorted[$index], 3)
}

function Wait-Http([string]$Url) {
    $deadline = (Get-Date).AddSeconds(120)
    do {
        try {
            $response = Invoke-WebRequest -UseBasicParsing $Url -TimeoutSec 3
            if ($response.StatusCode -eq 200) { return }
        }
        catch { }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    throw "Timed out waiting for $Url"
}

function Invoke-Psql([string]$Database, [string]$Sql, [switch]$DiscardOutput) {
    $args = @(
        '-h', $DatabaseHost,
        '-p', [string]$DatabasePort,
        '-U', $DatabaseUser,
        '-d', $Database,
        '-v', 'ON_ERROR_STOP=1',
        '-w',
        '-c', $Sql
    )
    if ($DiscardOutput) { & psql @args *> $null }
    else { & psql @args }
    if ($LASTEXITCODE -ne 0) { throw "psql failed for database '$Database'." }
}

function Invoke-PacedSoak([string]$Url, [int]$Seconds, [int]$Rps) {
    $count = $Seconds * $Rps
    $latencies = @()
    $errors = 0
    $wall = [Diagnostics.Stopwatch]::StartNew()
    for ($i = 0; $i -lt $count; $i++) {
        $targetMs = $i * (1000.0 / $Rps)
        while ($wall.Elapsed.TotalMilliseconds -lt $targetMs) {
            $remaining = $targetMs - $wall.Elapsed.TotalMilliseconds
            if ($remaining -gt 2) { Start-Sleep -Milliseconds ([Math]::Min(10, [int]$remaining)) }
        }
        $sw = [Diagnostics.Stopwatch]::StartNew()
        try {
            $response = Invoke-WebRequest -UseBasicParsing $Url -TimeoutSec 10
            if ($response.StatusCode -ne 200) { $errors++ }
        }
        catch { $errors++ }
        finally {
            $sw.Stop()
            $latencies += $sw.Elapsed.TotalMilliseconds
        }
    }
    $wall.Stop()
    return [ordered]@{
        requests = $count
        targetRequestsPerSecond = $Rps
        elapsedSeconds = [Math]::Round($wall.Elapsed.TotalSeconds, 3)
        actualRequestsPerSecond = [Math]::Round($count / $wall.Elapsed.TotalSeconds, 3)
        errors = $errors
        errorRatePercent = [Math]::Round($errors * 100.0 / $count, 4)
        p50Ms = Get-Percentile $latencies 0.50
        p95Ms = Get-Percentile $latencies 0.95
        p99Ms = Get-Percentile $latencies 0.99
    }
}

$db = 'dip_bench_' + [guid]::NewGuid().ToString('N').Substring(0, 10)
$apiPort = Get-FreePort
$apiBase = "http://127.0.0.1:$apiPort"
$apiProcess = $null
$databaseCreated = $false
$oldPgPassword = $env:PGPASSWORD
$env:PGPASSWORD = $DatabasePassword
$oldEnv = @{}
$envNames = @(
    'ConnectionStrings__PostgreSql','ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS',
    'Authentication__RequireAuthenticatedUser','Qa__WorkspaceRoot','Qa__ArtifactRoot','Artifacts__LocalRoot'
)
foreach ($name in $envNames) {
    $oldEnv[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

try {
    & pg_isready -h $DatabaseHost -p $DatabasePort -U $DatabaseUser -d postgres *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Native PostgreSQL is not ready.' }

    $createSql = 'CREATE DATABASE "{0}" OWNER "{1}";' -f $db, $DatabaseUser
    Invoke-Psql -Database 'postgres' -Sql $createSql -DiscardOutput
    $databaseCreated = $true

    Push-Location $root
    try {
        & dotnet build DevelopmentIntelligencePlatform.sln -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    }
    finally { Pop-Location }

    $env:ASPNETCORE_ENVIRONMENT = 'Benchmark'
    $env:ASPNETCORE_URLS = $apiBase
    $env:Authentication__RequireAuthenticatedUser = 'false'
    $env:ConnectionStrings__PostgreSql = "Host=$DatabaseHost;Port=$DatabasePort;Database=$db;Username=$DatabaseUser;Password=$DatabasePassword"
    $env:Qa__WorkspaceRoot = $root
    $env:Qa__ArtifactRoot = Join-Path $root 'artifacts\qa'
    $env:Artifacts__LocalRoot = Join-Path $root 'artifacts\durable'

    $dll = Join-Path $root 'src\Dip.Api\bin\Release\net8.0\Dip.Api.dll'
    $apiProcess = Start-Process dotnet -ArgumentList @($dll) -WorkingDirectory $root -PassThru
    Wait-Http "$apiBase/health"

    $project = $null
    for ($i = 0; $i -lt $ProjectCount; $i++) {
        $body = @{
            name = "Portfolio Benchmark $i"
            repositoryUrl = "https://github.com/example/dip-portfolio-benchmark-$i"
            defaultBranch = 'main'
            releaseDateUtc = $null
        } | ConvertTo-Json
        $created = Invoke-RestMethod -Method Post "$apiBase/api/projects" -ContentType 'application/json' -Body $body
        if ($i -eq 0) { $project = $created }
    }

    1..10 | ForEach-Object { Invoke-WebRequest -UseBasicParsing "$apiBase/api/projects" | Out-Null }
    $projectMs = @()
    1..$ProjectListIterations | ForEach-Object {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-WebRequest -UseBasicParsing "$apiBase/api/projects"
        $sw.Stop()
        if ($response.StatusCode -ne 200) { throw 'Project list failed.' }
        $projectMs += $sw.Elapsed.TotalMilliseconds
    }

    $collectorMs = @()
    $lastPipeline = $null
    1..$CollectorIterations | ForEach-Object {
        $now = [DateTimeOffset]::UtcNow
        $payload = @{
            schemaVersion = '1.0'
            reportId = "bench-$($_)-$([guid]::NewGuid().ToString('N'))"
            externalRunId = "bench-run-$($_)-$([guid]::NewGuid().ToString('N'))"
            attemptNumber = 1
            collectorType = 'portfolio-benchmark'
            collectorVersion = '1.0.0'
            projectId = $project.id
            repository = $project.repositoryUrl
            commitSha = $sourceSha
            branch = 'main'
            startedAt = $now.AddSeconds(-1)
            finishedAt = $now
            status = 'Succeeded'
            metrics = @(@{
                name='frame_time'; value=16.0; unit='ms'; baselineValue=16.6
                environmentId='portfolio'; scenarioId='benchmark'; platform='windows'
                buildConfiguration='Release'; benchmarkVersion='1'
            })
            issues = @()
        } | ConvertTo-Json -Depth 8
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-RestMethod -Method Post "$apiBase/api/v1/collector-reports" -ContentType 'application/json' -Body $payload
        $sw.Stop()
        $collectorMs += $sw.Elapsed.TotalMilliseconds
        $lastPipeline = $response.pipelineRunId
    }
    if (-not $lastPipeline) { throw 'Collector benchmark returned no pipeline id.' }

    Add-Type -AssemblyName System.Net.Http
    $client = [Net.Http.HttpClient]::new()
    $tasks = New-Object System.Collections.ArrayList
    $burst = [Diagnostics.Stopwatch]::StartNew()
    for ($i = 0; $i -lt $BurstRequests; $i++) {
        $targetMs = $i * (1000.0 / $BurstTargetRequestsPerSecond)
        while ($burst.Elapsed.TotalMilliseconds -lt $targetMs) { Start-Sleep -Milliseconds 1 }
        [void]$tasks.Add($client.GetAsync("$apiBase/api/projects", [Net.Http.HttpCompletionOption]::ResponseHeadersRead))
    }
    [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks.ToArray([Threading.Tasks.Task]))
    $burst.Stop()
    $burstErrors = 0
    foreach ($task in $tasks) {
        if (-not $task.Result.IsSuccessStatusCode) { $burstErrors++ }
        $task.Result.Dispose()
    }
    $client.Dispose()
    $burstErrorRate = [Math]::Round($burstErrors * 100.0 / $BurstRequests, 4)

    $seedSql = 'INSERT INTO "Metrics" ("Id","PipelineRunId","BaselinePipelineRunId","Name","Value","Unit","BaselineValue","Difference","DifferencePercent","Severity","ComparisonStatus","EnvironmentId","ScenarioId","Platform","BuildConfiguration","BenchmarkVersion") SELECT gen_random_uuid(),''{0}''::uuid,NULL,''portfolio.metric.''||(g%100),(g%1000)::numeric,''ms'',NULL,NULL,NULL,0,0,''portfolio'',''scale-query'',''windows'',''Release'',''1'' FROM generate_series(1,{1}) g;' -f $lastPipeline, $MetricRows
    Invoke-Psql -Database $db -Sql $seedSql -DiscardOutput

    $querySql = 'SELECT "Name",AVG("Value") FROM "Metrics" WHERE "PipelineRunId"=''{0}''::uuid GROUP BY "Name" ORDER BY "Name";' -f $lastPipeline
    $metricMs = @()
    1..$MetricQueryIterations | ForEach-Object {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        Invoke-Psql -Database $db -Sql $querySql -DiscardOutput
        $sw.Stop()
        $metricMs += $sw.Elapsed.TotalMilliseconds
    }

    $apiProcess.Refresh()
    $memoryBefore = [Math]::Round($apiProcess.WorkingSet64 / 1MB, 3)
    $soak = Invoke-PacedSoak "$apiBase/api/projects" $SoakSeconds $SoakTargetRequestsPerSecond
    $apiProcess.Refresh()
    if ($apiProcess.HasExited) { throw 'API exited during soak.' }
    $memoryAfter = [Math]::Round($apiProcess.WorkingSet64 / 1MB, 3)
    $memoryGrowth = [Math]::Round($memoryAfter - $memoryBefore, 3)

    $projectP50 = Get-Percentile $projectMs 0.50
    $projectP95 = Get-Percentile $projectMs 0.95
    $projectP99 = Get-Percentile $projectMs 0.99
    $collectorP95 = Get-Percentile $collectorMs 0.95
    $metricP95 = Get-Percentile $metricMs 0.95

    $passed = (
        $projectP95 -lt 500 -and
        $collectorP95 -lt 250 -and
        $burstErrorRate -eq 0 -and
        $metricP95 -lt 1000 -and
        $soak.errorRatePercent -le $MaxSoakErrorRatePercent -and
        $memoryGrowth -le $MaxSoakMemoryGrowthMb
    )

    $result = [ordered]@{
        schema = 'dip-portfolio-benchmark-v4'
        runtime = 'native'
        sourceCommit = $sourceSha
        sourceWorkingTreeDirty = $false
        expectedCommit = $(if ($ExpectedSha) { $ExpectedSha } else { $sourceSha })
        measuredAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        environment = [ordered]@{
            os = [Environment]::OSVersion.VersionString
            processorCount = [Environment]::ProcessorCount
            dotnet = (& dotnet --version | Out-String).Trim()
            database = 'native-postgresql'
            queueBackend = 'postgresql'
        }
        scenarios = [ordered]@{
            projectList = [ordered]@{ population=$ProjectCount; iterations=$ProjectListIterations; p50Ms=$projectP50; p95Ms=$projectP95; p99Ms=$projectP99; targetP95Ms=500; passed=$projectP95-lt500 }
            collector = [ordered]@{ iterations=$CollectorIterations; p95Ms=$collectorP95; targetP95Ms=250; passed=$collectorP95-lt250 }
            burst = [ordered]@{ requests=$BurstRequests; targetRequestsPerSecond=$BurstTargetRequestsPerSecond; elapsedSeconds=[Math]::Round($burst.Elapsed.TotalSeconds,3); errorRatePercent=$burstErrorRate; targetErrorRatePercent=0; passed=$burstErrorRate-eq0 }
            metricScale = [ordered]@{ rows=$MetricRows; iterations=$MetricQueryIterations; p95Ms=$metricP95; targetP95Ms=1000; passed=$metricP95-lt1000 }
            soak = $soak
            memory = [ordered]@{ beforeMb=$memoryBefore; afterMb=$memoryAfter; growthMb=$memoryGrowth; maxGrowthMb=$MaxSoakMemoryGrowthMb; passed=$memoryGrowth-le$MaxSoakMemoryGrowthMb }
        }
        passed = $passed
    }

    $canonicalJson = Join-Path $out 'portfolio-benchmark-results.json'
    $compatJson = Join-Path $out 'benchmark-results.json'
    $jsonText = $result | ConvertTo-Json -Depth 10
    $jsonText | Set-Content -LiteralPath $canonicalJson -Encoding UTF8
    $jsonText | Set-Content -LiteralPath $compatJson -Encoding UTF8
    (Get-FileHash -LiteralPath $canonicalJson -Algorithm SHA256).Hash.ToLowerInvariant() | Set-Content -LiteralPath (Join-Path $out 'portfolio-benchmark-results.sha256') -Encoding ASCII

    Write-Host "Native exact-revision benchmark complete: $canonicalJson" -ForegroundColor Green
    if (-not $passed) { throw 'One or more benchmark/soak gates failed.' }
    if ($UpdateDocumentation) {
        Write-Warning 'Measurement succeeded. Promote values only after confirming this artifact belongs to the frozen publication SHA.'
    }
}
finally {
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }
    $env:PGPASSWORD = $oldPgPassword
    foreach ($name in $oldEnv.Keys) {
        [Environment]::SetEnvironmentVariable($name, $oldEnv[$name], 'Process')
    }
    if ($databaseCreated) {
        try {
            $terminateSql = 'SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname=''{0}'';' -f $db
            Invoke-Psql -Database 'postgres' -Sql $terminateSql -DiscardOutput
        }
        catch { }
        try {
            $dropSql = 'DROP DATABASE IF EXISTS "{0}";' -f $db
            Invoke-Psql -Database 'postgres' -Sql $dropSql -DiscardOutput
        }
        catch { }
    }
}
