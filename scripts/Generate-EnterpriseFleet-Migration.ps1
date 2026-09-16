# Purpose: Automates the G en er at e-E nt er pr is eF le et-M ig ra ti on repository workflow.
[CmdletBinding()]
param([string]$MigrationName='AddDifficulty5EnterpriseFoundation')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repo
try {
  Write-Host 'DIP Phase 5 migration generation' -ForegroundColor Cyan
  $phase1=Get-ChildItem 'src\Dip.Infrastructure\Persistence\Migrations' -Filter '*_AddDifficulty1Foundation.cs' -ErrorAction SilentlyContinue
  if(-not $phase1){throw 'Phase 1 migration is missing.'}
  $existing=Get-ChildItem 'src\Dip.Infrastructure\Persistence\Migrations' -Filter "*_$MigrationName.cs" -ErrorAction SilentlyContinue
  if(-not $existing){
    if(-not $env:ConnectionStrings__PostgreSql){$env:ConnectionStrings__PostgreSql='Host=127.0.0.1;Port=5432;Database=dip;Username=dip;Password=change-me-local-only'}
    dotnet tool restore; if($LASTEXITCODE -ne 0){throw 'dotnet tool restore failed.'}
    dotnet ef migrations add $MigrationName --project 'src\Dip.Infrastructure\Dip.Infrastructure.csproj' --startup-project 'src\Dip.Api\Dip.Api.csproj' --context DipDbContext --output-dir 'Persistence\Migrations'
    if($LASTEXITCODE -ne 0){throw 'dotnet ef migrations add failed.'}
  }
  dotnet ef migrations has-pending-model-changes --project 'src\Dip.Infrastructure\Dip.Infrastructure.csproj' --startup-project 'src\Dip.Api\Dip.Api.csproj' --context DipDbContext
  if($LASTEXITCODE -ne 0){throw 'Phase 5 still has pending EF model changes.'}
  Write-Host '[PASS] Phase 5 migration/model snapshot ready.' -ForegroundColor Green
} finally {Pop-Location}
