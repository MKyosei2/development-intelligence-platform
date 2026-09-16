# Purpose: Automates the V er if y-D is as te rR ec ov er y repository workflow.
[CmdletBinding()]
param(
    [string]$DatabaseUser=$(if($env:POSTGRES_USER){$env:POSTGRES_USER}else{'dip'}),
    [string]$DatabaseHost=$(if($env:POSTGRES_HOST){$env:POSTGRES_HOST}else{'127.0.0.1'}),
    [int]$DatabasePort=$(if($env:POSTGRES_PORT){[int]$env:POSTGRES_PORT}else{5432}),
    [string]$DatabasePassword=$(if($env:POSTGRES_PASSWORD){$env:POSTGRES_PASSWORD}else{'dip-test-password'})
)

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

if(-not(Get-Command git -ErrorAction SilentlyContinue)){throw 'git is required.'}

$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out=Join-Path $root 'artifacts\dr-verification'
New-Item -Force -ItemType Directory $out | Out-Null
$db='dip_dr_'+[guid]::NewGuid().ToString('N').Substring(0,12)
$container=if($env:DIP_POSTGRES_CONTAINER){$env:DIP_POSTGRES_CONTAINER}else{'dip-local-ci-postgres'}

$localPg = (
    (Get-Command psql -ErrorAction SilentlyContinue) -and
    (Get-Command pg_dump -ErrorAction SilentlyContinue) -and
    (Get-Command pg_restore -ErrorAction SilentlyContinue)
)

if(-not $localPg){
    if(-not(Get-Command docker -ErrorAction SilentlyContinue)){throw 'PostgreSQL client tools or Docker are required.'}
    docker info *> $null
    if($LASTEXITCODE -ne 0){throw 'Docker Desktop is not running.'}
}

function Invoke-Psql([string]$Database,[string]$Sql,[switch]$TuplesOnly){
    if($localPg){
        $oldPwd=$env:PGPASSWORD
        $env:PGPASSWORD=$DatabasePassword
        try{
            $args=@('-h',$DatabaseHost,'-p',[string]$DatabasePort,'-U',$DatabaseUser,'-d',$Database,'-v','ON_ERROR_STOP=1')
            if($TuplesOnly){$args+=@('-A','-t')}
            $args+=@('-c',$Sql)
            $output=& psql @args
            if($LASTEXITCODE -ne 0){throw "psql failed for $Database"}
            return $output
        }finally{$env:PGPASSWORD=$oldPwd}
    }else{
        $args=@('exec','-e',"PGPASSWORD=$DatabasePassword",$container,'psql','-U',$DatabaseUser,'-d',$Database,'-v','ON_ERROR_STOP=1')
        if($TuplesOnly){$args+=@('-A','-t')}
        $args+=@('-c',$Sql)
        $output=& docker @args
        if($LASTEXITCODE -ne 0){throw "docker psql failed for $Database"}
        return $output
    }
}

try{
    Invoke-Psql 'postgres' "CREATE DATABASE `"$db`" OWNER `"$DatabaseUser`";" | Out-Null
    Invoke-Psql $db "CREATE TABLE dr_probe(id int primary key,value text);INSERT INTO dr_probe VALUES(1,'DIP-DR-PROBE');" | Out-Null

    $dump=Join-Path $out 'dip-dr.dump'
    Remove-Item $dump -Force -ErrorAction SilentlyContinue

    if($localPg){
        $oldPwd=$env:PGPASSWORD
        $env:PGPASSWORD=$DatabasePassword
        try{
            pg_dump -h $DatabaseHost -p $DatabasePort -U $DatabaseUser -d $db -Fc -f $dump
            if($LASTEXITCODE -ne 0){throw 'pg_dump failed.'}
            pg_restore --list $dump *> $null
            if($LASTEXITCODE -ne 0){throw 'pg_restore --list failed.'}
        }finally{$env:PGPASSWORD=$oldPwd}
    }else{
        docker exec -e "PGPASSWORD=$DatabasePassword" $container pg_dump -U $DatabaseUser -d $db -Fc -f /tmp/dip-dr.dump
        if($LASTEXITCODE -ne 0){throw 'Docker pg_dump failed.'}
        docker exec $container pg_restore --list /tmp/dip-dr.dump *> $null
        if($LASTEXITCODE -ne 0){throw 'Docker pg_restore --list failed.'}
        docker cp "${container}:/tmp/dip-dr.dump" $dump *> $null
        if($LASTEXITCODE -ne 0){throw 'docker cp dump failed.'}
    }

    Invoke-Psql 'postgres' "DROP DATABASE `"$db`";" | Out-Null
    Invoke-Psql 'postgres' "CREATE DATABASE `"$db`" OWNER `"$DatabaseUser`";" | Out-Null

    if($localPg){
        $oldPwd=$env:PGPASSWORD
        $env:PGPASSWORD=$DatabasePassword
        try{
            pg_restore -h $DatabaseHost -p $DatabasePort -U $DatabaseUser -d $db --no-owner --no-privileges $dump
            if($LASTEXITCODE -ne 0){throw 'pg_restore failed.'}
        }finally{$env:PGPASSWORD=$oldPwd}
    }else{
        docker cp $dump "${container}:/tmp/dip-dr.dump" *> $null
        if($LASTEXITCODE -ne 0){throw 'docker cp restore dump failed.'}
        docker exec -e "PGPASSWORD=$DatabasePassword" $container pg_restore -U $DatabaseUser -d $db --no-owner --no-privileges /tmp/dip-dr.dump
        if($LASTEXITCODE -ne 0){throw 'Docker pg_restore failed.'}
    }

    $value=(Invoke-Psql $db 'SELECT value FROM dr_probe WHERE id=1;' -TuplesOnly | Out-String).Trim()
    if($value -ne 'DIP-DR-PROBE'){throw 'Database restore verification mismatch.'}

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $src=Join-Path $out 'source-artifacts'
    $dst=Join-Path $out 'restored-artifacts'
    Remove-Item $src,$dst -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory $src | Out-Null
    'artifact-proof' | Set-Content (Join-Path $src 'proof.txt')

    $zip=Join-Path $out 'artifacts.zip'
    Remove-Item $zip -Force -ErrorAction SilentlyContinue
    [IO.Compression.ZipFile]::CreateFromDirectory($src,$zip)
    [IO.Compression.ZipFile]::ExtractToDirectory($zip,$dst)

    if((Get-FileHash (Join-Path $src 'proof.txt') -Algorithm SHA256).Hash -ne
       (Get-FileHash (Join-Path $dst 'proof.txt') -Algorithm SHA256).Hash){
        throw 'Artifact restore verification mismatch.'
    }

    $sha=(& git -C $root rev-parse HEAD | Out-String).Trim()
    [ordered]@{
        schema='dip-dr-verification-v4'
        runtime='native-or-docker-client'
        sourceCommit=$sha
        verifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
        databaseRestore='PASS'
        artifactRestore='PASS'
        dumpSha256=(Get-FileHash $dump -Algorithm SHA256).Hash
    } | ConvertTo-Json | Set-Content (Join-Path $out 'dr-verification.json') -Encoding UTF8

    "DR VERIFICATION PASS`nCommit=$sha`nDatabaseRestore=PASS`nArtifactRestore=PASS" |
        Set-Content (Join-Path $out 'DR_VERIFICATION_PASS.txt') -Encoding UTF8
}
finally{
    try{Invoke-Psql 'postgres' "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$db';" | Out-Null}catch{}
    try{Invoke-Psql 'postgres' "DROP DATABASE IF EXISTS `"$db`";" | Out-Null}catch{}
}
