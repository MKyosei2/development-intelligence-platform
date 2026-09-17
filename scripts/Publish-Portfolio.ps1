# Purpose: Verifies publication gates and optionally makes the portfolio repository public.
[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$MakePublic,
    [string]$Repository = "MKyosei2/development-intelligence-platform"
)
$ErrorActionPreference = "Stop"
function Require([string]$Name) { if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) { throw "$Name is required." } }
Require git; Require gh; Require python
if ((git rev-parse --abbrev-ref HEAD).Trim() -ne "main") { throw "Publication must run from main." }
if (git status --porcelain) { throw "Worktree must be clean before publication." }
python tools/verify_publication_safety.py --root . --output artifacts/publication-safety/publication-safety.json
if ($LASTEXITCODE -ne 0) { throw "Publication safety check failed." }
python tools/validate_portfolio_readiness.py --root . --output artifacts/portfolio-readiness/static.json
if ($LASTEXITCODE -ne 0) { throw "Portfolio readiness check failed." }
$sha=(git rev-parse HEAD).Trim()
$required=@("Commit Message",".NET CI","Python CI","Native Collector CI","Integration and E2E Tests","GUI E2E","Native Package Build","Security Scan","Universal QA Benchmark","Unreal Collector CI","Portfolio Publication Readiness")
$runs=gh run list --repo $Repository --commit $sha --limit 100 --json name,status,conclusion,url | ConvertFrom-Json
foreach($name in $required){
    $ok=$runs | Where-Object { $_.name -eq $name -and $_.status -eq "completed" -and $_.conclusion -eq "success" } | Select-Object -First 1
    if(-not $ok){ throw "Required exact-SHA workflow is not green: $name ($sha)" }
}
Write-Host "Exact-revision CI gate: PASS ($sha)"
if($MakePublic){
    if($PSCmdlet.ShouldProcess($Repository,"change repository visibility to public")){
        gh repo edit $Repository --visibility public --accept-visibility-change-consequences
        if($LASTEXITCODE -ne 0){ throw "GitHub visibility update failed." }
        Write-Host "Repository visibility changed to public after all gates passed."
    }
}else{ Write-Host "Dry gate complete. Add -MakePublic to perform the visibility change." }
