# Purpose: Runs Initialize Self Hosted Postgres as part of repository development and verification workflows.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'PostgreSqlRuntime.ps1')

if ($env:POSTGRES_BIN) {
    $env:PATH = "$env:POSTGRES_BIN;$env:PATH"
}

$env:PGPASSWORD = $env:POSTGRES_PASSWORD

$hostName = $env:POSTGRES_HOST
$port = $env:POSTGRES_PORT
$user = $env:POSTGRES_USER
$db = $env:POSTGRES_DB

if (-not $hostName -or -not $port -or -not $user -or -not $db -or -not $env:POSTGRES_PASSWORD) {
    throw 'PostgreSQL fallback environment is incomplete.'
}

if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
    throw 'psql is required.'
}

function Invoke-FallbackPsql {
    param(
        [Parameter(Mandatory=$true)][string]$Sql,
        [switch]$TuplesOnly,
        [switch]$DiscardOutput
    )

    $arguments = @(
        '-w',
        '-h',$hostName,
        '-p',[string]$port,
        '-U',$user,
        '-d','postgres',
        '-v','ON_ERROR_STOP=1'
    )
    if ($TuplesOnly) { $arguments += @('-A','-t') }
    $arguments += @('-f','-')

    Invoke-DipNativeInputChecked -File 'psql' -Arguments $arguments -InputText $Sql -DiscardOutput:$DiscardOutput -FailureMessage 'Fallback PostgreSQL command failed.'
}

$probe = (Invoke-FallbackPsql -Sql 'SELECT 1;' -TuplesOnly | Out-String).Trim()
if ($probe -ne '1') {
    throw "Fallback PostgreSQL login probe returned '$probe'."
}

$escapedDbLiteral = $db.Replace("'","''")
$safeDb = $db.Replace('"','""')
$safeUser = $user.Replace('"','""')

Invoke-FallbackPsql -Sql "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$escapedDbLiteral' AND pid <> pg_backend_pid();" -DiscardOutput
Invoke-FallbackPsql -Sql "DROP DATABASE IF EXISTS `"$safeDb`";" -DiscardOutput
Invoke-FallbackPsql -Sql "CREATE DATABASE `"$safeDb`" OWNER `"$safeUser`";" -DiscardOutput

Write-Host "PASS: Recreated PostgreSQL database '$db'."
