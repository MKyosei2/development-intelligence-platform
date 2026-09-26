# Purpose: Runs the QA Bug Zoo and preserves per-case diagnostic evidence even when the benchmark fails.
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
    $exitCode = $LASTEXITCODE

    $json = Join-Path $OutputDirectory 'qa-bug-zoo-results.json'
    $markdown = Join-Path $OutputDirectory 'qa-bug-zoo-results.md'

    if (-not (Test-Path -LiteralPath $json)) { throw "Bug Zoo JSON evidence was not generated: $json" }
    if (-not (Test-Path -LiteralPath $markdown)) { throw "Bug Zoo Markdown evidence was not generated: $markdown" }

    $result = Get-Content -LiteralPath $json -Raw | ConvertFrom-Json
    Write-Host "Bug Zoo evidence: $json"
    Write-Host ("Recall={0}% FalsePositive={1}% Reproduction={2}%" -f
        $result.detectionRecallPercent,
        $result.falsePositiveRatePercent,
        $result.reproductionRatePercent)

    if (@($result.missingRequiredLanguages).Count -gt 0) {
        Write-Host ("Missing required languages: " + (@($result.missingRequiredLanguages) -join ', ')) -ForegroundColor Red
    }

    foreach ($case in @($result.results)) {
        if ($case.infrastructureFailure -or -not $case.detected -or ($case.mustReproduce -and -not $case.reproduced)) {
            Write-Host ("[BUG-ZOO] {0}/{1} adapter={2} detected={3} reproduced={4} infra={5}" -f
                $case.language,$case.name,$case.adapter,$case.detected,$case.reproduced,$case.infrastructureFailure) -ForegroundColor Yellow
            if ($case.error) { Write-Host ("  Error=" + $case.error) -ForegroundColor DarkYellow }
            foreach ($tool in @($case.tools)) {
                Write-Host ("  Tool={0} available={1} success={2} exit={3}" -f
                    $tool.tool,$tool.wasAvailable,$tool.succeeded,$tool.exitCode)
            }
        }
    }

    if ($exitCode -ne 0) {
        throw "DIP QA Bug Zoo failed with exit code $exitCode. Detailed JSON evidence: $json"
    }

    Write-Host "DIP QA Bug Zoo passed: $json" -ForegroundColor Green
}
finally { Pop-Location }
