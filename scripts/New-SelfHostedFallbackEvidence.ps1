# Purpose: Defines New Self Hosted Fallback Evidence responsibilities for this repository area.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $root 'artifacts\self-hosted-fallback'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$passed = ($env:DIP_JOB_STATUS -eq 'success')
$payload = [ordered]@{
    schema = 'dip-self-hosted-fallback-v1'
    verificationMode = 'self-hosted-windows-fallback'
    sourceCommit = $env:GITHUB_SHA
    expectedCommit = $env:DIP_EXPECTED_SHA
    runId = $env:GITHUB_RUN_ID
    runAttempt = $env:GITHUB_RUN_ATTEMPT
    runner = [ordered]@{
        name = $env:RUNNER_NAME
        os = $env:RUNNER_OS
        architecture = $env:RUNNER_ARCH
        label = 'dip-fallback'
    }
    requested = [ordered]@{
        gui = ($env:DIP_RUN_GUI -eq 'true')
        benchmark = ($env:DIP_RUN_BENCHMARK -eq 'true')
    }
    jobStatus = $env:DIP_JOB_STATUS
    passed = $passed
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    note = 'Emergency fallback evidence. This does not claim GitHub-hosted verification.'
}

$payload | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $out 'evidence.json') -Encoding UTF8

@"
# Self-Hosted Windows Fallback Evidence

- Source commit: `$($payload.sourceCommit)`
- Verification mode: **self-hosted-windows-fallback**
- Runner: `$($payload.runner.name)`
- OS: `$($payload.runner.os)`
- Job status: **$($payload.jobStatus)**
- Passed: **$($payload.passed)**
- GUI requested: `$($payload.requested.gui)`
- Benchmark requested: `$($payload.requested.benchmark)`

This is explicit emergency fallback evidence and does not claim GitHub-hosted runner verification.
"@ | Set-Content -LiteralPath (Join-Path $out 'evidence.md') -Encoding UTF8
