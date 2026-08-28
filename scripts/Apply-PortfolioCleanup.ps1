[CmdletBinding(SupportsShouldProcess)]
param([string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = "Stop"
$pages = Join-Path $RepositoryRoot "src\Dip.Web\Pages"
if (-not (Test-Path -LiteralPath $pages)) { throw "Dip.Web Pages directory was not found: $pages" }
$stale = @(Get-ChildItem -LiteralPath $pages -Recurse -File -Filter *.razor | Where-Object { $_.Length -eq 0 })
foreach ($file in $stale) {
    if ($PSCmdlet.ShouldProcess($file.FullName, "remove obsolete zero-byte Razor page")) {
        Remove-Item -LiteralPath $file.FullName -Force
    }
}
Write-Host "Removed $($stale.Count) zero-byte Razor page(s)."
python (Join-Path $RepositoryRoot "tools\validate_portfolio_readiness.py") --root $RepositoryRoot
if ($LASTEXITCODE -ne 0) { throw "Portfolio readiness validation failed after cleanup." }
