# Purpose: Automates the V er if y-S el fH os te dW in do ws Pr ef li gh t repository workflow.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Find-PostgresBin {
    $psql = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($psql) { return Split-Path -Parent $psql.Source }

    $roots = @()
    if ($env:ProgramFiles) { $roots += (Join-Path $env:ProgramFiles 'PostgreSQL') }
    $pf86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    if ($pf86) { $roots += (Join-Path $pf86 'PostgreSQL') }

    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        foreach ($d in (Get-ChildItem $root -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending)) {
            $bin = Join-Path $d.FullName 'bin'
            if (
                (Test-Path (Join-Path $bin 'psql.exe')) -and
                (Test-Path (Join-Path $bin 'pg_isready.exe'))
            ) { return $bin }
        }
    }
    return $null
}

Write-Host "Runner: $env:RUNNER_NAME"
Write-Host "OS: $env:RUNNER_OS"
Write-Host "Workspace: $env:GITHUB_WORKSPACE"
Write-Host "SHA: $env:GITHUB_SHA"

if ($env:RUNNER_OS -ne 'Windows') { throw "Expected Windows runner, got '$env:RUNNER_OS'." }
if ($env:RUNNER_NAME -ne 'DIP-Windows-Fallback') { throw "Unexpected runner '$env:RUNNER_NAME'." }

$required = @('git','dotnet','python','node','npm')
foreach ($cmd in $required) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        throw "Required command '$cmd' is missing after setup actions."
    }
}

$pgBin = Find-PostgresBin
if (-not $pgBin) {
    throw 'PostgreSQL client tools were not found. Install PostgreSQL 16+ on the fallback PC.'
}
$env:PATH = "$pgBin;$env:PATH"
"POSTGRES_BIN=$pgBin" | Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append
$pgBin | Out-File -FilePath $env:GITHUB_PATH -Encoding utf8 -Append

$service = Get-Service -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'postgresql*' } |
    Sort-Object Name -Descending |
    Select-Object -First 1

if ($service -and $service.Status -ne 'Running') {
    try { Start-Service $service.Name } catch { }
}

& pg_isready -h $env:POSTGRES_HOST -p $env:POSTGRES_PORT -U $env:POSTGRES_USER -d postgres
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL is not ready.' }

if (-not $env:POSTGRES_PASSWORD) {
    throw 'DIP_SELFHOSTED_POSTGRES_PASSWORD GitHub secret is missing.'
}

Write-Host 'PASS: Windows self-hosted runner preflight.'
