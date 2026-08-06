[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$KeepServicesRunning,

    [ValidateRange(60, 600)]
    [int]$DockerTimeoutSeconds = 180,

    [ValidateRange(30, 600)]
    [int]$ApplicationTimeoutSeconds = 120,

    [ValidateRange(1024, 65535)]
    [int]$PostgreSqlPort = 55432,

    [ValidateRange(1024, 65535)]
    [int]$RedisPort = 56379,

    [ValidateRange(1024, 65535)]
    [int]$ApiPort = 55080,

    [ValidateRange(1024, 65535)]
    [int]$WebPort = 55081
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path `
    $repositoryRoot `
    "DevelopmentIntelligencePlatform.sln"

$artifactDirectory = Join-Path `
    $repositoryRoot `
    "artifacts\final-verification"

$testResultsDirectory = Join-Path `
    $artifactDirectory `
    "test-results"

$completionPath = Join-Path `
    $artifactDirectory `
    "PROJECT_COMPLETION.txt"

$failurePath = Join-Path `
    $artifactDirectory `
    "PROJECT_COMPLETION_FAILED.txt"

$composeProjectName = "dip-final-verification"
$applicationProcesses = New-Object System.Collections.ArrayList
$composeStarted = $false
$verificationSucceeded = $false

function Write-Step {
    param([Parameter(Mandatory = $true)][string]$Message)

    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Assert-Command {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Invoke-External {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [switch]$IgnoreExitCode
    )

    & $FilePath @Arguments
    $exitCode = $LASTEXITCODE

    if (-not $IgnoreExitCode -and $exitCode -ne 0) {
        throw "$FilePath $($Arguments -join ' ') failed with exit code $exitCode."
    }

    return $exitCode
}

function Invoke-DockerCompose {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [switch]$IgnoreExitCode
    )

    $allArguments = @(
        "compose",
        "--project-name",
        $composeProjectName
    ) + $Arguments

    return Invoke-External `
        -FilePath "docker" `
        -Arguments $allArguments `
        -IgnoreExitCode:$IgnoreExitCode
}

function Test-DockerEngine {
    & docker info *> $null
    return $LASTEXITCODE -eq 0
}

function Start-DockerEngine {
    if (Test-DockerEngine) {
        return
    }

    Write-Step "Docker Engine is not running; attempting to start Docker Desktop"

    $candidates = @(
        (Join-Path `
            $env:ProgramFiles `
            "Docker\Docker\Docker Desktop.exe"),
        (Join-Path `
            $env:LOCALAPPDATA `
            "Docker\Docker Desktop.exe")
    )

    $dockerDesktop = $candidates |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1

    if (-not $dockerDesktop) {
        throw @"
Docker Engine is unavailable and Docker Desktop could not be found.
Install Docker Desktop with the WSL 2 backend, start it once, and rerun this script.
"@
    }

    Start-Process -FilePath $dockerDesktop | Out-Null

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(
        $DockerTimeoutSeconds)

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        Start-Sleep -Seconds 2

        if (Test-DockerEngine) {
            Write-Host "Docker Engine is ready."
            return
        }
    }

    throw @"
Docker Desktop was started, but its Linux Engine did not become ready within
$DockerTimeoutSeconds seconds. Open Docker Desktop and repair its WSL 2
integration before rerunning the verification.
"@
}

function Wait-ComposeServiceHealthy {
    param(
        [Parameter(Mandatory = $true)][string]$Service,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(
        $TimeoutSeconds)

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $composeArguments = @(
            "compose",
            "--project-name",
            $composeProjectName,
            "ps",
            "-q",
            $Service
        )

        $containerId = (
            & docker @composeArguments |
            Out-String
        ).Trim()

        if ($LASTEXITCODE -eq 0 -and $containerId) {
            $status = (
                & docker inspect `
                    --format `
                    "{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}" `
                    $containerId |
                Out-String
            ).Trim()

            if ($LASTEXITCODE -eq 0 -and
                $status -in @("healthy", "running")) {
                Write-Host "$Service is $status."
                return
            }

            if ($status -in @("exited", "dead")) {
                Invoke-DockerCompose `
                    -Arguments @("logs", "--no-color", $Service) `
                    -IgnoreExitCode | Out-Null

                throw "Docker service '$Service' stopped unexpectedly."
            }
        }

        Start-Sleep -Seconds 2
    }

    Invoke-DockerCompose `
        -Arguments @("logs", "--no-color", $Service) `
        -IgnoreExitCode | Out-Null

    throw "Docker service '$Service' did not become healthy within $TimeoutSeconds seconds."
}

