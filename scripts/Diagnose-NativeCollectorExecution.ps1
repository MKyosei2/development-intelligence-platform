# Purpose: Diagnoses native collector test-process startup failures without changing native source behavior.
[CmdletBinding()]
param(
    [string]$ExecutablePath = '',
    [string]$BuildDirectory = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root =
    (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

$out =
    Join-Path `
        $root `
        'artifacts\native-execution-diagnostic'

New-Item `
    -ItemType Directory `
    -Force `
    -Path $out |
    Out-Null

if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $searchRoot =
        if (
            -not [string]::IsNullOrWhiteSpace($BuildDirectory) `
            -and (Test-Path -LiteralPath $BuildDirectory -PathType Container)
        ) {
            (Resolve-Path -LiteralPath $BuildDirectory).Path
        }
        else {
            Join-Path $root 'collectors\native-collector'
        }

    $candidates =
        @(
            Get-ChildItem `
                -LiteralPath $searchRoot `
                -Recurse `
                -File `
                -Filter 'dip-native-tests.exe' `
                -ErrorAction SilentlyContinue |
            Sort-Object `
                LastWriteTimeUtc `
                -Descending
        )

    if ($candidates.Count -eq 0) {
        throw (
            'dip-native-tests.exe was not found. Build the native collector first.'
        )
    }

    $ExecutablePath =
        $candidates[0].FullName
}

$ExecutablePath =
    (Resolve-Path -LiteralPath $ExecutablePath).Path

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "Native test executable was not found: $ExecutablePath"
}

if ([string]::IsNullOrWhiteSpace($BuildDirectory)) {
    $candidateBuild =
        Split-Path `
            -Parent `
            $ExecutablePath

    if (
        (Split-Path -Leaf $candidateBuild) `
            -in @('Debug', 'Release', 'RelWithDebInfo', 'MinSizeRel')
    ) {
        $candidateBuild =
            Split-Path `
                -Parent `
                $candidateBuild
    }

    $BuildDirectory =
        $candidateBuild
}

$startedAt =
    Get-Date

$stdout =
    Join-Path $out 'direct-stdout.txt'

$stderr =
    Join-Path $out 'direct-stderr.txt'

Remove-Item `
    $stdout, $stderr `
    -Force `
    -ErrorAction SilentlyContinue

$fileHash =
    Get-FileHash `
        -LiteralPath $ExecutablePath `
        -Algorithm SHA256

$signature =
    Get-AuthenticodeSignature `
        -LiteralPath $ExecutablePath

$zoneIdentifier =
    $null

