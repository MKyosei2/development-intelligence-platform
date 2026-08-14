[CmdletBinding()]
param()
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
foreach($c in 'docker','git'){if(-not(Get-Command $c -ErrorAction SilentlyContinue)){throw "$c is required."}}
function Get-FreePort{$l=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$l.Start();$p=([Net.IPEndPoint]$l.LocalEndpoint).Port;$l.Stop();$p}
$stamp=Get-Date -Format 'yyyyMMddHHmmss';$compose="dip-dr-$stamp";$pgPort=Get-FreePort;$out=Join-Path $root 'artifacts\dr-verification';New-Item -Force -ItemType Directory $out|Out-Null;Remove-Item (Join-Path $out 'DR_VERIFICATION_PASS.txt') -Force -ErrorAction SilentlyContinue
$vars='POSTGRES_PORT','POSTGRES_DB','POSTGRES_USER','POSTGRES_PASSWORD';$old=@{};foreach($n in $vars){$old[$n]=[Environment]::GetEnvironmentVariable($n,'Process')}
try{
 $env:POSTGRES_PORT="$pgPort";$env:POSTGRES_DB='dipdr';$env:POSTGRES_USER='dip';$env:POSTGRES_PASSWORD='dip-dr-password';Push-Location $root;try{
  & docker compose -p $compose up -d postgres;if($LASTEXITCODE-ne0){throw 'Postgres start failed'};$cid=(& docker compose -p $compose ps -q postgres|Out-String).Trim();if(-not$cid){throw 'Postgres container missing.'}
  $ready=$false;for($i=0;$i-lt60;$i++){& docker exec $cid pg_isready -U dip -d dipdr *> $null;if($LASTEXITCODE-eq0){$ready=$true;break};Start-Sleep -Milliseconds 500};if(-not$ready){throw 'Postgres did not become ready.'}
  & docker exec $cid psql -U dip -d dipdr -v ON_ERROR_STOP=1 -c "CREATE TABLE dr_probe(id int primary key,value text); INSERT INTO dr_probe VALUES(1,'DIP-DR-PROBE');" *> $null
  & docker exec $cid sh -c "pg_dump -U dip -d dipdr -Fc -f /tmp/dip-dr.dump";if($LASTEXITCODE-ne0){throw 'pg_dump failed'};$dump=Join-Path $out 'dip-dr.dump';& docker cp "${cid}:/tmp/dip-dr.dump" $dump *> $null
  $artifactDir=Join-Path $out 'source-artifacts';$restoreDir=Join-Path $out 'restored-artifacts';Remove-Item $artifactDir,$restoreDir -Recurse -Force -ErrorAction SilentlyContinue;New-Item -Force -ItemType Directory $artifactDir,$restoreDir|Out-Null;'artifact-proof'|Set-Content (Join-Path $artifactDir 'proof.txt') -Encoding UTF8;$sourceHash=(Get-FileHash (Join-Path $artifactDir 'proof.txt') -Algorithm SHA256).Hash
  Compress-Archive -Path (Join-Path $artifactDir '*') -DestinationPath (Join-Path $out 'artifacts.zip') -Force
  & docker exec $cid psql -U dip -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE dipdr;" *> $null;if($LASTEXITCODE-ne0){throw 'Drop restore target failed.'}
  & docker exec $cid psql -U dip -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE dipdr OWNER dip;" *> $null;if($LASTEXITCODE-ne0){throw 'Create restore target failed.'}
  & docker cp $dump "${cid}:/tmp/dip-dr.dump" *> $null;& docker exec $cid pg_restore -U dip -d dipdr --clean --if-exists /tmp/dip-dr.dump;if($LASTEXITCODE-ne0){throw 'pg_restore failed'}
  $value=(& docker exec $cid psql -U dip -d dipdr -Atc 'SELECT value FROM dr_probe WHERE id=1;'|Out-String).Trim();if($value-ne'DIP-DR-PROBE'){throw 'Database restore verification mismatch.'}
  Expand-Archive (Join-Path $out 'artifacts.zip') -DestinationPath $restoreDir -Force;$restoredHash=(Get-FileHash (Join-Path $restoreDir 'proof.txt') -Algorithm SHA256).Hash;if($sourceHash-ne$restoredHash){throw 'Artifact restore hash mismatch.'}
 }finally{Pop-Location}
 $sha=(& git -C $root rev-parse HEAD|Out-String).Trim();$manifest=[ordered]@{schema='dip-dr-verification-v2';sourceCommit=$sha;verifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');postgresPort=$pgPort;databaseRestore='PASS';artifactRestore='PASS';artifactSha256=$sourceHash;dumpSha256=(Get-FileHash $dump -Algorithm SHA256).Hash};$manifest|ConvertTo-Json|Set-Content (Join-Path $out 'dr-verification.json') -Encoding UTF8;@('DR VERIFICATION PASS',"Commit=$sha",'DatabaseRestore=PASS','ArtifactRestore=PASS')|Set-Content (Join-Path $out 'DR_VERIFICATION_PASS.txt') -Encoding UTF8
}finally{Push-Location $root;try{& docker compose -p $compose down -v --remove-orphans *> $null}catch{}finally{Pop-Location};foreach($n in $vars){[Environment]::SetEnvironmentVariable($n,$old[$n],'Process')}}
