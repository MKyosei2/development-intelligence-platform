#requires -Version 5.1
# Purpose: Repairs source readability issues without depending on a hard-coded repository revision.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$ExpectedRevision,
    [switch]$SkipOverlay
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Repo = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@(92, 47))
$Solution = Join-Path $Repo 'DevelopmentIntelligencePlatform.sln'
$RepairScript = Join-Path $PSScriptRoot 'Repair-SourcePurposeComments.ps1'
$VerifyScript = Join-Path $PSScriptRoot 'Verify-SourceReadability.ps1'

function Invoke-ExternalChecked {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE. Arguments: $($ArgumentList -join ' ')"
    }
}

function Get-HeadRevision {
    $output = @(& git -C $Repo rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or $output.Count -eq 0) {
        throw 'Could not resolve repository HEAD.'
    }

    return $output[0].Trim()
}

function Get-ChangedPaths {
    $paths = @(& git -C $Repo diff --name-only --diff-filter=ACMRT)
    if ($LASTEXITCODE -ne 0) {
        throw 'git diff --name-only failed.'
    }

    return @(
        $paths |
        ForEach-Object { $_.Trim().Replace([char]92, [char]47) } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
    )
}

function New-ChangedFilesOverlay {
    param(
        [Parameter(Mandatory = $true)][string[]]$Paths,
        [Parameter(Mandatory = $true)][string]$HeadRevision
    )

    if ($Paths.Count -eq 0) {
        Write-Host 'No tracked source changes were produced; overlay creation was skipped.' -ForegroundColor Yellow
        return $null
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $shortSha = $HeadRevision.Substring(0, [Math]::Min(8, $HeadRevision.Length))
    $outputDirectory = Join-Path $Repo 'artifacts\source-readability'
    $staging = Join-Path ([System.IO.Path]::GetTempPath()) ('dip-readability-overlay-' + [Guid]::NewGuid().ToString('N'))
    $zipPath = Join-Path $outputDirectory "DIP_S_Readability_Fix_Overlay_$shortSha.zip"

    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $staging -Force | Out-Null

    try {
        foreach ($relative in $Paths) {
            $source = Join-Path $Repo ($relative.Replace([char]47, [char]92))
            if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
                throw "Changed file was not found while building overlay: $relative"
            }

            $destination = Join-Path $staging ($relative.Replace([char]47, [char]92))
            $parent = Split-Path -Parent $destination
            if (-not (Test-Path -LiteralPath $parent)) {
                New-Item -ItemType Directory -Path $parent -Force | Out-Null
            }

            Copy-Item -LiteralPath $source -Destination $destination -Force
        }

        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }

        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            $staging,
            $zipPath,
            [System.IO.Compression.CompressionLevel]::Optimal,
            $false
        )

        return [pscustomobject]@{
            Path = $zipPath
            Sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
        }
    }
    finally {
        if (Test-Path -LiteralPath $staging) {
            Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

if (-not (Test-Path -LiteralPath $Solution -PathType Leaf)) {
    throw "DevelopmentIntelligencePlatform.sln was not found under: $Repo"
}
if (-not (Test-Path -LiteralPath $RepairScript -PathType Leaf)) {
    throw "Repair script was not found: $RepairScript"
}
if (-not (Test-Path -LiteralPath $VerifyScript -PathType Leaf)) {
    throw "Readability verifier was not found: $VerifyScript"
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'git was not found.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet was not found. Install/use the repository .NET 8 SDK before applying readability fixes.'
}

$head = Get-HeadRevision
if (-not [string]::IsNullOrWhiteSpace($ExpectedRevision) -and
    -not $head.Equals($ExpectedRevision.Trim(), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Base revision mismatch. Expected $ExpectedRevision but found $head."
}

$status = @(& git -C $Repo status --porcelain)
if ($LASTEXITCODE -ne 0) {
    throw 'git status failed.'
}
if ($status.Count -gt 0) {
    Write-Host 'The working tree is not clean:' -ForegroundColor Red
    $status | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    throw 'Start from a clean working tree so readability changes can be reviewed deterministically.'
}

Write-Host "Readability cleanup base revision: $head" -ForegroundColor Cyan

Write-Host 'Stage 1/4 - Repair malformed Purpose comments' -ForegroundColor Cyan
& $RepairScript -RepositoryRoot $Repo -BrokenOnly
if ($LASTEXITCODE -ne 0) {
    throw 'Purpose repair failed.'
}

Write-Host 'Stage 2/4 - Normalize C# whitespace' -ForegroundColor Cyan
Invoke-ExternalChecked -FilePath 'dotnet' -ArgumentList @(
    'format',
    $Solution,
    'whitespace',
    '--no-restore'
)

Write-Host 'Stage 3/4 - Verify readability and formatting' -ForegroundColor Cyan
& $VerifyScript -RepositoryRoot $Repo -Strict
if ($LASTEXITCODE -ne 0) {
    throw 'Strict source-readability verification failed.'
}
Invoke-ExternalChecked -FilePath 'dotnet' -ArgumentList @(
    'format',
    $Solution,
    'whitespace',
    '--no-restore',
    '--verify-no-changes'
)
& git -C $Repo diff --check
if ($LASTEXITCODE -ne 0) {
    throw 'git diff --check failed.'
}

$changedPaths = @(Get-ChangedPaths)
Write-Host "Changed tracked files: $($changedPaths.Count)" -ForegroundColor Green
$changedPaths | ForEach-Object { Write-Host "  $_" }

Write-Host 'Stage 4/4 - Create optional changed-files overlay' -ForegroundColor Cyan
if (-not $SkipOverlay) {
    $overlay = New-ChangedFilesOverlay -Paths $changedPaths -HeadRevision $head
    if ($null -ne $overlay) {
        Write-Host "Overlay ZIP: $($overlay.Path)" -ForegroundColor Green
        Write-Host "Overlay SHA-256: $($overlay.Sha256)" -ForegroundColor Green
    }
}
else {
    Write-Host 'Overlay creation skipped by request.' -ForegroundColor Yellow
}

Write-Host 'Readability cleanup completed. Build and test verification remain separate gates.' -ForegroundColor Green
