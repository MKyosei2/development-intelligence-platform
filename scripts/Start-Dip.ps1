[CmdletBinding()]
param(
    [switch]$ResetData,
    [switch]$NoBrowser,
    [switch]$SkipBuild,
    [bool]$SeedDemoProject = $true,

    [ValidateRange(60, 600)]
    [int]$DockerTimeoutSeconds = 240,

    [ValidateRange(60, 900)]
    [int]$StartupTimeoutSeconds = 300
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repositoryRoot "docker-compose.yml"
$runtimeDirectory = Join-Path $repositoryRoot "artifacts\runtime"
$statePath = Join-Path $runtimeDirectory "dip-runtime.json"
$diagnosticPath = Join-Path $runtimeDirectory "startup-diagnostics.txt"

function Write-Step {
    param([Parameter(Mandatory = $true)][string]$Message)

    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [switch]$IgnoreExitCode
    )

    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        & $FilePath @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    if (-not $IgnoreExitCode -and $exitCode -ne 0) {
        throw "$FilePath $($Arguments -join ' ') failed with exit code $exitCode."
    }

    return $exitCode
}

function Get-DockerExecutable {
    $command = Get-Command "docker.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path $env:ProgramFiles "Docker\Docker\resources\bin\docker.exe"),
        (Join-Path $env:LOCALAPPDATA "Docker\resources\bin\docker.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return $null
}

function Get-DockerDesktopExecutable {
    $candidates = @(
        (Join-Path $env:ProgramFiles "Docker\Docker\Docker Desktop.exe"),
        (Join-Path $env:LOCALAPPDATA "Docker\Docker Desktop.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return $null
}

function Test-DockerEngine {
    param([Parameter(Mandatory = $true)][string]$DockerExe)

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $DockerExe
    $startInfo.Arguments = "info"
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo

    try {
        [void]$process.Start()
        if (-not $process.WaitForExit(15000)) {
            try { $process.Kill() } catch {}
            return $false
        }

        return ($process.ExitCode -eq 0)
    }
    catch {
        return $false
    }
    finally {
        $process.Dispose()
    }
}

function Install-DockerDesktop {
    $winget = Get-Command "winget.exe" -ErrorAction SilentlyContinue
    if ($null -eq $winget) {
        throw @"
Docker Desktop is not installed and winget is unavailable.

Install Docker Desktop manually:
https://www.docker.com/products/docker-desktop/

Then run Start-DIP.cmd again.
"@
    }

    Write-Step "Docker Desktop is missing; installing with winget"

    Invoke-Native `
        -FilePath $winget.Source `
        -Arguments @(
            "install",
            "--id", "Docker.DockerDesktop",
            "-e",
            "--accept-package-agreements",
            "--accept-source-agreements",
            "--silent"
        ) | Out-Null

    $dockerExe = Get-DockerExecutable
    $desktopExe = Get-DockerDesktopExecutable

    if ($null -eq $dockerExe -or $null -eq $desktopExe) {
        throw @"
Docker Desktop installation completed, but the executables are not available yet.
Windows may require sign-out or reboot after the first installation.

Restart Windows if requested, then run Start-DIP.cmd again.
"@
    }
}

function Wait-DockerEngine {
    param(
        [Parameter(Mandatory = $true)][string]$DockerExe,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (Test-DockerEngine -DockerExe $DockerExe) {
            return $true
        }

        Start-Sleep -Seconds 2
    }

    return $false
}

function Ensure-DockerEngine {
    $dockerExe = Get-DockerExecutable

    if ($null -eq $dockerExe) {
        Install-DockerDesktop
        $dockerExe = Get-DockerExecutable
    }

    if (Test-DockerEngine -DockerExe $dockerExe) {
        return $dockerExe
    }

    $desktopExe = Get-DockerDesktopExecutable
    if ($null -eq $desktopExe) {
        Install-DockerDesktop
        $desktopExe = Get-DockerDesktopExecutable
        $dockerExe = Get-DockerExecutable
    }

    Write-Step "Starting Docker Desktop"

    $desktopProcess = Get-Process -Name "Docker Desktop" -ErrorAction SilentlyContinue
    if ($null -eq $desktopProcess) {
        Start-Process -FilePath $desktopExe | Out-Null
    }

    if (Wait-DockerEngine -DockerExe $dockerExe -TimeoutSeconds $DockerTimeoutSeconds) {
        return $dockerExe
    }

    Write-Warning "Docker Engine did not become ready. Trying desktop-linux context."

    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $contexts = @(
            & $dockerExe context ls --format "{{.Name}}" 2>$null
        )
        $contextExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    if ($contextExitCode -eq 0 -and "desktop-linux" -in $contexts) {
        Invoke-Native `
            -FilePath $dockerExe `
            -Arguments @("context", "use", "desktop-linux") `
            -IgnoreExitCode | Out-Null

        if (Wait-DockerEngine -DockerExe $dockerExe -TimeoutSeconds 60) {
            return $dockerExe
        }
    }

    Write-Warning "Docker is still unavailable. Restarting Docker Desktop once."

    Get-Process -Name "Docker Desktop" -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue

    Start-Sleep -Seconds 3
    Start-Process -FilePath $desktopExe | Out-Null

    if (Wait-DockerEngine -DockerExe $dockerExe -TimeoutSeconds $DockerTimeoutSeconds) {
        return $dockerExe
    }

    New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null

    $diagnostics = New-Object System.Collections.Generic.List[string]
    $diagnostics.Add("Docker Engine could not be started.")
    $diagnostics.Add("GeneratedAtUtc: $([DateTimeOffset]::UtcNow.ToString('O'))")
    $diagnostics.Add("")
    $diagnostics.Add("Docker contexts:")

    try {
        $contextText = (& $dockerExe context ls 2>&1 | Out-String)
        $diagnostics.Add($contextText)
    }
    catch {
        $diagnostics.Add($_.Exception.Message)
    }

    $diagnostics.Add("")
    $diagnostics.Add("WSL status:")

    $wsl = Get-Command "wsl.exe" -ErrorAction SilentlyContinue
    if ($null -ne $wsl) {
        try {
            $wslText = (& $wsl.Source --status 2>&1 | Out-String)
            $diagnostics.Add($wslText)
        }
        catch {
            $diagnostics.Add($_.Exception.Message)
        }
    }
    else {
        $diagnostics.Add("wsl.exe was not found.")
    }

    $diagnostics | Set-Content -LiteralPath $diagnosticPath -Encoding UTF8

    throw @"
Docker Desktop exists, but the Linux container engine could not be started automatically.

Diagnostics:
$diagnosticPath

If Docker Desktop asks for WSL2/virtualization installation or a Windows restart,
complete that OS-level requirement once and run Start-DIP.cmd again.
"@
}

function Test-PortAvailable {
    param([Parameter(Mandatory = $true)][int]$Port)

    $listener = $null

    try {
        $listener = New-Object `
            -TypeName System.Net.Sockets.TcpListener `
            -ArgumentList @([System.Net.IPAddress]::Loopback, $Port)

        $listener.Start()
        return $true
    }
    catch {
        return $false
    }
    finally {
        if ($null -ne $listener) {
            $listener.Stop()
        }
    }
}

function Get-FreePort {
    param(
        [Parameter(Mandatory = $true)][int]$Preferred,
        [Parameter(Mandatory = $true)][int]$FallbackStart,
        [Parameter(Mandatory = $true)][int]$FallbackEnd
    )

    if (Test-PortAvailable -Port $Preferred) {
        return $Preferred
    }

    foreach ($port in $FallbackStart..$FallbackEnd) {
        if (Test-PortAvailable -Port $port) {
            return $port
        }
    }

    throw "No free TCP port was found for preferred port $Preferred."
}

function Wait-Http {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastError = "No response."

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        try {
            $response = Invoke-WebRequest `
                -Uri $Uri `
                -UseBasicParsing `
                -TimeoutSec 5

            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Write-Host "$Name is ready: $Uri" -ForegroundColor Green
                return
            }

            $lastError = "HTTP $($response.StatusCode)"
        }
        catch {
            $lastError = $_.Exception.Message
        }

        Start-Sleep -Seconds 2
    }

    throw "$Name did not become ready at $Uri. LastError=$lastError"
}

function Save-ComposeDiagnostics {
    param(
        [Parameter(Mandatory = $true)][string]$DockerExe,
        [Parameter(Mandatory = $true)][string]$Reason
    )

    New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("DIP STARTUP FAILED")
    $lines.Add("==================")
    $lines.Add("GeneratedAtUtc: $([DateTimeOffset]::UtcNow.ToString('O'))")
    $lines.Add("Reason: $Reason")
    $lines.Add("")

    try {
        $lines.Add("docker compose ps")
        $lines.Add((& $DockerExe compose ps 2>&1 | Out-String))
    }
    catch {
        $lines.Add($_.Exception.Message)
    }

    $lines.Add("")

    try {
        $lines.Add("docker compose logs --tail=250")
        $lines.Add((& $DockerExe compose logs --tail=250 2>&1 | Out-String))
    }
    catch {
        $lines.Add($_.Exception.Message)
    }

    $lines | Set-Content -LiteralPath $diagnosticPath -Encoding UTF8
}

function Ensure-DemoProject {
    param([Parameter(Mandatory = $true)][string]$ApiBaseUrl)

    if (-not $SeedDemoProject) {
        return
    }

    try {
        $projects = @(
            Invoke-RestMethod `
                -Method Get `
                -Uri "$ApiBaseUrl/api/projects" `
                -TimeoutSec 15
        )

        if ($projects.Count -gt 0) {
            return
        }

        $body = @{
            name = "DIP Local Demo"
            repositoryUrl = "https://github.com/MKyosei2/development-intelligence-platform"
            defaultBranch = "main"
            releaseDateUtc = $null
        } | ConvertTo-Json -Depth 5

        Invoke-RestMethod `
            -Method Post `
            -Uri "$ApiBaseUrl/api/projects" `
            -ContentType "application/json" `
            -Body $body `
            -TimeoutSec 15 | Out-Null

        Write-Host "Created demo project for the GUI." -ForegroundColor Green
    }
    catch {
        Write-Warning "GUI is running, but demo project creation failed: $($_.Exception.Message)"
    }
}

if (-not (Test-Path -LiteralPath $composeFile)) {
    throw "docker-compose.yml was not found: $composeFile"
}

New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
Remove-Item -LiteralPath $diagnosticPath -Force -ErrorAction SilentlyContinue

Push-Location $repositoryRoot

try {
    Write-Step "Preparing Docker"
    $dockerExe = Ensure-DockerEngine

    Write-Step "Checking Docker Compose"
    Invoke-Native -FilePath $dockerExe -Arguments @("compose", "version") | Out-Null
    Invoke-Native -FilePath $dockerExe -Arguments @("compose", "config", "--quiet") | Out-Null

    Write-Step "Stopping previous DIP containers"
    $downArguments = @("compose", "down", "--remove-orphans")
    if ($ResetData) {
        $downArguments += "--volumes"
    }

    Invoke-Native `
        -FilePath $dockerExe `
        -Arguments $downArguments `
        -IgnoreExitCode | Out-Null

    Write-Step "Selecting available host ports"
    $postgresPort = Get-FreePort -Preferred 5432 -FallbackStart 55432 -FallbackEnd 55499
    $redisPort = Get-FreePort -Preferred 6379 -FallbackStart 56379 -FallbackEnd 56449
    $analysisPort = Get-FreePort -Preferred 8100 -FallbackStart 58100 -FallbackEnd 58199
    $apiPort = Get-FreePort -Preferred 5080 -FallbackStart 55080 -FallbackEnd 55149
    $webPort = Get-FreePort -Preferred 5081 -FallbackStart 55180 -FallbackEnd 55249

    $env:POSTGRES_PORT = $postgresPort.ToString()
    $env:REDIS_PORT = $redisPort.ToString()
    $env:ANALYSIS_PORT = $analysisPort.ToString()
    $env:API_PORT = $apiPort.ToString()
    $env:WEB_PORT = $webPort.ToString()

    Write-Host "PostgreSQL : $postgresPort"
    Write-Host "Redis      : $redisPort"
    Write-Host "Analysis   : $analysisPort"
    Write-Host "API        : $apiPort"
    Write-Host "Web GUI    : $webPort"

    Write-Step "Starting DIP services"

    $upArguments = @(
        "compose",
        "up",
        "-d"
    )

    if (-not $SkipBuild) {
        $upArguments += "--build"
    }

    $upArguments += @(
        "postgres",
        "redis",
        "python-analysis",
        "api",
        "worker",
        "web"
    )

    Invoke-Native -FilePath $dockerExe -Arguments $upArguments | Out-Null

    $apiBaseUrl = "http://127.0.0.1:$apiPort"
    $webUrl = "http://127.0.0.1:$webPort/"

    Write-Step "Waiting for API"
    Wait-Http `
        -Name "DIP API" `
        -Uri "$apiBaseUrl/health" `
        -TimeoutSeconds $StartupTimeoutSeconds

    Write-Step "Waiting for GUI"
    Wait-Http `
        -Name "DIP Web GUI" `
        -Uri $webUrl `
        -TimeoutSeconds $StartupTimeoutSeconds

    Write-Step "Preparing GUI data"
    Ensure-DemoProject -ApiBaseUrl $apiBaseUrl

    $state = [ordered]@{
        startedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        repositoryRoot = $repositoryRoot
        postgresPort = $postgresPort
        redisPort = $redisPort
        analysisPort = $analysisPort
        apiPort = $apiPort
        webPort = $webPort
        apiUrl = $apiBaseUrl
        webUrl = $webUrl
    }

    $state |
        ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath $statePath -Encoding UTF8

    Write-Step "DIP is ready"

    Invoke-Native `
        -FilePath $dockerExe `
        -Arguments @("compose", "ps") `
        -IgnoreExitCode | Out-Null

    Write-Host ""
    Write-Host "GUI     : $webUrl" -ForegroundColor Green
    Write-Host "API     : $apiBaseUrl" -ForegroundColor Green
    Write-Host "Swagger : $apiBaseUrl/swagger" -ForegroundColor Green
    Write-Host "State   : $statePath"

    if (-not $NoBrowser) {
        Start-Process $webUrl | Out-Null
    }
}
catch {
    $message = $_.Exception.Message

    if ($null -ne (Get-Variable -Name dockerExe -Scope Local -ErrorAction SilentlyContinue)) {
        try {
            Save-ComposeDiagnostics -DockerExe $dockerExe -Reason $message
            Write-Host "Diagnostics: $diagnosticPath" -ForegroundColor Yellow
        }
        catch {
        }
    }

    Write-Host ""
    Write-Host "DIP startup failed: $message" -ForegroundColor Red
    throw
}
finally {
    Pop-Location
}