function Get-LogTail {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return "(log file was not created)"
    }

    return (
        Get-Content `
            -LiteralPath $Path `
            -Tail 100 `
            -ErrorAction SilentlyContinue |
        Out-String
    )
}

function Start-DotNetProject {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Project,
        [string]$Urls
    )

    if ([string]::IsNullOrWhiteSpace($Urls)) {
        Remove-Item `
            Env:ASPNETCORE_URLS `
            -ErrorAction SilentlyContinue
    }
    else {
        $env:ASPNETCORE_URLS = $Urls
    }

    $standardOutput = Join-Path `
        $artifactDirectory `
        "$Name.stdout.log"

    $standardError = Join-Path `
        $artifactDirectory `
        "$Name.stderr.log"

    Remove-Item `
        -LiteralPath $standardOutput `
        -Force `
        -ErrorAction SilentlyContinue

    Remove-Item `
        -LiteralPath $standardError `
        -Force `
        -ErrorAction SilentlyContinue

    $process = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList @(
            "run",
            "--project",
            $Project,
            "--configuration",
            $Configuration,
            "--no-build",
            "--no-restore"
        ) `
        -WorkingDirectory $repositoryRoot `
        -RedirectStandardOutput $standardOutput `
        -RedirectStandardError $standardError `
        -PassThru

    [void]$applicationProcesses.Add(
        [pscustomobject]@{
            Name = $Name
            Process = $process
            StandardOutput = $standardOutput
            StandardError = $standardError
        })

    return $process
}

function Wait-HttpEndpoint {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $true)]$Process,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(
        $TimeoutSeconds)

    $lastError = "No response was received."

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if ($Process.HasExited) {
            $entry = $applicationProcesses |
                Where-Object { $_.Process.Id -eq $Process.Id } |
                Select-Object -First 1

            throw @"
$Name exited before becoming ready.
STDOUT:
$(Get-LogTail $entry.StandardOutput)
STDERR:
$(Get-LogTail $entry.StandardError)
"@
        }

        try {
            $response = Invoke-WebRequest `
                -Uri $Uri `
                -UseBasicParsing `
                -TimeoutSec 5

            if ($response.StatusCode -ge 200 -and
                $response.StatusCode -lt 400) {
                Write-Host "$Name is ready at $Uri"
                return
            }

            $lastError = "HTTP $($response.StatusCode)"
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Seconds 1
    }

    throw "$Name did not become ready within $TimeoutSeconds seconds. Last error: $lastError"
}

