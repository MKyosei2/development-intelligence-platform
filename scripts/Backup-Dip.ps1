[CmdletBinding()]
param(
 [string]$OutputDirectory='artifacts\backups',[string]$ArtifactDirectory='artifacts\durable',
 [string]$Database=$(if($env:POSTGRES_DB){$env:POSTGRES_DB}else{'dip'}),[string]$DatabaseUser=$(if($env:POSTGRES_USER){$env:POSTGRES_USER}else{'dip'}),
 [string]$DatabaseHost=$(if($env:POSTGRES_HOST){$env:POSTGRES_HOST}else{'127.0.0.1'}),[int]$DatabasePort=$(if($env:POSTGRES_PORT){[int]$env:POSTGRES_PORT}else{5432}),
 [string]$DatabasePassword=$(if($env:POSTGRES_PASSWORD){$env:POSTGRES_PASSWORD}else{''}),[string]$OffsiteDirectory=$(if($env:DIP_BACKUP_OFFSITE_DIRECTORY){$env:DIP_BACKUP_OFFSITE_DIRECTORY}else{''}),[switch]$RequireOffsiteMirror)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
function Need($n){if(-not(Get-Command $n -ErrorAction SilentlyContinue)){throw "$n is required."}}
function Resolve-DipPath([string]$Path,[string]$Root){if([IO.Path]::IsPathRooted($Path)){return [IO.Path]::GetFullPath($Path)};return [IO.Path]::GetFullPath((Join-Path $Root $Path))}
foreach($n in 'pg_dump','pg_restore','git'){Need $n}
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path;$out=Resolve-DipPath $OutputDirectory $root;$sourceArtifacts=Resolve-DipPath $ArtifactDirectory $root
New-Item -Force -ItemType Directory $out,$sourceArtifacts|Out-Null
$stamp=[DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ');$id=[guid]::NewGuid().ToString('N');$final=Join-Path $out $stamp;$tmp="$final.tmp-$id";New-Item -ItemType Directory $tmp|Out-Null
$oldPg=$env:PGPASSWORD;$env:PGPASSWORD=$DatabasePassword;$success=$false
try{
 $dump=Join-Path $tmp 'database.dump';& pg_dump -h $DatabaseHost -p $DatabasePort -U $DatabaseUser -d $Database -Fc -f $dump;if($LASTEXITCODE-ne0){throw 'pg_dump failed.'}
 & pg_restore --list $dump *> $null;if($LASTEXITCODE-ne0){throw 'pg_restore could not validate database.dump.'}
 Add-Type -AssemblyName System.IO.Compression.FileSystem;$archive=Join-Path $tmp 'artifacts.zip';[IO.Compression.ZipFile]::CreateFromDirectory($sourceArtifacts,$archive,[IO.Compression.CompressionLevel]::Optimal,$false)
 if((Get-Item $dump).Length-le0-or(Get-Item $archive).Length-le0){throw 'Backup output is empty.'}
 $manifest=[ordered]@{schema='dip-backup-v3';backupId=$id;createdAtUtc=[DateTimeOffset]::UtcNow.ToString('o');sourceCommit=((& git -C $root rev-parse HEAD|Out-String).Trim());sourceWorkingTreeDirty=[bool]((& git -C $root status --porcelain|Out-String).Trim());runtime='native';databaseHost=$DatabaseHost;databasePort=$DatabasePort;database=$Database;databaseFormat='pg_dump-custom';artifactDirectory=(Resolve-Path $sourceArtifacts).Path;databaseBytes=(Get-Item $dump).Length;artifactsBytes=(Get-Item $archive).Length;databaseSha256=(Get-FileHash $dump -Algorithm SHA256).Hash;artifactsSha256=(Get-FileHash $archive -Algorithm SHA256).Hash}
 $manifest|ConvertTo-Json -Depth 6|Set-Content (Join-Path $tmp 'backup-manifest.json') -Encoding UTF8
 Move-Item $tmp $final;$success=$true
 if($RequireOffsiteMirror-and[string]::IsNullOrWhiteSpace($OffsiteDirectory)){throw 'Offsite backup is required but DIP_BACKUP_OFFSITE_DIRECTORY is not configured.'}
 if(-not[string]::IsNullOrWhiteSpace($OffsiteDirectory)){$off=(Resolve-Path $OffsiteDirectory -ErrorAction SilentlyContinue);if(-not$off){New-Item -Force -ItemType Directory $OffsiteDirectory|Out-Null;$off=Resolve-Path $OffsiteDirectory};$target=Join-Path $off.Path $stamp;$offTmp="$target.tmp-$id";Copy-Item $final $offTmp -Recurse;foreach($f in 'database.dump','artifacts.zip'){if((Get-FileHash (Join-Path $final $f) -Algorithm SHA256).Hash-ne(Get-FileHash (Join-Path $offTmp $f) -Algorithm SHA256).Hash){throw "Offsite hash verification failed: $f"}};Move-Item $offTmp $target;Set-Content (Join-Path $final 'offsite-location.txt') $target -Encoding UTF8}
 Write-Host "Backup created and verified: $final" -ForegroundColor Green;Write-Output $final
}finally{$env:PGPASSWORD=$oldPg;if(-not$success-and(Test-Path $tmp)){Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue}}
