[CmdletBinding()]
param(
    [string]$ComposeProject = 'dip',
    [string]$OutputDirectory = 'artifacts\backups',
    [string]$Database = 'dip',
    [string]$DatabaseUser = 'dip',
    [string]$OffsiteDirectory = '',
    [string]$UtilityImage = '',
    [switch]$RequireOffsite
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


function Assert-SeparateOffsiteStorage([string]$LocalDirectory, [string]$OffsiteDirectory) {
    if (-not $RequireOffsite) { return }

    if ($env:OS -eq 'Windows_NT') {
        $localRoot = [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($LocalDirectory))
        $offsiteRoot = [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($OffsiteDirectory))
        if ([string]::IsNullOrWhiteSpace($localRoot) -or [string]::IsNullOrWhiteSpace($offsiteRoot)) {
            throw 'Could not determine local/offsite volume roots.'
        }
        if ($localRoot.Equals($offsiteRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'RequireOffsite requires a different Windows volume/share from OutputDirectory.'
        }
        return
    }

    $stat = Get-Command 'stat' -ErrorAction SilentlyContinue
    if (-not $stat) {
        throw 'RequireOffsite cannot verify storage separation because stat is unavailable.'
    }
    $localDevice = (& $stat.Source -c '%d' $LocalDirectory | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($localDevice)) {
        throw 'Could not determine OutputDirectory filesystem device.'
    }
    $offsiteDevice = (& $stat.Source -c '%d' $OffsiteDirectory | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($offsiteDevice)) {
        throw 'Could not determine OffsiteDirectory filesystem device.'
    }
    if ($localDevice -eq $offsiteDevice) {
        throw 'RequireOffsite requires OffsiteDirectory to be a separately mounted filesystem device.'
    }
}

function Assert-BackupIntegrity([string]$Directory) {
    $manifestPath = Join-Path $Directory 'backup-manifest.json'
    $dbDump = Join-Path $Directory 'database.dump'
    $artifactArchive = Join-Path $Directory 'artifacts.tar.gz'
    foreach ($path in @($manifestPath, $dbDump, $artifactArchive)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Backup integrity check is missing: $path"
        }
    }
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    if ((Get-FileHash $dbDump -Algorithm SHA256).Hash -ne $manifest.databaseSha256) {
        throw "Database SHA-256 mismatch in $Directory"
    }
    if ((Get-FileHash $artifactArchive -Algorithm SHA256).Hash -ne $manifest.artifactsSha256) {
        throw "Artifact SHA-256 mismatch in $Directory"
    }
}

Assert-SafeIdentifier $ComposeProject 'ComposeProject'
Assert-SafeIdentifier $Database 'Database'
Assert-SafeIdentifier $DatabaseUser 'DatabaseUser'
Assert-Command 'docker'
Assert-Command 'git'

if ($RequireOffsite -and [string]::IsNullOrWhiteSpace($OffsiteDirectory)) {
    throw 'RequireOffsite was specified but OffsiteDirectory is empty.'
}

if ([string]::IsNullOrWhiteSpace($UtilityImage)) {
    $UtilityImage = [Environment]::GetEnvironmentVariable('DIP_UTILITY_IMAGE', 'Process')
}
if ([string]::IsNullOrWhiteSpace($UtilityImage)) {
    $UtilityImage = 'alpine:3.22'
}
if ($RequireOffsite -and $UtilityImage -notmatch '^[A-Za-z0-9./:_-]+@sha256:[0-9a-f]{64}$') {
    throw 'RequireOffsite requires UtilityImage (or DIP_UTILITY_IMAGE) to be an immutable repository@sha256:<64 lowercase hex> reference.'
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root $OutputDirectory
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$backupId = [Guid]::NewGuid().ToString('N')
$finalDirectory = Join-Path $out $stamp
$tempDirectory = "$finalDirectory.tmp-$backupId"
$offsiteRoot = $null
$offsiteFinal = $null
$offsiteTemp = $null

if (-not [string]::IsNullOrWhiteSpace($OffsiteDirectory)) {
    $candidate = if ([IO.Path]::IsPathRooted($OffsiteDirectory)) {
        [IO.Path]::GetFullPath($OffsiteDirectory)
    } else {
        [IO.Path]::GetFullPath((Join-Path $root $OffsiteDirectory))
    }
    New-Item -ItemType Directory -Force -Path $candidate | Out-Null
    $offsiteRoot = (Resolve-Path $candidate).Path
    if ([IO.Path]::GetFullPath($offsiteRoot).TrimEnd('\','/') -eq [IO.Path]::GetFullPath($out).TrimEnd('\','/')) {
        throw 'OffsiteDirectory must not be the same directory as OutputDirectory.'
    }
    $offsiteFinal = Join-Path $offsiteRoot $stamp
    $offsiteTemp = "$offsiteFinal.tmp-$backupId"
}

New-Item -ItemType Directory -Force -Path $out | Out-Null
if ($offsiteRoot) { Assert-SeparateOffsiteStorage $out $offsiteRoot }
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
        & docker create --name $archiveContainer -v "${volume}:/source:ro" $UtilityImage sh -c 'tar -czf /tmp/artifacts.tar.gz -C /source .' *> $null
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
            offsiteRequired = [bool]$RequireOffsite
            offsiteConfigured = [bool]$offsiteRoot
        }
        $manifestPath = Join-Path $tempDirectory 'backup-manifest.json'
        $manifest | ConvertTo-Json -Depth 6 | Set-Content $manifestPath -Encoding UTF8

        # Atomic local publication: incomplete backups retain a .tmp suffix.
        Move-Item -LiteralPath $tempDirectory -Destination $finalDirectory
        Assert-BackupIntegrity $finalDirectory

        if ($offsiteRoot) {
            if (Test-Path $offsiteFinal) {
                throw "Offsite backup destination already exists: $offsiteFinal"
            }
            New-Item -ItemType Directory -Force -Path $offsiteTemp | Out-Null
            Copy-Item -Path (Join-Path $finalDirectory '*') -Destination $offsiteTemp -Recurse -Force
            Assert-BackupIntegrity $offsiteTemp
            'OFFSITE MIRROR VERIFIED' | Set-Content (Join-Path $offsiteTemp 'OFFSITE_MIRROR_PASS.txt') -Encoding UTF8
            Move-Item -LiteralPath $offsiteTemp -Destination $offsiteFinal
            Write-Host "Offsite backup mirror verified: $offsiteFinal" -ForegroundColor Green
        }

        $success = $true
        Write-Host "Backup created and verified: $finalDirectory" -ForegroundColor Green
        Write-Output $finalDirectory
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
    if ($offsiteTemp -and (Test-Path $offsiteTemp)) {
        Remove-Item -LiteralPath $offsiteTemp -Recurse -Force -ErrorAction SilentlyContinue
    }
}
