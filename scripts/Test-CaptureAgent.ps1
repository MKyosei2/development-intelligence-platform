# Purpose: Runs an interactive Windows smoke test for the capture agent and validates emitted media evidence.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($env:OS -ne 'Windows_NT') {
    Write-Host 'SKIP: capture smoke requires interactive Windows.'
    exit 0
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $root 'collectors/capture-agent/build/Release/dip-capture-agent.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw 'Capture agent not built. Run Build-NativeTools.ps1 first.'
}

$out = Join-Path $root 'artifacts/game-qa-verification/capture'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$video = Join-Path $out 'capture-smoke.mp4'
$audio = Join-Path $out 'capture-smoke.wav'
$stop = Join-Path $out 'capture.stop'
Remove-Item $video, $audio, $stop -Force -ErrorAction SilentlyContinue

$startArgs = @{
    FilePath = $exe
    ArgumentList = @('--video', $video, '--audio', $audio, '--stop-file', $stop, '--max-seconds', '10')
    PassThru = $true
    NoNewWindow = $true
}
$process = Start-Process @startArgs

Start-Sleep -Seconds 3
Set-Content -Path $stop -Value stop -Encoding ascii
if (-not $process.WaitForExit(10000)) {
    Stop-Process -Id $process.Id -Force
    throw 'Capture agent did not finalize.'
}
if (-not (Test-Path -LiteralPath $video) -or (Get-Item $video).Length -lt 1024) {
    throw 'MP4 evidence was not created.'
}
if (-not (Test-Path -LiteralPath $audio) -or (Get-Item $audio).Length -lt 44) {
    throw 'WAV evidence was not created.'
}

Write-Host 'PASS: capture-agent media evidence created.' -ForegroundColor Green
