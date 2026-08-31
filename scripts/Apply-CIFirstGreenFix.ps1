$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $RepoRoot

if (-not (Test-Path "DevelopmentIntelligencePlatform.sln")) {
    throw "Repository root could not be resolved."
}

$EmptyRazor = @(
    Get-ChildItem "src\Dip.Web" -Recurse -Filter "*.razor" -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Length -eq 0 }
)

foreach ($File in $EmptyRazor) {
    $Relative = $File.FullName.Substring($RepoRoot.Length + 1)
    Remove-Item -LiteralPath $File.FullName -Force
    Write-Host "[DELETE zero-byte Razor] $Relative" -ForegroundColor Green
}

Write-Host "Removed zero-byte Razor files: $($EmptyRazor.Count)" -ForegroundColor Cyan

python tools/verify_publication_safety.py --root . --output artifacts/publication-safety/publication-safety.json
if ($LASTEXITCODE -ne 0) {
    Get-Content artifacts/publication-safety/publication-safety.json -Raw
    throw "Publication Safety failed. Unknown literal credentials are intentionally not suppressed."
}

python tools/validate_portfolio_readiness.py --root . --output artifacts/portfolio-readiness/static.json
if ($LASTEXITCODE -ne 0) {
    Get-Content artifacts/portfolio-readiness/static.json -Raw
    throw "Portfolio Static Readiness failed."
}

dotnet restore DevelopmentIntelligencePlatform.sln
if ($LASTEXITCODE -ne 0) { throw ".NET restore failed." }

dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw ".NET Release build failed after cleanup." }

dotnet test tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Domain tests failed." }

dotnet test tests/Dip.Application.Tests/Dip.Application.Tests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Application tests failed." }

dotnet test tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Infrastructure tests failed." }

Write-Host "PASS: Publication Safety + Portfolio Readiness + .NET regression guard" -ForegroundColor Green
