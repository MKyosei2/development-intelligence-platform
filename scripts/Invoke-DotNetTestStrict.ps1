# Purpose: Runs dotnet test fail-closed. ExitCode=0 with zero executed tests is a failure.
[CmdletBinding()]
param(
    [string]$Project = '',
    [string]$Configuration = 'Release',
    [string]$Filter = '',
    [string]$ResultsDirectory = '',
    [string]$Name = '',
    [switch]$NoBuild,
    [int]$MinimumExecuted = 1
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-DipTrxCounter {
    param(
        [Parameter(Mandatory=$true)]$Counters,
        [Parameter(Mandatory=$true)][string]$Name
    )

    $attribute = $Counters.Attributes[$Name]
    if ($null -eq $attribute -or [string]::IsNullOrWhiteSpace($attribute.Value)) {
        return 0
    }

    return [int]$attribute.Value
}

function Invoke-DipDotNetTestStrict {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$Project,
        [string]$Configuration = 'Release',
        [string]$Filter = '',
        [string]$ResultsDirectory = '',
        [string]$Name = '',
        [switch]$NoBuild,
        [int]$MinimumExecuted = 1
    )

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required.'
    }

    if ([string]::IsNullOrWhiteSpace($Name)) {
        $Name = [IO.Path]::GetFileNameWithoutExtension($Project)
    }

    if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
        $ResultsDirectory = Join-Path (Get-Location).Path 'artifacts\strict-test-results'
    }

    New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null

    $safeName = ($Name -replace '[^A-Za-z0-9._-]', '_')
    $trxName = "$safeName.trx"
    $trxPath = Join-Path $ResultsDirectory $trxName
    Remove-Item -LiteralPath $trxPath -Force -ErrorAction SilentlyContinue

    $args = @(
        'test',
        $Project,
        '--configuration',
        $Configuration,
        '--logger',
        "trx;LogFileName=$trxName",
        '--results-directory',
        $ResultsDirectory
    )

    if ($NoBuild) {
        $args += '--no-build'
    }

    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $args += @('--filter', $Filter)
    }

    & dotnet @args
    $exitCode = $LASTEXITCODE

    if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
        throw "Strict test '$Name' produced no TRX evidence. ExitCode=$exitCode"
    }

    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $counters = $trx.SelectSingleNode("//*[local-name()='Counters']")

    if ($null -eq $counters) {
        throw "Strict test '$Name' TRX has no ResultSummary/Counters node."
    }

    $total = Get-DipTrxCounter $counters 'total'
    $executed = Get-DipTrxCounter $counters 'executed'
    $passed = Get-DipTrxCounter $counters 'passed'
    $failed = Get-DipTrxCounter $counters 'failed'
    $error = Get-DipTrxCounter $counters 'error'
    $timeout = Get-DipTrxCounter $counters 'timeout'
    $aborted = Get-DipTrxCounter $counters 'aborted'
    $notExecuted = Get-DipTrxCounter $counters 'notExecuted'

    $summary = [ordered]@{
        name = $Name
        project = $Project
        filter = $Filter
        exitCode = $exitCode
        total = $total
        executed = $executed
        passed = $passed
        failed = $failed
        error = $error
        timeout = $timeout
        aborted = $aborted
        notExecuted = $notExecuted
        minimumExecuted = $MinimumExecuted
        trx = $trxPath
    }

    $summaryPath = [IO.Path]::ChangeExtension($trxPath, '.strict.json')
    $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

    if ($exitCode -ne 0) {
        throw "Strict test '$Name' failed: dotnet test exit code $exitCode."
    }

    if ($executed -lt $MinimumExecuted) {
        throw "Strict test '$Name' executed $executed test(s); minimum is $MinimumExecuted. Zero-test PASS is forbidden."
    }

    if ($failed -gt 0 -or $error -gt 0 -or $timeout -gt 0 -or $aborted -gt 0) {
        throw "Strict test '$Name' is not clean: failed=$failed error=$error timeout=$timeout aborted=$aborted."
    }

    Write-Host "STRICT TEST PASS: $Name executed=$executed passed=$passed" -ForegroundColor Green
    return [pscustomobject]$summary
}

# Execute only when the script is called directly, not when dot-sourced.
if ($MyInvocation.InvocationName -ne '.') {
    if ([string]::IsNullOrWhiteSpace($Project)) {
        throw '-Project is required when Invoke-DotNetTestStrict.ps1 is executed directly.'
    }

    Invoke-DipDotNetTestStrict `
        -Project $Project `
        -Configuration $Configuration `
        -Filter $Filter `
        -ResultsDirectory $ResultsDirectory `
        -Name $Name `
        -NoBuild:$NoBuild `
        -MinimumExecuted $MinimumExecuted |
    Out-Null
}
