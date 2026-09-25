# Purpose: Runs Verify Universal QA as part of repository development and verification workflows.
[CmdletBinding()]
param([string[]]$RequireLanguages=@('python','csharp','cpp','javascript','customlang'))
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out=Join-Path $root 'artifacts\universal-qa-verification';New-Item -ItemType Directory -Force -Path $out|Out-Null
$required=@('src\Dip.Domain\ValueObjects\LanguageId.cs','src\Dip.Infrastructure\QualityAutomation\QaAdapterRegistry.cs','src\Dip.Infrastructure\QualityAutomation\RepositoryTechnologyDetector.cs','src\Dip.Infrastructure\QualityAutomation\UniversalDiagnosticNormalizer.cs','src\Dip.Infrastructure\QualityAutomation\ManifestQaAdapter.cs','src\Dip.Infrastructure\QualityAutomation\GenericCommandQaAdapter.cs','src\Dip.Infrastructure\QualityAutomation\UnityProjectQaAdapter.cs')
foreach($f in $required){if(-not(Test-Path (Join-Path $root $f))){throw "Missing universal QA source: $f"}}
if(Test-Path (Join-Path $root 'src\Dip.Domain\Enums\QaLanguage.cs')){throw 'Legacy QaLanguage enum still exists.'}
& (Join-Path $PSScriptRoot 'Invoke-QaBugZoo.ps1') -RequireLanguages $RequireLanguages
if($LASTEXITCODE-ne0){throw 'Universal QA Bug Zoo failed.'}
$sha=(& git -C $root rev-parse HEAD|Out-String).Trim()
$evidence=Get-Content (Join-Path $root 'artifacts\qa-bug-zoo\qa-bug-zoo-results.json') -Raw|ConvertFrom-Json
if(@($evidence.missingRequiredLanguages).Count-ne0){throw "Missing required QA languages: $($evidence.missingRequiredLanguages -join ', ')"}
@("UNIVERSAL QA VERIFIED","Commit=$sha","Languages=$($evidence.executedLanguages -join ',')","Recall=$($evidence.detectionRecallPercent)","FalsePositiveRate=$($evidence.falsePositiveRatePercent)","ReproductionRate=$($evidence.reproductionRatePercent)")|Set-Content (Join-Path $out 'UNIVERSAL_QA_VERIFICATION_PASS.txt') -Encoding UTF8
Write-Host 'Universal QA verification PASS.' -ForegroundColor Green
