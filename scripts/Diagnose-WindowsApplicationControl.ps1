# Purpose: Collects Windows Application Control / Code Integrity evidence without changing system policy.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = '',
    [int]$LookbackHours = 6,
    [string[]]$Paths = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}
else {
    $RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
}

$out = Join-Path $RepositoryRoot 'artifacts\windows-application-control'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$summary = Join-Path $out 'SUMMARY.txt'
$eventsText = Join-Path $out 'code-integrity-events.txt'
$eventsJson = Join-Path $out 'code-integrity-events.json'
$assembliesText = Join-Path $out 'assemblies.txt'

Remove-Item `
    $summary, $eventsText, $eventsJson, $assembliesText `
    -Force `
    -ErrorAction SilentlyContinue

$start = (Get-Date).AddHours(-1 * [Math]::Max(1, $LookbackHours))
$events = @()

try {
    $events = @(
        Get-WinEvent `
            -FilterHashtable @{
                LogName = 'Microsoft-Windows-CodeIntegrity/Operational'
                StartTime = $start
            } `
            -ErrorAction Stop |
        Select-Object TimeCreated, Id, LevelDisplayName, Message
    )

    $events |
        Format-List |
        Out-String |
        Set-Content -LiteralPath $eventsText -Encoding UTF8

    $events |
        ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath $eventsJson -Encoding UTF8
}
catch {
    @(
        'Code Integrity event query was unavailable.'
        $_.Exception.Message
    ) |
        Set-Content -LiteralPath $eventsText -Encoding UTF8
}

if ($Paths.Count -eq 0) {
    $Paths = @(
        (Join-Path $RepositoryRoot 'src\Dip.Infrastructure\bin\Release\net8.0\Dip.Infrastructure.dll'),
        (Join-Path $RepositoryRoot 'tests\Dip.Infrastructure.Tests\bin\Release\net8.0\Dip.Infrastructure.dll'),
        (Join-Path $RepositoryRoot 'collectors\native-collector')
    )
}

$assemblyRows = [System.Collections.Generic.List[object]]::new()

foreach ($candidate in $Paths) {
    if (-not (Test-Path -LiteralPath $candidate)) {
        continue
    }

    $files =
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            Get-ChildItem -LiteralPath $candidate -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Extension -in @('.dll','.exe') }
        }
        else {
            @(Get-Item -LiteralPath $candidate -ErrorAction Stop)
        }

    foreach ($file in $files) {
        $zone = '<not present>'

        try {
            $zone = (
                Get-Content `
                    -LiteralPath $file.FullName `
                    -Stream Zone.Identifier `
                    -ErrorAction Stop |
                Out-String
            ).Trim()
        }
        catch {
        }

        $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
        $hash = Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256

        $assemblyRows.Add(
            [pscustomobject]@{
                Path = $file.FullName
                SHA256 = $hash.Hash
                SignatureStatus = [string]$signature.Status
                Signer = if ($signature.SignerCertificate) {
                    $signature.SignerCertificate.Subject
                } else {
                    ''
                }
                ZoneIdentifier = $zone
            }
        ) | Out-Null
    }
}

$assemblyRows |
    Format-List |
    Out-String |
    Set-Content -LiteralPath $assembliesText -Encoding UTF8

$matched = @(
    $events |
        Where-Object {
            $_.Message -and
            (
                $_.Message -match 'blocked' -or
                $_.Message -match '0x800711C7' -or
                $_.Message -match 'policy'
            )
        }
)

$status =
    if ($matched.Count -gt 0) {
        'ENVIRONMENT_BLOCKED'
    }
    else {
        'NO_CONFIRMED_BLOCK_IN_EVENT_WINDOW'
    }

@(
    "STATUS=$status"
    "LOOKBACK_HOURS=$LookbackHours"
    "MATCHING_EVENTS=$($matched.Count)"
    "TOTAL_CODE_INTEGRITY_EVENTS=$($events.Count)"
    "ASSEMBLIES_INSPECTED=$($assemblyRows.Count)"
    "EVENTS_TEXT=$eventsText"
    "EVENTS_JSON=$eventsJson"
    "ASSEMBLIES=$assembliesText"
    'NOTE=This diagnostic does not disable or modify WDAC/App Control policy.'
) |
    Set-Content -LiteralPath $summary -Encoding UTF8

Write-Host "Windows Application Control diagnostic: $status" -ForegroundColor Cyan
Write-Host "Summary: $summary" -ForegroundColor Cyan
