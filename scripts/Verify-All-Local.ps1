param(
    [ValidateSet("Quick", "Full")]
    [string]$Mode = "Quick"
)

$ErrorActionPreference = "Stop"

[Console]::OutputEncoding =
    [System.Text.UTF8Encoding]::new($false)

$OutputEncoding =
    [System.Text.UTF8Encoding]::new($false)

$env:DOTNET_CLI_UI_LANGUAGE = "en-US"

$RepoRoot =
    Split-Path -Parent $PSScriptRoot

Set-Location -LiteralPath $RepoRoot


function Header {
    param([string]$Text)

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host " $Text" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
}


function Run-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Header $Name

    $global:LASTEXITCODE = 0

    & $Action

    if ($LASTEXITCODE -ne 0) {

        throw "$Name failed. Exit code: $LASTEXITCODE"
    }

    Write-Host ""
    Write-Host "[PASS] $Name" -ForegroundColor Green
}


function Ensure-Docker {

    Header "Docker Engine"

    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {

        throw "Docker CLI is not installed. Full CI requires Docker."
    }

    docker info *> $null

    if ($LASTEXITCODE -eq 0) {

        Write-Host "[PASS] Docker is already running." -ForegroundColor Green
        return
    }

    Write-Host "Docker Engine is not running." -ForegroundColor Yellow
    Write-Host "Trying to start Docker Desktop..." -ForegroundColor Yellow

    $DockerCandidates = @(
        "$env:ProgramFiles\Docker\Docker\Docker Desktop.exe",
        "${env:ProgramFiles(x86)}\Docker\Docker\Docker Desktop.exe",
        "$env:LOCALAPPDATA\Docker\Docker Desktop.exe"
    )

    $DockerDesktop =
        $DockerCandidates |
        Where-Object {
            $_ -and (Test-Path -LiteralPath $_)
        } |
        Select-Object -First 1

    if (-not $DockerDesktop) {

        throw @"
Docker Desktop executable was not found.

Quick CI can run without Docker.
Full CI requires a running Docker-compatible engine.
"@
    }

    Start-Process `
        -FilePath $DockerDesktop `
        -ErrorAction Stop

    Write-Host ""
    Write-Host "Waiting for Docker Engine..." -ForegroundColor Cyan

    $Ready = $false

    for ($i = 1; $i -le 90; $i++) {

        Start-Sleep -Seconds 2

        docker info *> $null

        if ($LASTEXITCODE -eq 0) {

            $Ready = $true
            break
        }

        Write-Host "." -NoNewline
    }

    Write-Host ""

    if (-not $Ready) {

        throw "Docker Engine did not become ready within 180 seconds."
    }

    Write-Host "[PASS] Docker Engine ready." -ForegroundColor Green
}


