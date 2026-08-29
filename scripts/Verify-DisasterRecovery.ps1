[CmdletBinding()]
param(
    [string]$OffsiteDirectory = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
foreach ($command in 'docker','git') {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command is required."
    }
}

function Get-FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
    $listener.Start()
    $port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()
    return $port
}

$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$compose = "dip-dr-$stamp"
$pgPort = Get-FreePort
$out = Join-Path $root 'artifacts\dr-verification'
New-Item -Force -ItemType Directory $out | Out-Null
Remove-Item (Join-Path $out 'DR_VERIFICATION_PASS.txt') -Force -ErrorAction SilentlyContinue

$offsite = $null
if (-not [string]::IsNullOrWhiteSpace($OffsiteDirectory)) {
    $offsiteCandidate = if ([IO.Path]::IsPathRooted($OffsiteDirectory)) {
        [IO.Path]::GetFullPath($OffsiteDirectory)
    } else {
        [IO.Path]::GetFullPath((Join-Path $root $OffsiteDirectory))
    }
    New-Item -ItemType Directory -Force $offsiteCandidate | Out-Null
    $offsite = (Resolve-Path $offsiteCandidate).Path
}

$vars = 'POSTGRES_PORT','POSTGRES_DB','POSTGRES_USER','POSTGRES_PASSWORD'
$old = @{}
foreach ($name in $vars) {
    $old[$name] = [Environment]::GetEnvironmentVariable($name,'Process')
}

