# Purpose: Runs the governance load/concurrency verification using local or Docker-hosted PostgreSQL client tooling.
[CmdletBinding()]
param(
    [int]$ProjectCount = 10,
    [int]$FindingsPerProject = 5000,
    [int]$ConcurrentReaders = 64,
    [int]$ConcurrentWriters = 32,
    [int]$ReadRequests = 500,
    [int]$WriteRequests = 200,
    [string]$PostgreSqlHost = $(if ($env:POSTGRES_HOST) { $env:POSTGRES_HOST } else { '127.0.0.1' }),
    [int]$PostgreSqlPort = $(if ($env:POSTGRES_PORT) { [int]$env:POSTGRES_PORT } else { 5432 }),
    [string]$PostgreSqlUser = $(if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'dip' }),
    [string]$PostgreSqlPassword = $(if ($env:POSTGRES_PASSWORD) { $env:POSTGRES_PASSWORD } else { 'change-me-local-only' })
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'PostgreSqlRuntime.ps1')

foreach ($command in @('git', 'dotnet')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command is required."
    }
}

$pg = Resolve-DipPostgresClient `
    -Host $PostgreSqlHost `
    -Port $PostgreSqlPort `
    -User $PostgreSqlUser `
    -Password $PostgreSqlPassword

function Get-FreePort {
    $listener = [Net.Sockets.TcpListener]::new(
        [Net.IPAddress]::Loopback,
        0)
    $listener.Start()

    try {
        return ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Get-P95([double[]]$Values) {
    if (!$Values.Count) {
        return 0
    }

    $sorted = @($Values | Sort-Object)
    return [Math]::Round(
        $sorted[
            [Math]::Max(
                0,
                [Math]::Ceiling($sorted.Count * .95) - 1)
        ],
        3)
}

function Wait-Http($Url) {
    $deadline = (Get-Date).AddSeconds(120)

    do {
        try {
            if (
                (Invoke-WebRequest `
                    -UseBasicParsing `
                    $Url `
                    -TimeoutSec 3).StatusCode -eq 200
            ) {
                return
            }
        }
        catch {
        }

        Start-Sleep -Milliseconds 400
    }
    while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for $Url"
}

function Wait-HttpBatch(
    [System.Collections.ArrayList]$Pending,
    [System.Collections.Generic.List[double]]$Latencies,
    [System.Collections.Generic.List[string]]$Errors)
{
    while ($Pending.Count -gt 0) {
        for ($index = $Pending.Count - 1; $index -ge 0; $index--) {
            $entry = $Pending[$index]

            if (!$entry.Task.IsCompleted) {
                continue
            }

            $entry.Stopwatch.Stop()
            $Latencies.Add(
                $entry.Stopwatch.Elapsed.TotalMilliseconds)

            try {
                $response = $entry.Task.GetAwaiter().GetResult()

                if (-not $response.IsSuccessStatusCode) {
                    $Errors.Add(
                        "$($entry.Kind):$([int]$response.StatusCode)")
                }

                $response.Dispose()
            }
            catch {
                $Errors.Add(
                    "$($entry.Kind)-ex:$($_.Exception.Message)")
            }

            if ($entry.Content) {
                $entry.Content.Dispose()
            }

            $Pending.RemoveAt($index)
        }

        if ($Pending.Count -gt 0) {
            Start-Sleep -Milliseconds 2
        }
    }
}

function Invoke-Psql {
    param(
        [string]$Database,
        [string]$Sql,
        [switch]$TuplesOnly
    )

    Invoke-DipPsql `
        -Client $pg `
        -Database $Database `
        -Sql $Sql `
        -TuplesOnly:$TuplesOnly
}

$out = Join-Path $root 'artifacts\governance-load'
New-Item -ItemType Directory -Force $out | Out-Null
Remove-Item `
    (Join-Path $out 'GOVERNANCE_LOAD_PASS.txt') `
    -Force `
    -ErrorAction SilentlyContinue

$database =
    'dip_govload_' +
    [guid]::NewGuid().ToString('N').Substring(0, 12)

$apiPort = Get-FreePort
$api = $null
$databaseCreated = $false

$vars = @(
    'POSTGRES_HOST',
    'POSTGRES_PORT',
    'POSTGRES_DB',
    'POSTGRES_USER',
    'POSTGRES_PASSWORD',
    'ASPNETCORE_ENVIRONMENT',
    'DOTNET_ENVIRONMENT',
    'ASPNETCORE_URLS',
    'ConnectionStrings__PostgreSql',
    'Authentication__RequireAuthenticatedUser'
)

$old = @{}
foreach ($name in $vars) {
    $old[$name] =
        [Environment]::GetEnvironmentVariable(
            $name,
            'Process')
}

try {
    $probe = (
        Invoke-Psql `
            -Database 'postgres' `
            -Sql 'SELECT 1;' `
            -TuplesOnly |
        Out-String
    ).Trim()

    if ($probe -ne '1') {
        throw "PostgreSQL is not reachable at ${PostgreSqlHost}:${PostgreSqlPort}."
    }

    Invoke-Psql `
        -Database 'postgres' `
        -Sql "CREATE DATABASE `"$database`" OWNER `"$PostgreSqlUser`";" |
        Out-Null

    $databaseCreated = $true

    $env:POSTGRES_HOST = $PostgreSqlHost
    $env:POSTGRES_PORT = [string]$PostgreSqlPort
    $env:POSTGRES_DB = $database
    $env:POSTGRES_USER = $PostgreSqlUser
    $env:POSTGRES_PASSWORD = $PostgreSqlPassword
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$apiPort"
    $env:ConnectionStrings__PostgreSql =
        "Host=$PostgreSqlHost;Port=$PostgreSqlPort;Database=$database;Username=$PostgreSqlUser;Password=$PostgreSqlPassword"
    $env:Authentication__RequireAuthenticatedUser = 'false'

    Push-Location $root
    try {
        & dotnet build DevelopmentIntelligencePlatform.sln `
            -c Release `
            --nologo

        if ($LASTEXITCODE -ne 0) {
            throw 'Release build failed.'
        }
    }
    finally {
        Pop-Location
    }

    $dll =
        Join-Path `
            $root `
            'src\Dip.Api\bin\Release\net8.0\Dip.Api.dll'

    $api =
        Start-Process `
            dotnet `
            -ArgumentList @($dll) `
            -WorkingDirectory $root `
            -PassThru `
            -WindowStyle Hidden

    Wait-Http "http://127.0.0.1:$apiPort/health"

    $projectIds = @()

    for ($index = 0; $index -lt $ProjectCount; $index++) {
        $body = @{
            name = "Governance Load $index"
            repositoryUrl = "https://example.invalid/load-$index"
            defaultBranch = 'main'
            releaseDateUtc = $null
        } | ConvertTo-Json

        $project =
            Invoke-RestMethod `
                -Method Post `
                -Uri "http://127.0.0.1:$apiPort/api/projects" `
                -ContentType application/json `
                -Body $body

        $projectIds += [Guid]$project.id
    }

    foreach ($projectId in $projectIds) {
        $sql = @"
INSERT INTO qa_finding_states(id,project_id,fingerprint,last_finding_id,subject_key,last_commit_sha,status,occurrences,team,workstream,ownership_source,disposition_reason,updated_by,first_seen_at_utc,last_seen_at_utc,updated_at_utc,version)
SELECT gen_random_uuid(),'$projectId'::uuid,md5('${projectId}:'||g::text)||md5('${projectId}:x:'||g::text),gen_random_uuid(),'load:'||g,'0123456789abcdef0123456789abcdef01234567','Confirmed',1,'Load Team','Load','Rule',NULL,NULL,clock_timestamp()+(g||' microseconds')::interval,clock_timestamp()+(g||' microseconds')::interval,now(),1 FROM generate_series(1,$FindingsPerProject) g;
"@

        Invoke-Psql `
            -Database $database `
            -Sql $sql |
            Out-Null
    }

    $handler =
        [Net.Http.HttpClientHandler]::new()

    $client =
        [Net.Http.HttpClient]::new($handler)

    $client.Timeout =
        [TimeSpan]::FromSeconds(30)

    $readLat =
        [Collections.Generic.List[double]]::new()

    $writeLat =
        [Collections.Generic.List[double]]::new()

    $errors =
        [Collections.Generic.List[string]]::new()

    for (
        $base = 0;
        $base -lt $ReadRequests;
        $base += $ConcurrentReaders)
    {
        $pending =
            [Collections.ArrayList]::new()

        $count =
            [Math]::Min(
                $ConcurrentReaders,
                $ReadRequests - $base)

        for ($offset = 0; $offset -lt $count; $offset++) {
            $requestIndex =
                $base + $offset

            $projectId =
                $projectIds[
                    $requestIndex % $projectIds.Count
                ]

            $watch =
                [Diagnostics.Stopwatch]::StartNew()

            $task =
                $client.GetAsync(
                    "http://127.0.0.1:$apiPort/api/v1/qa-governance/projects/$projectId/findings?pageSize=100")

            [void]$pending.Add(
                [pscustomobject]@{
                    Task = $task
                    Stopwatch = $watch
                    Kind = 'read'
                    Content = $null
                })
        }

        Wait-HttpBatch `
            $pending `
            $readLat `
            $errors
    }

    $targets = @()

    foreach ($projectId in $projectIds) {
        $page =
            Invoke-RestMethod `
                "http://127.0.0.1:$apiPort/api/v1/qa-governance/projects/$projectId/findings?pageSize=$([Math]::Min(100,$WriteRequests))"

        foreach ($item in $page.items) {
            $targets += @{
                projectId = $projectId
                fingerprint = $item.fingerprint
                version = $item.version
            }

            if ($targets.Count -ge $WriteRequests) {
                break
            }
        }

        if ($targets.Count -ge $WriteRequests) {
            break
        }
    }

    for (
        $base = 0;
        $base -lt $targets.Count;
        $base += $ConcurrentWriters)
    {
        $pending =
            [Collections.ArrayList]::new()

        $count =
            [Math]::Min(
                $ConcurrentWriters,
                $targets.Count - $base)

        for ($offset = 0; $offset -lt $count; $offset++) {
            $target =
                $targets[$base + $offset]

            $payload = @{
                projectId = $target.projectId
                fingerprint = $target.fingerprint
                status = 'Assigned'
                reason = 'Governance formal concurrency test'
                expectedVersion = $target.version
            } | ConvertTo-Json

            $content =
                [Net.Http.StringContent]::new(
                    $payload,
                    [Text.Encoding]::UTF8,
                    'application/json')

            $watch =
                [Diagnostics.Stopwatch]::StartNew()

            $task =
                $client.PostAsync(
                    "http://127.0.0.1:$apiPort/api/v1/qa-governance/findings/transition",
                    $content)

            [void]$pending.Add(
                [pscustomobject]@{
                    Task = $task
                    Stopwatch = $watch
                    Kind = 'write'
                    Content = $content
                })
        }

        Wait-HttpBatch `
            $pending `
            $writeLat `
            $errors
    }

    $client.Dispose()

    $total =
        $ProjectCount * $FindingsPerProject

    $readP95 =
        Get-P95 $readLat.ToArray()

    $writeP95 =
        Get-P95 $writeLat.ToArray()

    $errorCount =
        $errors.Count

    $errorRate =
        [Math]::Round(
            100.0 * $errorCount /
            [Math]::Max(
                1,
                $ReadRequests + $targets.Count),
            3)

    $passed =
        $readP95 -lt 750 `
        -and $writeP95 -lt 1000 `
        -and $errorRate -lt 1

    $sha = (
        & git -C $root rev-parse HEAD |
        Out-String
    ).Trim()

    $runtime =
        Get-DipPostgresRuntimeLabel `
            -Client $pg

    $result = [ordered]@{
        schema = 'dip-governance-load-v4'
        runtime = $runtime
        sourceCommit = $sha
        measuredAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        projects = $ProjectCount
        findingStates = $total
        readRequests = $ReadRequests
        writeRequests = $targets.Count
        concurrentReaders = $ConcurrentReaders
        concurrentWriters = $ConcurrentWriters
        readP95Ms = $readP95
        writeP95Ms = $writeP95
        errorRatePercent = $errorRate
        targets = [ordered]@{
            readP95Ms = 750
            writeP95Ms = 1000
            errorRatePercent = 1
        }
        passed = $passed
        errors = @(
            $errors |
            Select-Object -First 20
        )
    }

    $result |
        ConvertTo-Json -Depth 8 |
        Set-Content `
            (Join-Path $out 'governance-load-results.json') `
            -Encoding UTF8

    if (!$passed) {
        throw (
            "Governance load test failed. " +
            "read p95=$readP95 write p95=$writeP95 error=$errorRate%"
        )
    }

    @(
        'GOVERNANCE LOAD PASS'
        "Commit=$sha"
        "Runtime=$runtime"
        "FindingStates=$total"
        "ReadP95Ms=$readP95"
        "WriteP95Ms=$writeP95"
        "ErrorRatePercent=$errorRate"
    ) |
        Set-Content `
            (Join-Path $out 'GOVERNANCE_LOAD_PASS.txt') `
            -Encoding UTF8
}
finally {
    if ($api -and !$api.HasExited) {
        Stop-Process `
            $api.Id `
            -Force `
            -ErrorAction SilentlyContinue
    }

    if ($databaseCreated) {
        try {
            Invoke-Psql `
                -Database 'postgres' `
                -Sql "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$database';" |
                Out-Null
        }
        catch {
        }

        try {
            Invoke-Psql `
                -Database 'postgres' `
                -Sql "DROP DATABASE IF EXISTS `"$database`";" |
                Out-Null
        }
        catch {
        }
    }

    foreach ($name in $vars) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $old[$name],
            'Process')
    }
}