try {

    Header "DIP LOCAL CI - $Mode"

    Write-Host "Repository : $RepoRoot"
    Write-Host "Mode       : $Mode"


    # ================================================================
    # Required tools
    # ================================================================

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {

        throw ".NET SDK is not installed."
    }


    # ================================================================
    # .NET build
    # ================================================================

    Run-Step ".NET Restore" {

        dotnet restore `
            DevelopmentIntelligencePlatform.sln
    }


    Run-Step ".NET Release Build" {

        dotnet build `
            DevelopmentIntelligencePlatform.sln `
            --configuration Release `
            --no-restore
    }


    # ================================================================
    # Fast tests
    # ================================================================

    Run-Step "Domain Tests" {

        dotnet test `
            tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj `
            --configuration Release `
            --no-build
    }


    Run-Step "Application Tests" {

        dotnet test `
            tests/Dip.Application.Tests/Dip.Application.Tests.csproj `
            --configuration Release `
            --no-build
    }


    Run-Step "Infrastructure Tests" {

        dotnet test `
            tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj `
            --configuration Release `
            --no-build
    }


    # ================================================================
    # NuGet vulnerability audit
    # ================================================================

    Header "NuGet Vulnerability Audit"

    $NuGetOutput = & dotnet list `
        DevelopmentIntelligencePlatform.sln `
        package `
        --vulnerable `
        --include-transitive 2>&1

    $NuGetExitCode = $LASTEXITCODE

    $NuGetText =
        $NuGetOutput |
        Out-String

    Write-Host $NuGetText

    if ($NuGetExitCode -ne 0) {

        throw "NuGet vulnerability audit command failed."
    }

    if (
        $NuGetText -match
        "has the following vulnerable packages"
    ) {

        throw "Vulnerable NuGet package(s) detected."
    }

    Write-Host "[PASS] NuGet Vulnerability Audit" -ForegroundColor Green


    # ================================================================
    # Python
    # ================================================================

    $PythonCommand = $null

    if (Get-Command python -ErrorAction SilentlyContinue) {

        $PythonCommand = "python"
    }
    elseif (Get-Command py -ErrorAction SilentlyContinue) {

        $PythonCommand = "py"
    }

    if ($PythonCommand) {

        Run-Step "Python Compile Check" {

            & $PythonCommand `
                -m compileall `
                -q `
                services/python-analysis/app `
                collectors/unreal-collector
        }

    }
    else {

        Write-Host ""
        Write-Host "[WARN] Python not installed. Python compile check skipped." -ForegroundColor Yellow
    }


    # ================================================================
    # Git conflict check
    # ================================================================

    Header "Git Conflict Marker Check"

    $ConflictOutput = & git grep `
        -n `
        -E `
        '^(<<<<<<<|=======|>>>>>>>)' `
        -- `
        ':!*.md' `
        ':!*.txt'

    $ConflictExit = $LASTEXITCODE

    if ($ConflictExit -eq 0) {

        Write-Host $ConflictOutput

        throw "Unresolved merge conflict marker detected."
    }

    if ($ConflictExit -ne 1) {

        throw "git grep failed."
    }

    $global:LASTEXITCODE = 0

    Write-Host "[PASS] Git Conflict Marker Check" -ForegroundColor Green


    # ================================================================
    # Full only
    # ================================================================

    if ($Mode -eq "Full") {

        Ensure-Docker


        Run-Step "Docker Compose Config" {

            docker compose `
                -f docker-compose.yml `
                config `
                --quiet
        }


        # Integration tests require Testcontainers -> Docker
        Run-Step "Integration Test Project" {

            dotnet test `
                tests/Dip.IntegrationTests/Dip.IntegrationTests.csproj `
                --configuration Release `
                --no-build
        }


        $DockerImages = @(
            @{
                Name       = "API"
                Dockerfile = "infrastructure/docker/api.Dockerfile"
                Tag        = "dip-local-api"
            },
            @{
                Name       = "Web"
                Dockerfile = "infrastructure/docker/web.Dockerfile"
                Tag        = "dip-local-web"
            },
            @{
                Name       = "Worker"
                Dockerfile = "infrastructure/docker/worker.Dockerfile"
                Tag        = "dip-local-worker"
            },
            @{
                Name       = "Analysis"
                Dockerfile = "infrastructure/docker/analysis.Dockerfile"
                Tag        = "dip-local-analysis"
            }
        )


        foreach ($Image in $DockerImages) {

            $Name       = $Image.Name
            $Dockerfile = $Image.Dockerfile
            $Tag        = $Image.Tag

            Run-Step "Docker Build $Name" {

                docker build `
                    --file $Dockerfile `
                    --tag "${Tag}:verify" `
                    .
            }
        }
    }


    # ================================================================
    # Success
    # ================================================================

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host " LOCAL CI PASSED - $Mode" -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host ""

    if ($Mode -eq "Quick") {

        Write-Host "Quick CI passed." -ForegroundColor Green
        Write-Host "git push is allowed." -ForegroundColor Green
        Write-Host ""
        Write-Host "Full verification:" -ForegroundColor Cyan
        Write-Host "  .\Verify-Local-CI-Full.cmd"
    }

    exit 0
}
catch {

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host " LOCAL CI FAILED - $Mode" -ForegroundColor Red
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""

    exit 1
}