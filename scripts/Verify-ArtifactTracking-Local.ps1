# Purpose: Verifies A rt if ac tT ra ck in g L oc al requirements for DIP.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repo
try {
    Write-Host 'DIP Phase 2 local verification' -ForegroundColor Cyan

    $phase2Migration = Get-ChildItem 'src\Dip.Infrastructure\Persistence\Migrations' `
        -Filter '*_AddDifficulty2LocalArtifacts.cs' -ErrorAction SilentlyContinue
    if (-not $phase2Migration) {
        throw 'Phase 2 migration is missing. Run scripts\Generate-ArtifactTracking-Migration.ps1 first.'
    }

    dotnet restore DevelopmentIntelligencePlatform.sln
    if ($LASTEXITCODE -ne 0) { throw 'solution restore failed.' }

    dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'server solution Release build failed.' }

    dotnet restore 'src\Dip.Agent.Core\Dip.Agent.Core.csproj'
    if ($LASTEXITCODE -ne 0) { throw 'Dip.Agent.Core restore failed.' }
    dotnet build 'src\Dip.Agent.Core\Dip.Agent.Core.csproj' -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Dip.Agent.Core build failed.' }

    dotnet restore 'src\Dip.Agent.Windows\Dip.Agent.Windows.csproj'
    if ($LASTEXITCODE -ne 0) { throw 'Dip.Agent.Windows restore failed.' }
    dotnet build 'src\Dip.Agent.Windows\Dip.Agent.Windows.csproj' -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Dip.Agent.Windows build failed.' }

    dotnet test 'tests\Dip.Application.Tests\Dip.Application.Tests.csproj' -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Application tests failed.' }

    dotnet restore 'tests\Dip.Agent.Core.Tests\Dip.Agent.Core.Tests.csproj'
    if ($LASTEXITCODE -ne 0) { throw 'Dip.Agent.Core.Tests restore failed.' }
    dotnet test 'tests\Dip.Agent.Core.Tests\Dip.Agent.Core.Tests.csproj' -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Agent Core tests failed.' }

    Write-Host ''
    Write-Host '[PASS] Phase 2 compile/unit verification completed.' -ForegroundColor Green
    Write-Host '[INFO] No commit, push, or GitHub Actions rerun was performed.' -ForegroundColor Green
}
finally { Pop-Location }
