[CmdletBinding()]
param(
    [string]$RepoPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$NoBackendStart,
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoPath = [System.IO.Path]::GetFullPath($RepoPath)
$desktopProject = Join-Path $RepoPath 'src\Dip.Desktop\Dip.Desktop.csproj'
$startDip = Join-Path $RepoPath 'scripts\Start-DIP.ps1'

if (-not (Test-Path -LiteralPath $desktopProject -PathType Leaf)) {
    throw "Desktop project not found: $desktopProject"
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK was not found in PATH.'
}

function Test-UrlReady([string]$Url) {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 3
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 400
    }
    catch { return $false }
}

Write-Host '=== DIP DESKTOP START ===' -ForegroundColor Cyan
Write-Host "Repository: $RepoPath"

if (-not $NoBackendStart) {
    $apiReady = Test-UrlReady 'http://127.0.0.1:5080/health'
    $capabilityReady = Test-UrlReady 'http://127.0.0.1:5080/api/v1/capabilities'

    if ($apiReady -and -not $capabilityReady) {
        Write-Host '[INFO] An older DIP backend is running. Restarting it so the native-GUI capability endpoint matches the current source...' -ForegroundColor Cyan
        & (Join-Path $RepoPath 'scripts\Stop-DIP.ps1') -KeepData
        $apiReady = $false
    }

    if (-not $apiReady) {
        Write-Host '[INFO] Starting the existing native runtime...' -ForegroundColor Cyan
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $startDip -NoBrowser
        if ($LASTEXITCODE -ne 0) { throw "Start-DIP.ps1 failed with exit code $LASTEXITCODE" }
    }
}

if (-not (Test-UrlReady 'http://127.0.0.1:5080/health')) {
    throw 'DIP API is not ready at http://127.0.0.1:5080/health.'
}
if (-not (Test-UrlReady 'http://127.0.0.1:5080/api/v1/capabilities')) {
    throw 'The running DIP API does not expose /api/v1/capabilities. Restart the backend from the patched source.'
}

if (-not $NoBuild) {
    Write-Host '[INFO] Building the Windows desktop shell...' -ForegroundColor Cyan
    & dotnet build $desktopProject -c Release
    if ($LASTEXITCODE -ne 0) { throw "Desktop build failed with exit code $LASTEXITCODE" }
}

$exe = Join-Path $RepoPath 'src\Dip.Desktop\bin\Release\net8.0-windows\DevelopmentIntelligencePlatform.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "Desktop executable was not found: $exe"
}

Write-Host '[PASS] Starting DevelopmentIntelligencePlatform.exe' -ForegroundColor Green
Start-Process -FilePath $exe -WorkingDirectory $RepoPath | Out-Null
Write-Host 'DIP_DESKTOP_READY=YES' -ForegroundColor Green
