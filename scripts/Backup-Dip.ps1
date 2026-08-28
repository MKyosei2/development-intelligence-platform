[CmdletBinding()]
param(
    [string]$ComposeProject = 'dip',
    [string]$OutputDirectory = 'artifacts\backups',
    [string]$Database = 'dip',
    [string]$DatabaseUser = 'dip'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-SafeIdentifier([string]$Value, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') {
        throw "$Name must start with a letter or digit and contain only letters, digits, underscore, or hyphen."
    }
}

function Assert-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name is required."
    }
}

Assert-SafeIdentifier $ComposeProject 'ComposeProject'
Assert-SafeIdentifier $Database 'Database'
Assert-SafeIdentifier $DatabaseUser 'DatabaseUser'
Assert-Command 'docker'
Assert-Command 'git'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root $OutputDirectory
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$backupId = [Guid]::NewGuid().ToString('N')
$finalDirectory = Join-Path $out $stamp
$tempDirectory = "$finalDirectory.tmp-$backupId"

New-Item -ItemType Directory -Force -Path $out | Out-Null
if (Test-Path $finalDirectory) {
    throw "Backup destination already exists: $finalDirectory"
}
New-Item -ItemType Directory -Force -Path $tempDirectory | Out-Null

$success = $false
$pg = $null
try {
    Push-Location $root
    try {
        $pg = (& docker compose -p $ComposeProject ps -q postgres | Out-String).Trim()
        if (-not $pg) {
            throw "Postgres service for compose project '$ComposeProject' is not running."
        }

        $api = (& docker compose -p $ComposeProject ps -q api | Out-String).Trim()
        if (-not $api) {
            throw "API service for compose project '$ComposeProject' is not running."
        }

        & docker exec $pg rm -f /tmp/dip-backup.dump *> $null
        & docker exec $pg pg_dump -U $DatabaseUser -d $Database -Fc -f /tmp/dip-backup.dump
        if ($LASTEXITCODE -ne 0) {
            throw 'pg_dump failed.'
        }

        # Validate the custom-format archive before it leaves PostgreSQL.
        & docker exec $pg pg_restore --list /tmp/dip-backup.dump *> $null
        if ($LASTEXITCODE -ne 0) {
            throw 'pg_restore could not read the generated database dump.'
        }

        $dbDump = Join-Path $tempDirectory 'database.dump'
        & docker cp "${pg}:/tmp/dip-backup.dump" $dbDump
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not copy database dump.'
        }

        $volume = (& docker inspect $api --format '{{range .Mounts}}{{if eq .Destination "/data/dip-artifacts"}}{{.Name}}{{end}}{{end}}' | Out-String).Trim()
        if (-not $volume) {
            throw 'Could not resolve the Local artifact Docker volume.'
        }

        $artifactArchive = Join-Path $tempDirectory 'artifacts.tar.gz'
        $archiveContainer = "dip-backup-$backupId"
        & docker create --name $archiveContainer -v "${volume}:/source:ro" alpine:3.22 sh -c 'tar -czf /tmp/artifacts.tar.gz -C /source .' *> $null
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not create artifact archive helper container.'
        }
        try {
            & docker start -a $archiveContainer
            if ($LASTEXITCODE -ne 0) {
                throw 'Artifact archive failed.'
            }

            & docker exec $archiveContainer tar -tzf /tmp/artifacts.tar.gz *> $null
            if ($LASTEXITCODE -ne 0) {
                throw 'Generated artifact archive failed validation.'
            }

            & docker cp "${archiveContainer}:/tmp/artifacts.tar.gz" $artifactArchive
            if ($LASTEXITCODE -ne 0) {
                throw 'Could not copy artifact archive.'
            }
        }
        finally {
            & docker rm -f $archiveContainer *> $null
        }

        if ((Get-Item $dbDump).Length -le 0) {
            throw 'Database dump is empty.'
        }
        if ((Get-Item $artifactArchive).Length -le 0) {
            throw 'Artifact archive is empty.'
        }

        $sha = (& git -C $root rev-parse HEAD | Out-String).Trim()
        $branch = (& git -C $root rev-parse --abbrev-ref HEAD | Out-String).Trim()
        $dirty = [bool]((& git -C $root status --porcelain | Out-String).Trim())
        $manifest = [ordered]@{
            schema = 'dip-backup-v2'
            backupId = $backupId
            createdAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
            sourceCommit = $sha
            sourceBranch = $branch
            sourceWorkingTreeDirty = $dirty
            composeProject = $ComposeProject
            database = $Database
            databaseFormat = 'pg_dump-custom'
            artifactVolume = $volume
            databaseBytes = (Get-Item $dbDump).Length
            artifactsBytes = (Get-Item $artifactArchive).Length
            databaseSha256 = (Get-FileHash $dbDump -Algorithm SHA256).Hash
            artifactsSha256 = (Get-FileHash $artifactArchive -Algorithm SHA256).Hash
        }
        $manifestPath = Join-Path $tempDirectory 'backup-manifest.json'
        $manifest | ConvertTo-Json -Depth 6 | Set-Content $manifestPath -Encoding UTF8

        # Atomic publication: incomplete backups keep the .tmp suffix and are never
        # mistaken for a restorable backup set.
        Move-Item -LiteralPath $tempDirectory -Destination $finalDirectory
        $success = $true
        Write-Host "Backup created and verified: $finalDirectory" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}
finally {
    if ($pg) {
        try { & docker exec $pg rm -f /tmp/dip-backup.dump *> $null } catch {}
    }
    if (-not $success -and (Test-Path $tempDirectory)) {
        Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