try {
    $zoneIdentifier =
        (
            Get-Content `
                -LiteralPath $ExecutablePath `
                -Stream Zone.Identifier `
                -ErrorAction Stop |
            Out-String
        ).Trim()
}
catch {
    $zoneIdentifier =
        '<not present>'
}

$direct = [ordered]@{
    started = $false
    exitCode = $null
    exceptionType = $null
    exceptionMessage = $null
    nativeErrorCode = $null
    hResult = $null
}

try {
    $process =
        Start-Process `
            -FilePath $ExecutablePath `
            -WorkingDirectory (
                Split-Path -Parent $ExecutablePath
            ) `
            -RedirectStandardOutput $stdout `
            -RedirectStandardError $stderr `
            -NoNewWindow `
            -Wait `
            -PassThru

    $direct.started =
        $true

    $direct.exitCode =
        $process.ExitCode
}
catch {
    $exception =
        $_.Exception

    $direct.exceptionType =
        $exception.GetType().FullName

    $direct.exceptionMessage =
        $exception.Message

    $direct.hResult =
        $exception.HResult

    if ($exception -is [System.ComponentModel.Win32Exception]) {
        $direct.nativeErrorCode =
            $exception.NativeErrorCode
    }
}

$codeIntegrityText =
    Join-Path `
        $out `
        'code-integrity-events.txt'

$codeIntegrityJson =
    Join-Path `
        $out `
        'code-integrity-events.json'

try {
    $events =
        @(
            Get-WinEvent `
                -FilterHashtable @{
                    LogName =
                        'Microsoft-Windows-CodeIntegrity/Operational'
                    StartTime =
                        $startedAt.AddMinutes(-2)
                } `
                -ErrorAction Stop |
            Where-Object {
                $_.Message -and
                $_.Message.IndexOf(
                    [IO.Path]::GetFileName($ExecutablePath),
                    [StringComparison]::OrdinalIgnoreCase) -ge 0
            } |
            Select-Object `
                TimeCreated,
                Id,
                LevelDisplayName,
                Message
        )

    $events |
        Format-List |
        Out-String |
        Set-Content `
            -LiteralPath $codeIntegrityText `
            -Encoding UTF8

    $events |
        ConvertTo-Json -Depth 5 |
        Set-Content `
            -LiteralPath $codeIntegrityJson `
            -Encoding UTF8
}
catch {
    @(
        'Code Integrity event query was unavailable.'
        $_.Exception.Message
    ) |
        Set-Content `
            -LiteralPath $codeIntegrityText `
            -Encoding UTF8
}

$ctest = [ordered]@{
    available = $false
    buildDirectory = $BuildDirectory
    started = $false
    exitCode = $null
    exception = $null
}

$ctestCommand =
    Get-Command `
        ctest `
        -ErrorAction SilentlyContinue

if (
    $ctestCommand `
    -and
    (Test-Path -LiteralPath $BuildDirectory -PathType Container)
) {
    $ctest.available =
        $true

    $ctestStdout =
        Join-Path $out 'ctest-stdout.txt'

    $ctestStderr =
        Join-Path $out 'ctest-stderr.txt'

    try {
        $quotedBuild =
            '"' +
            $BuildDirectory.Replace('"', '\"') +
            '"'

        $ctestProcess =
            Start-Process `
                -FilePath $ctestCommand.Path `
                -ArgumentList @(
                    '--test-dir',
                    $quotedBuild,
                    '-C',
                    'Release',
                    '-VV',
                    '--output-on-failure'
                ) `
                -WorkingDirectory $root `
                -RedirectStandardOutput $ctestStdout `
                -RedirectStandardError $ctestStderr `
                -NoNewWindow `
                -Wait `
                -PassThru

        $ctest.started =
            $true

        $ctest.exitCode =
            $ctestProcess.ExitCode
    }
    catch {
        $ctest.exception =
            $_.Exception.ToString()
    }
}

$diagnostic =
    [ordered]@{
        schema =
            'dip-native-execution-diagnostic-v1'
        generatedAtUtc =
            [DateTimeOffset]::UtcNow.ToString('o')
        executable =
            $ExecutablePath
        buildDirectory =
            $BuildDirectory
        sha256 =
            $fileHash.Hash
        authenticode = [ordered]@{
            status =
                [string]$signature.Status
            statusMessage =
                $signature.StatusMessage
            signerSubject =
                $(if ($signature.SignerCertificate) {
                    $signature.SignerCertificate.Subject
                }
                else {
                    $null
                })
        }
        zoneIdentifier =
            $zoneIdentifier
        directExecution =
            $direct
        ctest =
            $ctest
        codeIntegrityText =
            $codeIntegrityText
    }

$jsonPath =
    Join-Path `
        $out `
        'native-execution-diagnostic.json'

$diagnostic |
    ConvertTo-Json -Depth 8 |
    Set-Content `
        -LiteralPath $jsonPath `
        -Encoding UTF8

@(
    'NATIVE EXECUTION DIAGNOSTIC COMPLETE'
    "Executable=$ExecutablePath"
    "DirectStarted=$($direct.started)"
    "DirectExitCode=$($direct.exitCode)"
    "NativeErrorCode=$($direct.nativeErrorCode)"
    "AuthenticodeStatus=$($signature.Status)"
    "CTestStarted=$($ctest.started)"
    "CTestExitCode=$($ctest.exitCode)"
    "Evidence=$jsonPath"
) |
    Set-Content `
        -LiteralPath (
            Join-Path $out 'NATIVE_EXECUTION_DIAGNOSTIC.txt'
        ) `
        -Encoding UTF8

Write-Host (
    "Native execution diagnostic complete: $jsonPath"
) -ForegroundColor Green
