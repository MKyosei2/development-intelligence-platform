# Purpose: Builds native runtime tools used by capture, crash, and hardware verification workflows.
[CmdletBinding()]
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$tools = @('capture-agent', 'crash-watchdog', 'hardware-probe', 'hardware-stress-agent')
$isWindowsHost = $env:OS -eq 'Windows_NT'

if (-not $isWindowsHost) {
    Write-Host 'Native runtime tools are Windows-oriented; non-Windows hosts perform configure-only validation.'
}

foreach ($tool in $tools) {
    $source = Join-Path $root "collectors/$tool"
    $build = Join-Path $source 'build'
    Write-Host "=== $tool ==="

    $configureArgs = @('-S', $source, '-B', $build)
    if ($isWindowsHost) {
        $configureArgs += @('-A', 'x64')
    }

    & cmake @configureArgs
    if ($LASTEXITCODE -ne 0) {
        throw "CMake configure failed: $tool"
    }

    if ($isWindowsHost) {
        cmake --build $build --config $Configuration --parallel
        if ($LASTEXITCODE -ne 0) {
            throw "CMake build failed: $tool"
        }
    }
}
