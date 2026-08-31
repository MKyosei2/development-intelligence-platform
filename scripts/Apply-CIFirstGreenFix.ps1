$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$DeleteList = Join-Path $Root "PATCH_DELETE_LIST.txt"

if (-not (Test-Path (Join-Path $Root "DevelopmentIntelligencePlatform.sln"))) {
    throw "This patch must live under the development-intelligence-platform repository root."
}

if (-not (Test-Path $DeleteList)) {
    throw "PATCH_DELETE_LIST.txt was not found."
}

Write-Host "DIP CI first-green patch" -ForegroundColor Cyan
Write-Host "Repository: $Root"
Write-Host ""

$Targets = Get-Content $DeleteList |
    Where-Object {
        $_ -and -not $_.TrimStart().StartsWith("#")
    }

$Preserved = @()
$Deleted = @()

foreach ($Relative in $Targets) {
    $Path = Join-Path $Root $Relative

    if (-not (Test-Path $Path)) {
        continue
    }

    $Item = Get-Item $Path

    if ($Item.Length -ne 0) {
        $Preserved += $Relative
        Write-Host "[PRESERVE] non-empty file: $Relative" -ForegroundColor Yellow
        continue
    }

    Remove-Item $Path -Force
    $Deleted += $Relative
    Write-Host "[DELETE] $Relative" -ForegroundColor Green
}

Write-Host ""
Write-Host "Deleted zero-byte legacy stubs: $($Deleted.Count)" -ForegroundColor Cyan

if ($Preserved.Count -gt 0) {
    Write-Host ""
    Write-Host "Non-empty files were deliberately preserved:" -ForegroundColor Yellow
    $Preserved | ForEach-Object { Write-Host "  $_" }
}

Push-Location $Root
try {
    Write-Host ""
    Write-Host "=== Publication Safety ===" -ForegroundColor Cyan
    python tools/verify_publication_safety.py `
        --root . `
        --output artifacts/publication-safety/publication-safety.json
    if ($LASTEXITCODE -ne 0) {
        throw "Publication Safety still fails. Review the emitted findings; unknown literal credentials are intentionally not suppressed."
    }

    Write-Host ""
    Write-Host "=== Portfolio Static Readiness ===" -ForegroundColor Cyan
    python tools/validate_portfolio_readiness.py `
        --root . `
        --output artifacts/portfolio-readiness/static.json
    if ($LASTEXITCODE -ne 0) {
        throw "Portfolio Static Readiness still fails."
    }

    Write-Host ""
    Write-Host "=== .NET Restore ===" -ForegroundColor Cyan
    dotnet restore DevelopmentIntelligencePlatform.sln
    if ($LASTEXITCODE -ne 0) {
        throw ".NET restore failed."
    }

    Write-Host ""
    Write-Host "=== .NET Release Build ===" -ForegroundColor Cyan
    dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw ".NET Release Build failed. Preserve the existing local source fixes and inspect the build output before committing."
    }

    Write-Host ""
    Write-Host "=== Unit Tests ===" -ForegroundColor Cyan
    dotnet test tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Domain tests failed." }

    dotnet test tests/Dip.Application.Tests/Dip.Application.Tests.csproj -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Application tests failed." }

    dotnet test tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Infrastructure tests failed." }

    Write-Host ""
    Write-Host "PASS: static publication checks, Release build, and .NET unit tests." -ForegroundColor Green
}
finally {
    Pop-Location
}
