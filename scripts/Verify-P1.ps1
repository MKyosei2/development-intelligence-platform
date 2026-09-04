[CmdletBinding()]
param([switch]$SkipBugZoo)

$ErrorActionPreference="Stop"
$root=(Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$out=Join-Path $root "artifacts/p1-verification";New-Item -ItemType Directory -Force -Path $out|Out-Null
Push-Location $root
try{
 dotnet restore DevelopmentIntelligencePlatform.sln
 if($LASTEXITCODE -ne 0){throw "dotnet restore failed"}
 dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
 if($LASTEXITCODE -ne 0){throw "dotnet build failed"}
 @("tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj","tests/Dip.Application.Tests/Dip.Application.Tests.csproj","tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj")|ForEach-Object{dotnet test $_ -c Release --no-build;if($LASTEXITCODE -ne 0){throw "test failed: $_"}}
 & (Join-Path $PSScriptRoot "Build-P1-NativeTools.ps1") -Configuration Release
 if($LASTEXITCODE -ne 0){throw "P1 native tool verification failed"}
 if($env:OS -eq "Windows_NT" -and [Environment]::UserInteractive){& (Join-Path $PSScriptRoot "Test-P1-Capture.ps1");if($LASTEXITCODE -ne 0){throw "P1 capture smoke failed"}}
 if(-not $SkipBugZoo){& (Join-Path $PSScriptRoot "Invoke-QaBugZoo.ps1");if($LASTEXITCODE -ne 0){throw "P1 QA Bug Zoo failed"}}
 $stamp=Get-Date -Format o
 @("P1 LOCAL VERIFICATION PASSED","Timestamp: $stamp","Note: Live E2E still requires API/Worker/PostgreSQL and should be run separately.")|Set-Content -Path (Join-Path $out "P1_LOCAL_VERIFICATION.txt") -Encoding UTF8
 Write-Host "P1 local verification passed." -ForegroundColor Green
}finally{Pop-Location}
