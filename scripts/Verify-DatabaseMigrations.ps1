# Purpose: Verifies Database Migrations for repository development and validation workflows.
[CmdletBinding()]
param(
    [string]$DatabaseUser=$(if($env:POSTGRES_USER){$env:POSTGRES_USER}else{'dip'}),
    [string]$DatabaseHost=$(if($env:POSTGRES_HOST){$env:POSTGRES_HOST}else{'127.0.0.1'}),
    [int]$DatabasePort=$(if($env:POSTGRES_PORT){[int]$env:POSTGRES_PORT}else{5432}),
    [string]$DatabasePassword=$(if($env:POSTGRES_PASSWORD){$env:POSTGRES_PASSWORD}else{'dip-test-password'})
)

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

if(-not(Get-Command dotnet -ErrorAction SilentlyContinue)){throw 'dotnet is required.'}

$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$db='dip_migration_'+[guid]::NewGuid().ToString('N').Substring(0,12)
$old=$env:ConnectionStrings__PostgreSql
$container=if($env:DIP_POSTGRES_CONTAINER){$env:DIP_POSTGRES_CONTAINER}else{'dip-local-ci-postgres'}
$localPsql=[bool](Get-Command psql -ErrorAction SilentlyContinue)

if(-not $localPsql){
    if(-not(Get-Command docker -ErrorAction SilentlyContinue)){throw 'psql or Docker is required.'}
    docker info *> $null
    if($LASTEXITCODE -ne 0){throw 'Docker Desktop is not running.'}
}

function Invoke-Psql([string]$Database,[string]$Sql,[switch]$TuplesOnly){
    if($localPsql){
        $oldPwd=$env:PGPASSWORD
        $env:PGPASSWORD=$DatabasePassword
        try{
            $args=@('-h',$DatabaseHost,'-p',[string]$DatabasePort,'-U',$DatabaseUser,'-d',$Database,'-v','ON_ERROR_STOP=1')
            if($TuplesOnly){$args+=@('-A','-t')}
            $args+=@('-c',$Sql)
            $output=& psql @args
            if($LASTEXITCODE -ne 0){throw "psql failed for database $Database"}
            return $output
        }finally{$env:PGPASSWORD=$oldPwd}
    }else{
        $args=@('exec','-e',"PGPASSWORD=$DatabasePassword",$container,'psql','-U',$DatabaseUser,'-d',$Database,'-v','ON_ERROR_STOP=1')
        if($TuplesOnly){$args+=@('-A','-t')}
        $args+=@('-c',$Sql)
        $output=& docker @args
        if($LASTEXITCODE -ne 0){throw "docker psql failed for database $Database"}
        return $output
    }
}

try{
    Invoke-Psql 'postgres' "CREATE DATABASE `"$db`" OWNER `"$DatabaseUser`";" | Out-Null
    $env:ConnectionStrings__PostgreSql="Host=$DatabaseHost;Port=$DatabasePort;Database=$db;Username=$DatabaseUser;Password=$DatabasePassword"

    Push-Location $root
    try{
        dotnet tool restore
        if($LASTEXITCODE -ne 0){throw 'dotnet tool restore failed.'}

        $migrations=@(
            dotnet ef migrations list --project src/Dip.Infrastructure --startup-project src/Dip.Api --no-connect 2>$null
        ) | Where-Object { $_ -match '^\d{8,}_.+' }

        if($migrations.Count -lt 1){throw 'No EF migrations were discovered.'}
        $latest=$migrations[-1]

        if($migrations.Count -gt 1){
            $previous=$migrations[-2]
            dotnet ef database update $previous --project src/Dip.Infrastructure --startup-project src/Dip.Api
            if($LASTEXITCODE -ne 0){throw "Migration to previous revision failed: $previous"}
        }

        dotnet ef database update $latest --project src/Dip.Infrastructure --startup-project src/Dip.Api
        if($LASTEXITCODE -ne 0){throw "Migration to latest revision failed: $latest"}

        dotnet ef database update $latest --project src/Dip.Infrastructure --startup-project src/Dip.Api
        if($LASTEXITCODE -ne 0){throw 'Idempotent migration reapply failed.'}

        $applied=(Invoke-Psql $db 'SELECT count(*) FROM "__EFMigrationsHistory";' -TuplesOnly | Out-String).Trim()
        if($applied -notmatch '^\d+$' -or [int]$applied -lt 1){throw 'Migration history verification failed.'}

        Write-Host "Database migration verification PASS: $latest ($applied applied)" -ForegroundColor Green
    }finally{Pop-Location}
}
finally{
    $env:ConnectionStrings__PostgreSql=$old
    try{Invoke-Psql 'postgres' "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$db';" | Out-Null}catch{}
    try{Invoke-Psql 'postgres' "DROP DATABASE IF EXISTS `"$db`";" | Out-Null}catch{}
}