try {
    $env:POSTGRES_PORT = "$pgPort"
    $env:POSTGRES_DB = 'dipdr'
    $env:POSTGRES_USER = 'dip'
    $env:POSTGRES_PASSWORD = 'dip-dr-password'
    Push-Location $root
    try {
        & docker compose -p $compose up -d postgres
        if ($LASTEXITCODE -ne 0) { throw 'Postgres start failed' }
        $cid = (& docker compose -p $compose ps -q postgres | Out-String).Trim()
        if (-not $cid) { throw 'Postgres container missing.' }

        $ready = $false
        for ($i=0; $i -lt 60; $i++) {
            & docker exec $cid pg_isready -U dip -d dipdr *> $null
            if ($LASTEXITCODE -eq 0) { $ready = $true; break }
            Start-Sleep -Milliseconds 500
        }
        if (-not $ready) { throw 'Postgres did not become ready.' }

        & docker exec $cid psql -U dip -d dipdr -v ON_ERROR_STOP=1 -c "CREATE TABLE dr_probe(id int primary key,value text); INSERT INTO dr_probe VALUES(1,'DIP-DR-PROBE');" *> $null
        & docker exec $cid sh -c "pg_dump -U dip -d dipdr -Fc -f /tmp/dip-dr.dump"
        if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed' }
        & docker exec $cid pg_restore --list /tmp/dip-dr.dump *> $null
        if ($LASTEXITCODE -ne 0) { throw 'Generated DR dump is unreadable.' }

        $dump = Join-Path $out 'dip-dr.dump'
        & docker cp "${cid}:/tmp/dip-dr.dump" $dump *> $null
        $artifactDir = Join-Path $out 'source-artifacts'
        $restoreDir = Join-Path $out 'restored-artifacts'
        Remove-Item $artifactDir,$restoreDir -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -Force -ItemType Directory $artifactDir,$restoreDir | Out-Null
        'artifact-proof' | Set-Content (Join-Path $artifactDir 'proof.txt') -Encoding UTF8
        $sourceHash = (Get-FileHash (Join-Path $artifactDir 'proof.txt') -Algorithm SHA256).Hash
        $archive = Join-Path $out 'artifacts.zip'
        Compress-Archive -Path (Join-Path $artifactDir '*') -DestinationPath $archive -Force

        $restoreDump = $dump
        $restoreArchive = $archive
        $offsiteVerified = $false
        if ($offsite) {
            $offsiteRun = Join-Path $offsite "dr-$stamp"
            $offsiteTemp = "$offsiteRun.tmp"
            Remove-Item $offsiteTemp,$offsiteRun -Recurse -Force -ErrorAction SilentlyContinue
            New-Item -Force -ItemType Directory $offsiteTemp | Out-Null
            Copy-Item $dump (Join-Path $offsiteTemp 'database.dump')
            Copy-Item $archive (Join-Path $offsiteTemp 'artifacts.zip')
            $sourceDumpHash = (Get-FileHash $dump -Algorithm SHA256).Hash
            $sourceArchiveHash = (Get-FileHash $archive -Algorithm SHA256).Hash
            if ((Get-FileHash (Join-Path $offsiteTemp 'database.dump') -Algorithm SHA256).Hash -ne $sourceDumpHash) { throw 'Offsite DB mirror hash mismatch.' }
            if ((Get-FileHash (Join-Path $offsiteTemp 'artifacts.zip') -Algorithm SHA256).Hash -ne $sourceArchiveHash) { throw 'Offsite artifact mirror hash mismatch.' }
            Move-Item $offsiteTemp $offsiteRun
            $restoreDump = Join-Path $offsiteRun 'database.dump'
            $restoreArchive = Join-Path $offsiteRun 'artifacts.zip'
            $offsiteVerified = $true
        }

        & docker exec $cid psql -U dip -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE dipdr;" *> $null
        if ($LASTEXITCODE -ne 0) { throw 'Drop restore target failed.' }
        & docker exec $cid psql -U dip -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE dipdr OWNER dip;" *> $null
        if ($LASTEXITCODE -ne 0) { throw 'Create restore target failed.' }
        & docker cp $restoreDump "${cid}:/tmp/dip-dr-restore.dump" *> $null
        & docker exec $cid pg_restore -U dip -d dipdr --clean --if-exists /tmp/dip-dr-restore.dump
        if ($LASTEXITCODE -ne 0) { throw 'pg_restore failed' }

        $value = (& docker exec $cid psql -U dip -d dipdr -Atc 'SELECT value FROM dr_probe WHERE id=1;' | Out-String).Trim()
        if ($value -ne 'DIP-DR-PROBE') { throw 'Database restore verification mismatch.' }
        Expand-Archive $restoreArchive -DestinationPath $restoreDir -Force
        $restoredHash = (Get-FileHash (Join-Path $restoreDir 'proof.txt') -Algorithm SHA256).Hash
        if ($sourceHash -ne $restoredHash) { throw 'Artifact restore hash mismatch.' }
    }
    finally {
        Pop-Location
    }

    $sha = (& git -C $root rev-parse HEAD | Out-String).Trim()
    $manifest = [ordered]@{
        schema = 'dip-dr-verification-v3'
        sourceCommit = $sha
        verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        postgresPort = $pgPort
        databaseRestore = 'PASS'
        artifactRestore = 'PASS'
        offsiteRestore = if ($offsite) { 'PASS' } else { 'NOT_REQUESTED' }
        artifactSha256 = $sourceHash
        dumpSha256 = (Get-FileHash $dump -Algorithm SHA256).Hash
    }
    $manifest | ConvertTo-Json | Set-Content (Join-Path $out 'dr-verification.json') -Encoding UTF8
    @(
        'DR VERIFICATION PASS',
        "Commit=$sha",
        'DatabaseRestore=PASS',
        'ArtifactRestore=PASS',
        "OffsiteRestore=$($manifest.offsiteRestore)"
    ) | Set-Content (Join-Path $out 'DR_VERIFICATION_PASS.txt') -Encoding UTF8
}
finally {
    Push-Location $root
    try { & docker compose -p $compose down -v --remove-orphans *> $null } catch {}
    finally { Pop-Location }
    foreach ($name in $vars) {
        [Environment]::SetEnvironmentVariable($name,$old[$name],'Process')
    }
}
