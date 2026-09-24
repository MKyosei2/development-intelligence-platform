# Purpose: Automates V er if y G ui Ph as e1 St at ic for repository development and verification workflows.
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$required = @(
    "src/Dip.Web/State/ProjectState.cs",
    "src/Dip.Infrastructure/GitHub/GitHubRepositoryCatalog.cs",
    "src/Dip.Api/Controllers/GitHubConnectionController.cs",
    "src/Dip.Api/Controllers/GitHubRepositoryController.cs",
    "src/Dip.Api/Controllers/GitHubWorkspaceController.cs",
    "src/Dip.Api/Controllers/GitHubPullRequestController.cs",
    "src/Dip.Api/Hubs/ProjectEventsHub.cs",
    "src/Dip.Web/Pages/Projects/GitHubImport.razor",
    "src/Dip.Web/Pages/Dashboard/ProjectDashboard.razor",
    "src/Dip.Web/Pages/Dashboard/Repository.razor",
    "src/Dip.Web/Pages/Dashboard/Ci.razor",
    "src/Dip.Web/Pages/Dashboard/Qa.razor",
    "src/Dip.Web/Components/Common/LoadingState.razor",
    "src/Dip.Web/Components/Common/ErrorState.razor",
    "src/Dip.Web/Components/Common/EmptyState.razor",
    "src/Dip.Web/Services/RealtimeUpdateService.cs",
    "tests/Dip.UiE2E/playwright.config.mjs",
    "tests/Dip.UiE2E/tests/gui-onboarding.spec.mjs",
    ".github/workflows/gui-e2e.yml"
)

foreach ($relative in $required) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing required GUI Phase 1 file: $relative" }
    if ((Get-Item -LiteralPath $path).Length -eq 0) { throw "GUI Phase 1 file is zero bytes: $relative" }
}

$webRoot = Join-Path $root "src/Dip.Web"
$emptyWeb = Get-ChildItem $webRoot -File -Recurse | Where-Object {
  $_.Length -eq 0 -and
  $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
  $_.Name -ne '.gitkeep'
}
if ($emptyWeb) { throw "Zero-byte Dip.Web source remains: $($emptyWeb.FullName -join ', ')" }

Get-Content (Join-Path $root "src/Dip.Api/appsettings.json") -Raw | ConvertFrom-Json | Out-Null
Get-Content (Join-Path $root "src/Dip.Web/appsettings.json") -Raw | ConvertFrom-Json | Out-Null
Get-Content (Join-Path $root "tests/Dip.UiE2E/package.json") -Raw | ConvertFrom-Json | Out-Null

if (Get-Command node -ErrorAction SilentlyContinue) {
    node --check (Join-Path $root "tests/Dip.UiE2E/playwright.config.mjs")
    if ($LASTEXITCODE -ne 0) { throw "Playwright config syntax validation failed." }
    node --check (Join-Path $root "tests/Dip.UiE2E/tests/gui-onboarding.spec.mjs")
    if ($LASTEXITCODE -ne 0) { throw "Playwright onboarding test syntax validation failed." }
}

Write-Host "GUI Phase 1 static validation passed." -ForegroundColor Green
Write-Host "Run scripts/Verify-GuiPhase1.ps1 with native PostgreSQL + Node to prove runtime E2E." -ForegroundColor Yellow
