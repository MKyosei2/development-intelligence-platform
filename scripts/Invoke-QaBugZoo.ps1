# Purpose: Automates Invoke QA Bug Zoo for repository development and verification workflows.
[CmdletBinding()]
param(
    [switch]$AllowMissingTools,
    [string]$OutputDirectory,
    [string[]]$RequireLanguages = @('python','csharp','cpp','javascript','customlang')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts\qa-bug-zoo' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet SDK is required to run the DIP QA Bug Zoo.' }

$project = Join-Path $root 'tools\Dip.QaBugZooVerifier\Dip.QaBugZooVerifier.csproj'
if (-not (Test-Path -LiteralPath $project)) { throw "Bug Zoo verifier project is missing: $project" }

$args = @('run', '--project', $project, '--configuration', 'Release', '--', '--root', $root, '--output', $OutputDirectory)
if ($AllowMissingTools) { $args += '--allow-missing-tools' }
if ($RequireLanguages.Count -gt 0) { $args += @('--require-languages', ($RequireLanguages -join ',')) }

Push-Location $root
try {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "DIP QA Bug Zoo failed with exit code $LASTEXITCODE." }
    $json = Join-Path $OutputDirectory 'qa-bug-zoo-results.json'
    $markdown = Join-Path $OutputDirectory 'qa-bug-zoo-results.md'
    if (-not (Test-Path -LiteralPath $json)) { throw "Bug Zoo JSON evidence was not generated: $json" }
    if (-not (Test-Path -LiteralPath $markdown)) { throw "Bug Zoo Markdown evidence was not generated: $markdown" }
    Write-Host "DIP QA Bug Zoo passed: $json" -ForegroundColor Green
}
finally { Pop-Location }
