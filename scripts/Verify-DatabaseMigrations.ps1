[CmdletBinding()]
param([string]$Configuration='Release')
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
foreach($c in 'dotnet','docker','git'){if(-not(Get-Command $c -ErrorAction SilentlyContinue)){throw "$c is required."}}
function Get-FreePort{$l=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$l.Start();$p=([Net.IPEndPoint]$l.LocalEndpoint).Port;$l.Stop();$p}
$stamp=Get-Date -Format 'yyyyMMddHHmmss';$compose="dip-migration-$stamp";$db='dipmigration';$pgPort=Get-FreePort;$old=@{}
$vars='POSTGRES_PORT','POSTGRES_DB','POSTGRES_USER','POSTGRES_PASSWORD','ConnectionStrings__PostgreSql','ASPNETCORE_ENVIRONMENT','Authentication__RequireAuthenticatedUser'
foreach($n in $vars){$old[$n]=[Environment]::GetEnvironmentVariable($n,'Process')}
$out=Join-Path $root 'artifacts\migration-verification';New-Item -Force -ItemType Directory $out|Out-Null;Remove-Item (Join-Path $out 'MIGRATION_VERIFICATION_PASS.txt') -Force -ErrorAction SilentlyContinue
try{
 $env:POSTGRES_PORT="$pgPort";$env:POSTGRES_DB=$db;$env:POSTGRES_USER='dip';$env:POSTGRES_PASSWORD='dip-migration-password';$env:ASPNETCORE_ENVIRONMENT='Development';$env:Authentication__RequireAuthenticatedUser='false';$env:ConnectionStrings__PostgreSql="Host=127.0.0.1;Port=$pgPort;Database=$db;Username=dip;Password=dip-migration-password"
 Push-Location $root;try{
  & docker compose -p $compose up -d postgres;if($LASTEXITCODE-ne0){throw 'PostgreSQL start failed.'}
  $cid=(& docker compose -p $compose ps -q postgres|Out-String).Trim();if(-not$cid){throw 'Postgres container missing.'}
  $ready=$false;for($i=0;$i-lt60;$i++){& docker exec $cid pg_isready -U dip -d $db *> $null;if($LASTEXITCODE-eq0){$ready=$true;break};Start-Sleep -Milliseconds 500};if(-not$ready){throw 'PostgreSQL did not become ready.'}
  & dotnet tool restore;if($LASTEXITCODE-ne0){throw 'dotnet tool restore failed.'}
  & dotnet build DevelopmentIntelligencePlatform.sln -c $Configuration --nologo;if($LASTEXITCODE-ne0){throw 'Build failed.'}
  & dotnet ef database update --project src/Dip.Infrastructure --startup-project src/Dip.Api --configuration $Configuration;if($LASTEXITCODE-ne0){throw 'Fresh upgrade to latest failed.'}
  & dotnet ef database update 202608140006_AddQaGovernance --project src/Dip.Infrastructure --startup-project src/Dip.Api --configuration $Configuration;if($LASTEXITCODE-ne0){throw 'Rollback to previous migration failed.'}
  & dotnet ef database update --project src/Dip.Infrastructure --startup-project src/Dip.Api --configuration $Configuration;if($LASTEXITCODE-ne0){throw 'Re-upgrade failed.'}
  $tables=(& docker exec $cid psql -U dip -d $db -Atc "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('project_memberships','qa_governance_processed_runs','qa_release_gate_overrides','qa_backup_verifications');"|Out-String).Trim();if($tables-ne'4'){throw "Hardening migration tables missing ($tables/4)."}
  $activeConstraint=(& docker exec $cid psql -U dip -d $db -Atc "SELECT count(*) FROM pg_constraint WHERE conname='uq_qa_policies_single_active';"|Out-String).Trim();if($activeConstraint-ne'1'){throw 'Single-active-policy database constraint is missing.'}
 }finally{Pop-Location}
 $sha=(& git -C $root rev-parse HEAD|Out-String).Trim();$proof=[ordered]@{schema='dip-migration-verification-v2';sourceCommit=$sha;verifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');postgresPort=$pgPort;freshUpgrade='PASS';rollback='PASS';reUpgrade='PASS';hardeningTables='PASS';singleActivePolicyConstraint='PASS'};$proof|ConvertTo-Json|Set-Content (Join-Path $out 'migration-verification.json') -Encoding UTF8;@('MIGRATION VERIFICATION PASS',"Commit=$sha",'FreshUpgrade=PASS','Rollback=PASS','ReUpgrade=PASS','SingleActivePolicyConstraint=PASS')|Set-Content (Join-Path $out 'MIGRATION_VERIFICATION_PASS.txt') -Encoding UTF8
}finally{Push-Location $root;try{& docker compose -p $compose down -v --remove-orphans *> $null}catch{}finally{Pop-Location};foreach($n in $vars){[Environment]::SetEnvironmentVariable($n,$old[$n],'Process')}}
