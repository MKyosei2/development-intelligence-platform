# Purpose: Applies deterministic formatting after overlaying the full-verification source-fix package.
[CmdletBinding()]
param()

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
    if(-not(Get-Command dotnet -ErrorAction SilentlyContinue)){throw 'dotnet is required.'}

    dotnet restore .\DevelopmentIntelligencePlatform.sln
    if($LASTEXITCODE -ne 0){throw "dotnet restore failed: $LASTEXITCODE"}

    dotnet format .\DevelopmentIntelligencePlatform.sln --no-restore
    if($LASTEXITCODE -ne 0){throw "dotnet format failed: $LASTEXITCODE"}

    dotnet format .\DevelopmentIntelligencePlatform.sln --verify-no-changes --no-restore
    if($LASTEXITCODE -ne 0){throw "dotnet format verification failed: $LASTEXITCODE"}

    dotnet build .\DevelopmentIntelligencePlatform.sln -c Release --no-restore
    if($LASTEXITCODE -ne 0){throw "Release build failed: $LASTEXITCODE"}

    dotnet test .\tests\Dip.Infrastructure.Tests\Dip.Infrastructure.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~VerifiedFix'
    if($LASTEXITCODE -ne 0){throw "Verified Fix safety tests failed: $LASTEXITCODE"}

    & (Join-Path $PSScriptRoot 'Verify-SourceReadability.ps1') -Strict
    & (Join-Path $PSScriptRoot 'Verify-ProjectControl.ps1')
    & (Join-Path $PSScriptRoot 'Verify-TechnicalIntelligence.ps1') -SkipNative -SkipLiveE2E

    Write-Host 'Overlay formatting and static verification completed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
