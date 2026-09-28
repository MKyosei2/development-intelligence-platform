# Purpose: Verifies database and artifact disaster recovery using local PostgreSQL tools or a running PostgreSQL Docker container.
[CmdletBinding()]
param(
    [string]$DatabaseUser = $(if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'dip' }),
    [string]$DatabaseHost = $(if ($env:POSTGRES_HOST) { $env:POSTGRES_HOST } else { '127.0.0.1' }),
    [int]$DatabasePort = $(if ($env:POSTGRES_PORT) { [int]$env:POSTGRES_PORT } else { 5432 }),
    [string]$DatabasePassword = $(if ($env:POSTGRES_PASSWORD) { $env:POSTGRES_PASSWORD } else { 'dip-test-password' })
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'git is required.'
}

. (Join-Path $PSScriptRoot 'PostgreSqlRuntime.ps1')

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root 'artifacts\dr-verification'
New-Item -Force -ItemType Directory $out | Out-Null

$db = 'dip_dr_' + [guid]::NewGuid().ToString('N').Substring(0, 12)

$pg = Resolve-DipPostgresClient `
    -HostName $DatabaseHost `
    -Port $DatabasePort `
    -User $DatabaseUser `
    -Password $DatabasePassword `
    -RequireBackupTools

function Invoke-Psql {
    param(
        [string]$Database,
        [string]$Sql,
        [switch]$TuplesOnly
    )

    Invoke-DipPsql `
        -Client $pg `
        -Database $Database `
        -Sql $Sql `
        -TuplesOnly:$TuplesOnly
}

try {
    $ready = Wait-DipPostgresReady -Client $pg -TimeoutSeconds 120
    if (-not $ready.Ready) {
        throw "PostgreSQL is not reachable at ${DatabaseHost}:${DatabasePort} after $($ready.Attempts) attempt(s)."
    }

    Invoke-Psql `
        -Database 'postgres' `
        -Sql "CREATE DATABASE `"$db`" OWNER `"$DatabaseUser`";" |
        Out-Null

    Invoke-Psql `
        -Database $db `
        -Sql "CREATE TABLE dr_probe(id int primary key,value text);INSERT INTO dr_probe VALUES(1,'DIP-DR-PROBE');" |
        Out-Null

    $dump = Join-Path $out 'dip-dr.dump'
    Remove-Item $dump -Force -ErrorAction SilentlyContinue

    if ($pg.Mode -eq 'Local') {
        $oldPassword = $env:PGPASSWORD
        $env:PGPASSWORD = $DatabasePassword

        try {
            Invoke-DipNativeChecked `
                -File 'pg_dump' `
                -Arguments @(
                    '-h',$DatabaseHost,
                    '-p',[string]$DatabasePort,
                    '-U',$DatabaseUser,
                    '-d',$db,
                    '-Fc',
                    '-f',$dump
                ) `
                -DiscardOutput `
                -FailureMessage 'pg_dump failed.'

            Invoke-DipNativeChecked `
                -File 'pg_restore' `
                -Arguments @('--list',$dump) `
                -DiscardOutput `
                -FailureMessage 'pg_restore --list failed.'
        }
        finally {
            $env:PGPASSWORD = $oldPassword
        }
    }
    else {
        Invoke-DipNativeChecked `
            -File 'docker' `
            -Arguments @(
                'exec',
                '-e',"PGPASSWORD=$DatabasePassword",
                $pg.Container,
                'pg_dump',
                '-U',$DatabaseUser,
                '-d',$db,
                '-Fc',
                '-f','/tmp/dip-dr.dump'
            ) `
            -DiscardOutput `
            -FailureMessage 'Docker pg_dump failed.'

        Invoke-DipNativeChecked `
            -File 'docker' `
            -Arguments @(
                'exec',
                $pg.Container,
                'pg_restore',
                '--list',
                '/tmp/dip-dr.dump'
            ) `
            -DiscardOutput `
            -FailureMessage 'Docker pg_restore --list failed.'

        Invoke-DipNativeChecked `
            -File 'docker' `
            -Arguments @(
                'cp',
                "$($pg.Container):/tmp/dip-dr.dump",
                $dump
            ) `
            -DiscardOutput `
            -FailureMessage 'docker cp dump failed.'
    }

    Invoke-Psql `
        -Database 'postgres' `
        -Sql "DROP DATABASE `"$db`";" |
        Out-Null

    Invoke-Psql `
        -Database 'postgres' `
        -Sql "CREATE DATABASE `"$db`" OWNER `"$DatabaseUser`";" |
        Out-Null

    if ($pg.Mode -eq 'Local') {
        $oldPassword = $env:PGPASSWORD
        $env:PGPASSWORD = $DatabasePassword

        try {
            Invoke-DipNativeChecked `
                -File 'pg_restore' `
                -Arguments @(
                    '-h',$DatabaseHost,
                    '-p',[string]$DatabasePort,
                    '-U',$DatabaseUser,
                    '-d',$db,
                    '--no-owner',
                    '--no-privileges',
                    $dump
                ) `
                -DiscardOutput `
                -FailureMessage 'pg_restore failed.'
        }
        finally {
            $env:PGPASSWORD = $oldPassword
        }
    }
    else {
        Invoke-DipNativeChecked `
            -File 'docker' `
            -Arguments @(
                'cp',
                $dump,
                "$($pg.Container):/tmp/dip-dr.dump"
            ) `
            -DiscardOutput `
            -FailureMessage 'docker cp restore dump failed.'

        Invoke-DipNativeChecked `
            -File 'docker' `
            -Arguments @(
                'exec',
                '-e',"PGPASSWORD=$DatabasePassword",
                $pg.Container,
                'pg_restore',
                '-U',$DatabaseUser,
                '-d',$db,
                '--no-owner',
                '--no-privileges',
                '/tmp/dip-dr.dump'
            ) `
            -DiscardOutput `
            -FailureMessage 'Docker pg_restore failed.'
    }

    $value = (
        Invoke-Psql `
            -Database $db `
            -Sql 'SELECT value FROM dr_probe WHERE id=1;' `
            -TuplesOnly |
        Out-String
    ).Trim()

    if ($value -ne 'DIP-DR-PROBE') {
        throw 'Database restore verification mismatch.'
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $src = Join-Path $out 'source-artifacts'
    $dst = Join-Path $out 'restored-artifacts'

    Remove-Item $src, $dst -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory $src | Out-Null

    'artifact-proof' |
        Set-Content (Join-Path $src 'proof.txt')

    $zip = Join-Path $out 'artifacts.zip'
    Remove-Item $zip -Force -ErrorAction SilentlyContinue

    [IO.Compression.ZipFile]::CreateFromDirectory($src,$zip)
    [IO.Compression.ZipFile]::ExtractToDirectory($zip,$dst)

    $sourceArtifactHash =
        (Get-FileHash (Join-Path $src 'proof.txt') -Algorithm SHA256).Hash
    $restoredArtifactHash =
        (Get-FileHash (Join-Path $dst 'proof.txt') -Algorithm SHA256).Hash

    if ($sourceArtifactHash -ne $restoredArtifactHash) {
        throw 'Artifact restore verification mismatch.'
    }

    $sha = (
        Invoke-DipNativeChecked `
            -File 'git' `
            -Arguments @('-C',$root,'rev-parse','HEAD') `
            -FailureMessage 'Could not resolve exact repository SHA.' |
        Out-String
    ).Trim()

    $runtime = Get-DipPostgresRuntimeLabel -Client $pg

    [ordered]@{
        schema = 'dip-dr-verification-v6'
        runtime = $runtime
        sourceCommit = $sha
        verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        databaseRestore = 'PASS'
        artifactRestore = 'PASS'
        dumpSha256 = (Get-FileHash $dump -Algorithm SHA256).Hash
        postgresReadyAttempts = $ready.Attempts
        nativeExitCodePolicy = 'exit-code-only'
    } |
        ConvertTo-Json |
        Set-Content `
            (Join-Path $out 'dr-verification.json') `
            -Encoding UTF8

    @(
        'DR VERIFICATION PASS'
        "Commit=$sha"
        "Runtime=$runtime"
        'DatabaseRestore=PASS'
        'ArtifactRestore=PASS'
        "PostgresReadyAttempts=$($ready.Attempts)"
        'NativeExitCodePolicy=exit-code-only'
    ) |
        Set-Content `
            (Join-Path $out 'DR_VERIFICATION_PASS.txt') `
            -Encoding UTF8
}
finally {
    try {
        Invoke-Psql `
            -Database 'postgres' `
            -Sql "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$db';" |
            Out-Null
    }
    catch {
    }

    try {
        Invoke-Psql `
            -Database 'postgres' `
            -Sql "DROP DATABASE IF EXISTS `"$db`";" |
            Out-Null
    }
    catch {
    }
}
