# Purpose: Runs Initialize Self Hosted Postgres as part of repository development and verification workflows.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($env:POSTGRES_BIN) { $env:PATH = "$env:POSTGRES_BIN;$env:PATH" }
$env:PGPASSWORD = $env:POSTGRES_PASSWORD

$hostName = $env:POSTGRES_HOST
$port = $env:POSTGRES_PORT
$user = $env:POSTGRES_USER
$db = $env:POSTGRES_DB

if (-not $hostName -or -not $port -or -not $user -or -not $db -or -not $env:POSTGRES_PASSWORD) {
    throw 'PostgreSQL fallback environment is incomplete.'
}

& psql -w -h $hostName -p $port -U $user -d postgres -At -v ON_ERROR_STOP=1 -c 'SELECT 1;'
if ($LASTEXITCODE -ne 0) { throw 'Fallback PostgreSQL login failed.' }

$escapedDb = $db.Replace("'","''")
$terminateSql = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$escapedDb' AND pid <> pg_backend_pid();"
& psql -w -h $hostName -p $port -U $user -d postgres -v ON_ERROR_STOP=1 -c $terminateSql
if ($LASTEXITCODE -ne 0) { throw 'Could not terminate stale fallback DB sessions.' }

& psql -w -h $hostName -p $port -U $user -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS `"$db`";"
if ($LASTEXITCODE -ne 0) { throw 'Could not drop fallback database.' }

& psql -w -h $hostName -p $port -U $user -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE `"$db`" OWNER `"$user`";"
if ($LASTEXITCODE -ne 0) { throw 'Could not create fallback database.' }

Write-Host "PASS: Recreated PostgreSQL database '$db'."
