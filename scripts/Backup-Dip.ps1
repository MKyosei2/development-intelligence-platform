[CmdletBinding()]
param([string]$ComposeProject='dip',[string]$OutputDirectory='artifacts\backups',[string]$Database='dip',[string]$DatabaseUser='dip')
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path;foreach($c in 'docker','git'){if(-not(Get-Command $c -ErrorAction SilentlyContinue)){throw "$c is required."}}
$out=Join-Path $root $OutputDirectory;$stamp=(Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ');$dir=Join-Path $out $stamp;New-Item -ItemType Directory -Force $dir|Out-Null
Push-Location $root
try{
 $pg=(& docker compose -p $ComposeProject ps -q postgres|Out-String).Trim();if(-not$pg){throw "Postgres service for compose project '$ComposeProject' is not running."}
 $api=(& docker compose -p $ComposeProject ps -q api|Out-String).Trim();if(-not$api){throw "API service for compose project '$ComposeProject' is not running."}
 & docker exec $pg sh -c "pg_dump -U '$DatabaseUser' -d '$Database' -Fc -f /tmp/dip-backup.dump";if($LASTEXITCODE-ne0){throw 'pg_dump failed.'}
 $dbDump=Join-Path $dir 'database.dump';& docker cp "${pg}:/tmp/dip-backup.dump" $dbDump;if($LASTEXITCODE-ne0){throw 'Could not copy database dump.'}
 $volume=(& docker inspect $api --format '{{range .Mounts}}{{if eq .Destination "/data/dip-artifacts"}}{{.Name}}{{end}}{{end}}'|Out-String).Trim();if(-not$volume){throw 'Could not resolve the Local artifact Docker volume.'}
 $tmp="dip-backup-$([Guid]::NewGuid().ToString('N'))";& docker create --name $tmp -v "${volume}:/source:ro" alpine:3.22 sh -c 'tar -czf /tmp/artifacts.tar.gz -C /source .' *> $null;try{& docker start -a $tmp;if($LASTEXITCODE-ne0){throw 'Artifact archive failed.'};& docker cp "${tmp}:/tmp/artifacts.tar.gz" (Join-Path $dir 'artifacts.tar.gz');if($LASTEXITCODE-ne0){throw 'Could not copy artifact archive.'}}finally{& docker rm -f $tmp *> $null}
 $sha=(& git -C $root rev-parse HEAD|Out-String).Trim();$manifest=[ordered]@{schema='dip-backup-v1';createdAtUtc=[DateTimeOffset]::UtcNow.ToString('o');sourceCommit=$sha;composeProject=$ComposeProject;database=$Database;artifactVolume=$volume;databaseSha256=(Get-FileHash $dbDump -Algorithm SHA256).Hash;artifactsSha256=(Get-FileHash (Join-Path $dir 'artifacts.tar.gz') -Algorithm SHA256).Hash};$manifest|ConvertTo-Json|Set-Content (Join-Path $dir 'backup-manifest.json') -Encoding UTF8
 Write-Host "Backup created: $dir"
}finally{Pop-Location}
