$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $Root

if (-not (Test-Path "DevelopmentIntelligencePlatform.sln")) {
    throw "Repository root could not be resolved."
}

# Remove only confirmed zero-byte Razor stubs. Non-empty files are never touched.
$EmptyRazor = @(
    Get-ChildItem "src\Dip.Web" -Recurse -Filter "*.razor" -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Length -eq 0 }
)

foreach ($File in $EmptyRazor) {
    $Relative = $File.FullName.Substring($Root.Length + 1)
    Remove-Item -LiteralPath $File.FullName -Force
    Write-Host "[DELETE zero-byte Razor] $Relative" -ForegroundColor Green
}

Write-Host "Removed zero-byte Razor files: $($EmptyRazor.Count)" -ForegroundColor Cyan