function Stop-ApplicationProcesses {
    foreach ($entry in $applicationProcesses) {
        try {
            if (-not $entry.Process.HasExited) {
                & taskkill `
                    /PID $entry.Process.Id `
                    /T `
                    /F *> $null
            }
        }
        catch {
            Write-Warning "Could not stop $($entry.Name): $($_.Exception.Message)"
        }
    }
}

if (-not (Test-Path -LiteralPath $solutionPath)) {
    throw "Solution not found: $solutionPath"
}

New-Item `
    -ItemType Directory `
    -Path $artifactDirectory `
    -Force | Out-Null

New-Item `
    -ItemType Directory `
    -Path $testResultsDirectory `
    -Force | Out-Null

Remove-Item `
    -LiteralPath $completionPath `
    -Force `
    -ErrorAction SilentlyContinue

Remove-Item `
    -LiteralPath $failurePath `
    -Force `
    -ErrorAction SilentlyContinue

Push-Location $repositoryRoot

try {
    Write-Step "Checking required tools"

    Assert-Command "dotnet"
    Assert-Command "docker"
    Assert-Command "git"

    $sdkVersion = (& dotnet --version | Out-String).Trim()

    if (-not $sdkVersion.StartsWith("8.")) {
        throw "The project requires .NET SDK 8.x, but '$sdkVersion' is active."
    }

    Start-DockerEngine

    Invoke-External `
        -FilePath "docker" `
        -Arguments @("compose", "version") | Out-Null

    Write-Step "Starting isolated PostgreSQL and Redis verification services"

    $env:POSTGRES_DB = "dip_verification"
    $env:POSTGRES_USER = "dip"
    $env:POSTGRES_PASSWORD = "dip-verification-password"
    $env:POSTGRES_PORT = $PostgreSqlPort.ToString()
    $env:REDIS_PORT = $RedisPort.ToString()

    $env:ConnectionStrings__PostgreSql = (
        "Host=127.0.0.1;" +
        "Port=$PostgreSqlPort;" +
        "Database=dip_verification;" +
        "Username=dip;" +
        "Password=dip-verification-password"
    )

    $env:Redis__ConnectionString =
        "127.0.0.1:$RedisPort"

    $env:ASPNETCORE_ENVIRONMENT = "Testing"
    $env:DOTNET_ENVIRONMENT = "Testing"
    $env:GitHub__WebhookSecret =
        "local-final-verification-secret"
    $env:GitHub__Token =
        "local-final-verification-token"

    Invoke-DockerCompose `
        -Arguments @(
            "config",
            "--quiet"
        ) | Out-Null

    Invoke-DockerCompose `
        -Arguments @(
            "down",
            "--volumes",
            "--remove-orphans"
        ) `
        -IgnoreExitCode | Out-Null

    Invoke-DockerCompose `
        -Arguments @(
            "up",
            "-d",
            "postgres",
            "redis"
        ) | Out-Null

    $composeStarted = $true

    Wait-ComposeServiceHealthy `
        -Service "postgres" `
        -TimeoutSeconds $DockerTimeoutSeconds

    Wait-ComposeServiceHealthy `
        -Service "redis" `
        -TimeoutSeconds $DockerTimeoutSeconds

    Write-Step "Cleaning generated build output"

    Remove-Item `
        -LiteralPath (Join-Path $repositoryRoot ".vs") `
        -Recurse `
        -Force `
        -ErrorAction SilentlyContinue

    Get-ChildItem `
        -Path $repositoryRoot `
        -Recurse `
        -Directory `
        -Filter bin |
        Remove-Item `
            -Recurse `
            -Force `
            -ErrorAction SilentlyContinue

    Get-ChildItem `
        -Path $repositoryRoot `
        -Recurse `
        -Directory `
        -Filter obj |
        Remove-Item `
            -Recurse `
            -Force `
            -ErrorAction SilentlyContinue

    Remove-Item `
        -LiteralPath $testResultsDirectory `
        -Recurse `
        -Force `
        -ErrorAction SilentlyContinue

    New-Item `
        -ItemType Directory `
        -Path $testResultsDirectory `
        -Force | Out-Null

    Write-Step "Restoring the complete solution"

    Invoke-External `
        -FilePath "dotnet" `
        -Arguments @(
            "restore",
            $solutionPath
        ) | Out-Null

    Write-Step "Building the complete solution in $Configuration"

    Invoke-External `
        -FilePath "dotnet" `
        -Arguments @(
            "build",
            $solutionPath,
            "--configuration",
            $Configuration,
            "--no-restore"
        ) | Out-Null

    Write-Step "Running domain unit tests"

    Invoke-External `
        -FilePath "dotnet" `
        -Arguments @(
            "test",
            "tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj",
            "--configuration",
            $Configuration,
            "--no-build",
            "--logger",
            "trx;LogFileName=domain-tests.trx",
            "--results-directory",
            $testResultsDirectory,
            "--blame-hang-timeout",
            "5m"
        ) | Out-Null

    Write-Step "Running PostgreSQL API integration tests"

    Invoke-External `
        -FilePath "dotnet" `
        -Arguments @(
            "test",
            "tests/Dip.IntegrationTests/Dip.IntegrationTests.csproj",
            "--configuration",
            $Configuration,
            "--no-build",
            "--logger",
            "trx;LogFileName=integration-tests.trx",
            "--results-directory",
            $testResultsDirectory,
            "--blame-hang-timeout",
            "5m"
        ) | Out-Null

    Write-Step "Starting API"

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:DOTNET_ENVIRONMENT = "Development"

    $apiProcess = Start-DotNetProject `
        -Name "Dip.Api" `
        -Project "src/Dip.Api/Dip.Api.csproj" `
        -Urls "http://127.0.0.1:$ApiPort"

    Wait-HttpEndpoint `
        -Name "Dip.Api" `
        -Uri "http://127.0.0.1:$ApiPort/health" `
        -Process $apiProcess `
        -TimeoutSeconds $ApplicationTimeoutSeconds

    Write-Step "Starting Worker"

    $workerProcess = Start-DotNetProject `
        -Name "Dip.Worker" `
        -Project "src/Dip.Worker/Dip.Worker.csproj"

    Start-Sleep -Seconds 3

    if ($workerProcess.HasExited) {
        $entry = $applicationProcesses |
            Where-Object {
                $_.Process.Id -eq $workerProcess.Id
            } |
            Select-Object -First 1

        throw @"
Dip.Worker exited during startup.
STDOUT:
$(Get-LogTail $entry.StandardOutput)
STDERR:
$(Get-LogTail $entry.StandardError)
"@
    }

    Write-Step "Starting Web dashboard"

    $env:Api__BaseUrl =
        "http://127.0.0.1:$ApiPort/"

    $webProcess = Start-DotNetProject `
        -Name "Dip.Web" `
        -Project "src/Dip.Web/Dip.Web.csproj" `
        -Urls "http://127.0.0.1:$WebPort"

    Wait-HttpEndpoint `
        -Name "Dip.Web" `
        -Uri "http://127.0.0.1:$WebPort/" `
        -Process $webProcess `
        -TimeoutSeconds $ApplicationTimeoutSeconds

    Write-Step "Running end-to-end collector pipeline smoke test"

    $verificationId = Guid.NewGuid().ToString("N")
    $repositoryUrl =
        "https://github.com/example/dip-verification-$verificationId"

    $projectBody = @{
        name = "Final Verification $verificationId"
        repositoryUrl = $repositoryUrl
        defaultBranch = "main"
        releaseDateUtc = $null
    } | ConvertTo-Json -Depth 10

    $project = Invoke-RestMethod `
        -Method Post `
        -Uri "http://127.0.0.1:$ApiPort/api/projects" `
        -ContentType "application/json" `
        -Body $projectBody `
        -TimeoutSec 15

    if (-not $project.id) {
        throw "Project creation did not return an id."
    }

    $startedAt =
        [DateTimeOffset]::UtcNow.AddSeconds(-2)
    $finishedAt =
        [DateTimeOffset]::UtcNow

    $reportBody = @{
        schemaVersion = "1.0"
        reportId = "verification-report-$verificationId"
        externalRunId = "verification-run-$verificationId"
        attemptNumber = 1
        collectorType = "final-verification"
        collectorVersion = "1.0.0"
        projectId = $project.id
        repository = $repositoryUrl
        commitSha = "abcdef1234567890"
        branch = "main"
        startedAt = $startedAt.ToString("O")
        finishedAt = $finishedAt.ToString("O")
        status = "Succeeded"
        metrics = @(
            @{
                name = "verification.signal"
                value = 1
                unit = "count"
                baselineValue = $null
                environmentId = "local-final-verification"
                scenarioId = "p0-mvp"
                platform = "windows"
                buildConfiguration = $Configuration
                benchmarkVersion = "1.0"
            }
        )
        issues = @()
    } | ConvertTo-Json -Depth 20

    $collectorResult = Invoke-RestMethod `
        -Method Post `
        -Uri "http://127.0.0.1:$ApiPort/api/v1/collector-reports" `
        -ContentType "application/json" `
        -Body $reportBody `
        -TimeoutSec 30

    if (-not $collectorResult.pipelineRunId) {
        throw "Collector report processing did not return a PipelineRunId."
    }

    $pipelineRun = Invoke-RestMethod `
        -Method Get `
        -Uri (
            "http://127.0.0.1:$ApiPort/" +
            "api/v1/pipeline-runs/" +
            $collectorResult.pipelineRunId
        ) `
        -TimeoutSec 15

    if ($pipelineRun.status -ne "Succeeded") {
        throw "PipelineRun status was '$($pipelineRun.status)', not 'Succeeded'."
    }

    $qualityGateStatus = $null
    $qualityGateDeadline =
        [DateTimeOffset]::UtcNow.AddSeconds(
            $ApplicationTimeoutSeconds)

    while ([DateTimeOffset]::UtcNow -lt $qualityGateDeadline) {
        if ($workerProcess.HasExited) {
            $entry = $applicationProcesses |
                Where-Object {
                    $_.Process.Id -eq $workerProcess.Id
                } |
                Select-Object -First 1

            throw @"
Dip.Worker exited while processing the quality gate.
STDOUT:
$(Get-LogTail $entry.StandardOutput)
STDERR:
$(Get-LogTail $entry.StandardError)
"@
        }

        try {
            $dashboard = Invoke-RestMethod `
                -Method Get `
                -Uri (
                    "http://127.0.0.1:$ApiPort/" +
                    "api/v1/dashboard/projects/" +
                    $project.id
                ) `
                -TimeoutSec 10

            $qualityGateStatus =
                $dashboard.qualityGateStatus

            if (-not [string]::IsNullOrWhiteSpace(
                    $qualityGateStatus)) {
                break
            }
        }
        catch {
            # The quality-gate result is asynchronous. Retry until timeout.
        }

        Start-Sleep -Seconds 1
    }

    if ($qualityGateStatus -ne "Passed") {
        throw "Quality Gate did not reach 'Passed'. Actual='$qualityGateStatus'."
    }

    if ($apiProcess.HasExited -or
        $workerProcess.HasExited -or
        $webProcess.HasExited) {
        throw "At least one application process stopped during final verification."
    }

    $commitSha = (
        & git rev-parse HEAD |
        Out-String
    ).Trim()

    $completedAtUtc =
        [DateTimeOffset]::UtcNow.ToString("O")

    @"
PROJECT COMPLETION VERIFIED
===========================

Project: Development Intelligence Platform
Scope: P0 MVP
Commit: $commitSha
CompletedAtUtc: $completedAtUtc
Configuration: $Configuration
DotNetSdk: $sdkVersion

Verified:
- Docker Engine and Compose
- isolated PostgreSQL
- isolated Redis
- full solution restore
- full solution build
- domain unit tests
- PostgreSQL API integration tests
- Dip.Api startup and health endpoint
- Dip.Worker startup
- Dip.Web startup
- project creation
- collector report validation and persistence
- outbox publication
- Redis queue consumption
- asynchronous Quality Gate evaluation
- dashboard projection
- PipelineRun status: Succeeded
- Quality Gate status: Passed

This declaration is generated only after every verification step succeeds.
"@ | Set-Content `
        -LiteralPath $completionPath `
        -Encoding UTF8

    $verificationSucceeded = $true

    Write-Host ""
    Write-Host "PROJECT COMPLETION VERIFIED" `
        -ForegroundColor Green
    Write-Host "Declaration: $completionPath"

    if ($KeepServicesRunning) {
        Write-Host "API: http://127.0.0.1:$ApiPort"
        Write-Host "Web: http://127.0.0.1:$WebPort"
        Write-Host "PostgreSQL host port: $PostgreSqlPort"
        Write-Host "Redis host port: $RedisPort"
    }
}
catch {
    $message = $_.Exception.Message
    $failedAtUtc =
        [DateTimeOffset]::UtcNow.ToString("O")

    @"
PROJECT COMPLETION NOT VERIFIED
===============================

FailedAtUtc: $failedAtUtc
Reason:
$message

Review the logs in:
$artifactDirectory
"@ | Set-Content `
        -LiteralPath $failurePath `
        -Encoding UTF8

    Write-Error $message
    throw
}
finally {
    if (-not (
            $verificationSucceeded -and
            $KeepServicesRunning)) {
        Stop-ApplicationProcesses

        if ($composeStarted) {
            Invoke-DockerCompose `
                -Arguments @(
                    "down",
                    "--volumes",
                    "--remove-orphans"
                ) `
                -IgnoreExitCode | Out-Null
        }
    }

    Pop-Location
}
