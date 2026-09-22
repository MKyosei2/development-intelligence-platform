# Purpose: Automates a repeatable development or verification workflow for this repository.
$ErrorActionPreference="Stop"
if($env:OS -ne "Windows_NT"){Write-Host "SKIP: capture smoke requires interactive Windows.";exit 0}
$root=(Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$exe=Join-Path $root "collectors/capture-agent/build/Release/dip-capture-agent.exe"
if(-not(Test-Path $exe)){throw "Capture agent not built. Run Build-P1-NativeTools.ps1 first."}
$out=Join-Path $root "artifacts/p1-verification/capture";New-Item -ItemType Directory -Force -Path $out|Out-Null
$video=Join-Path $out "capture-smoke.mp4";$audio=Join-Path $out "capture-smoke.wav";$stop=Join-Path $out "capture.stop";Remove-Item $video,$audio,$stop -Force -ErrorAction SilentlyContinue
$p=Start-Process -FilePath $exe -ArgumentList @("--video",$video,"--audio",$audio,"--stop-file",$stop,"--max-seconds","10") -PassThru -NoNewWindow
Start-Sleep -Seconds 3;Set-Content -Path $stop -Value stop -Encoding ascii
if(-not $p.WaitForExit(10000)){Stop-Process -Id $p.Id -Force;throw "Capture agent did not finalize."}
if(-not(Test-Path $video) -or (Get-Item $video).Length -lt 1024){throw "MP4 evidence was not created."}
if(-not(Test-Path $audio) -or (Get-Item $audio).Length -lt 44){throw "WAV evidence was not created."}
Write-Host "PASS: P1 capture evidence created." -ForegroundColor Green
