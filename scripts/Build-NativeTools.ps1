# Purpose: Automates a repeatable development or verification workflow for this repository.
param([string]$Configuration="Release")
$ErrorActionPreference="Stop"
$root=(Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$tools=@("capture-agent","crash-watchdog","hardware-probe","hardware-stress-agent")
if(-not $IsWindows -and $PSVersionTable.PSEdition -eq "Core"){Write-Host "P1 native runtime tools are Windows-only; configure-only validation can still be performed."}
foreach($tool in $tools){
 $source=Join-Path $root "collectors/$tool";$build=Join-Path $source "build"
 Write-Host "=== $tool ==="
 cmake -S $source -B $build -A x64
 if($LASTEXITCODE -ne 0){throw "CMake configure failed: $tool"}
 if($env:OS -eq "Windows_NT"){
  cmake --build $build --config $Configuration --parallel
  if($LASTEXITCODE -ne 0){throw "CMake build failed: $tool"}
 }
}
