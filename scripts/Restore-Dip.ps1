[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$BackupDirectory,[string]$ComposeProject='dip',[string]$Database='dip',[string]$DatabaseUser='dip',[switch]$ConfirmRestore)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
if(-not$ConfirmRestore){throw 'Restore is destructive. Re-run with -ConfirmRestore after verifying the target compose project.'}
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path;$backup=(Resolve-Path $BackupDirectory).Path;foreach($c in 'docker'){if(-not(Get-Command $c -ErrorAction SilentlyContinue)){throw "$c is required."}}
$dbDump=Join-Path $backup 'database.dump';$artifactArchive=Join-Path $backup 'artifacts.tar.gz';$manifestPath=Join-Path $backup 'backup-manifest.json';foreach($f in $dbDump,$artifactArchive,$manifestPath){if(-not(Test-Path $f)){throw "Backup component missing: $f"}}
$manifest=Get-Content $manifestPath -Raw|ConvertFrom-Json;if((Get-FileHash $dbDump -Algorithm SHA256).Hash-ne$manifest.databaseSha256){throw 'Database dump hash does not match backup manifest.'};if((Get-FileHash $artifactArchive -Algorithm SHA256).Hash-ne$manifest.artifactsSha256){throw 'Artifact archive hash does not match backup manifest.'}
Push-Location $root
try{
 $pg=(& docker compose -p $ComposeProject ps -q postgres|Out-String).Trim();if(-not$pg){throw "Postgres service for compose project '$ComposeProject' is not running."};$api=(& docker compose -p $ComposeProject ps -q api|Out-String).Trim();if(-not$api){throw "API service for compose project '$ComposeProject' is not running."}
 $volume=(& docker inspect $api --format '{{range .Mounts}}{{if eq .Destination "/data/dip-artifacts"}}{{.Name}}{{end}}{{end}}'|Out-String).Trim();if(-not$volume){throw 'Could not resolve the Local artifact Docker volume.'}
 & docker compose -p $ComposeProject stop api worker web *> $null
 & docker cp $dbDump "${pg}:/tmp/dip-restore.dump";if($LASTEXITCODE-ne0){throw 'Could not copy database dump into PostgreSQL container.'}
 & docker exec $pg psql -U $DatabaseUser -d postgres -v ON_ERROR_STOP=1 -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$Database' AND pid<>pg_backend_pid();" *> $null
 & docker exec $pg psql -U $DatabaseUser -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS \"$Database\";" *> $null
 & docker exec $pg psql -U $DatabaseUser -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE \"$Database\" OWNER \"$DatabaseUser\";" *> $null
 & docker exec $pg pg_restore -U $DatabaseUser -d $Database --clean --if-exists /tmp/dip-restore.dump;if($LASTEXITCODE-ne0){throw 'Database restore failed.'}
 $tmp="dip-restore-$([Guid]::NewGuid().ToString('N'))";& docker create --name $tmp -v "${volume}:/target" alpine:3.22 sh -c 'rm -rf /target/* /target/.[!.]* /target/..?* 2>/dev/null || true; tar -xzf /tmp/artifacts.tar.gz -C /target' *> $null;try{& docker cp $artifactArchive "${tmp}:/tmp/artifacts.tar.gz";if($LASTEXITCODE-ne0){throw 'Could not copy artifact archive into restore container.'};& docker start -a $tmp;if($LASTEXITCODE-ne0){throw 'Artifact restore failed.'}}finally{& docker rm -f $tmp *> $null}
 & docker compose -p $ComposeProject start api worker web *> $null;Write-Host "Restore completed from: $backup"
}finally{Pop-Location}
