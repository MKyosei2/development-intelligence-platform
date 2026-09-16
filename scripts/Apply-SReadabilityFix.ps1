#requires -Version 5.1
# Purpose: Applies the S-priority readability fixes, formats C# source, and proves the strengthened source audit on the current repository.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$AllowDifferentRevision,
    [switch]$SkipDotNetFormat
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Repo = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@(92, 47))
$ExpectedBaseSha = '6c7ee85cd370288e1c7b8e9c89a3a5aaa6888c40'
$Solution = Join-Path $Repo 'DevelopmentIntelligencePlatform.sln'

function Invoke-ExternalChecked {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$FilePath failed with exit code $exitCode. Arguments: $($ArgumentList -join ' ')"
    }
}

if (-not (Test-Path -LiteralPath $Solution -PathType Leaf)) {
    throw "DevelopmentIntelligencePlatform.sln was not found under: $Repo"
}

$gitCommand = Get-Command git -ErrorAction SilentlyContinue
if ($gitCommand) {
    $headOutput = & git -C $Repo rev-parse HEAD 2>$null
    $gitExitCode = $LASTEXITCODE
    if ($gitExitCode -eq 0) {
        $head = (($headOutput | Out-String).Trim())
        if (-not $AllowDifferentRevision -and $head -ne $ExpectedBaseSha) {
            throw "Base revision mismatch. Expected $ExpectedBaseSha but found $head. Use -AllowDifferentRevision only after reviewing the diff."
        }
    }
}

Write-Host 'Stage 1/4 - Repair malformed or duplicate Purpose comments' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'Repair-SourcePurposeComments.ps1') -RepositoryRoot $Repo

if (-not $SkipDotNetFormat) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet was not found. Use the repository .NET 8 environment, or rerun with -SkipDotNetFormat only for audit diagnosis.'
    }

    Write-Host 'Stage 2/4 - Normalize C# formatting' -ForegroundColor Cyan
    Invoke-ExternalChecked -FilePath 'dotnet' -ArgumentList @(
        'format',
        $Solution,
        '--no-restore'
    )
}
else {
    Write-Host 'Stage 2/4 - dotnet format skipped by request' -ForegroundColor Yellow
}

Write-Host 'Stage 3/4 - Run strengthened source readability audit' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'Verify-SourceReadability.ps1') -RepositoryRoot $Repo -Strict

if (-not $SkipDotNetFormat) {
    Write-Host 'Stage 4/4 - Prove dotnet formatting is stable' -ForegroundColor Cyan
    Invoke-ExternalChecked -FilePath 'dotnet' -ArgumentList @(
        'format',
        $Solution,
        '--verify-no-changes',
        '--no-restore'
    )
}
else {
    Write-Host 'Stage 4/4 - dotnet format verification skipped by request' -ForegroundColor Yellow
}

Write-Host ''
Write-Host 'S-priority readability fix completed successfully.' -ForegroundColor Green
