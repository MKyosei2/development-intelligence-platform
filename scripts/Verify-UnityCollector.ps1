# Purpose: Verifies U ni ty Co ll ec to r requirements for DIP.
[CmdletBinding()]
param(
    [string]$UnityPath = $env:UNITY_EDITOR_PATH,
    [string]$ExpectedSha = '',
    [switch]$StaticOnly,
    [switch]$KeepTemporaryProject
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$package = Join-Path $root 'collectors\unity-collector\Packages\com.mkyosei.dip.collector'
$out = Join-Path $root 'artifacts\unity-verification'
$tempProject = Join-Path $out 'temporary-project'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$required = @(
    'Editor\Analysis\BuildSettingsAnalyzer.cs',
    'Editor\Analysis\SceneAnalyzer.cs',
    'Editor\Analysis\AnimationAnalyzer.cs',
    'Runtime\PerformanceProbe.cs',
    'Editor\Cli\UnityCollectorCli.cs',
    'Editor\Cli\UnityCollectorExactRevisionCli.cs',
    'Editor\Models\CollectorModels.cs',
    'Tests\Editor\BuildSettingsAnalyzerTests.cs',
    'Tests\Editor\AnimationAnalyzerTests.cs',
    'Tests\Runtime\PerformanceProbeTests.cs',
    'Editor\Dip.UnityCollector.Editor.asmdef',
    'Runtime\Dip.UnityCollector.Runtime.asmdef'
)

foreach ($relative in $required) {
    $path = Join-Path $package $relative
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Unity Collector required file is missing: $relative"
    }
    if ((Get-Item -LiteralPath $path).Length -eq 0) {
        throw "Unity Collector file is empty: $relative"
    }
}

$emptySources = @(
    Get-ChildItem -LiteralPath $package -Recurse -File -Filter '*.cs' |
        Where-Object { $_.Length -eq 0 }
)
if ($emptySources.Count -gt 0) {
    throw "Unity Collector contains empty C# source files: $($emptySources.FullName -join ', ')"
}

$sourceSha = (& git -C $root rev-parse HEAD | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceSha -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'Could not resolve the exact DIP source SHA.'
}

$sourceBranch = (& git -C $root branch --show-current | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the DIP branch.' }
if ([string]::IsNullOrWhiteSpace($sourceBranch)) { $sourceBranch = 'detached' }

if ($ExpectedSha -and $sourceSha -ne $ExpectedSha) {
    throw "Exact SHA mismatch. expected=$ExpectedSha actual=$sourceSha"
}

$staticEvidence = Join-Path $out 'UNITY_COLLECTOR_STATIC_PASS.txt'
@(
    'STATUS=PASS',
    "Timestamp=$([DateTimeOffset]::UtcNow.ToString('o'))",
    "SourceCommit=$sourceSha",
    "SourceBranch=$sourceBranch",
    'Checks=required sources; no empty C# sources; exact-revision CLI present'
) | Set-Content -LiteralPath $staticEvidence -Encoding UTF8

if ($StaticOnly) {
    Write-Host "Unity Collector static verification passed: $staticEvidence" -ForegroundColor Green
    return
}

$trackedDirty = (& git -C $root status --porcelain --untracked-files=no | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the DIP working tree.' }
if ($trackedDirty) {
    throw 'Unity runtime verification requires a clean tracked DIP working tree.'
}

if (-not $UnityPath) {
    throw 'UnityPath was not supplied. Set -UnityPath or UNITY_EDITOR_PATH.'
}
$UnityPath = (Resolve-Path -LiteralPath $UnityPath).Path
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity executable not found: $UnityPath"
}

if (Test-Path -LiteralPath $tempProject) {
    Remove-Item -LiteralPath $tempProject -Recurse -Force
}
New-Item -ItemType Directory -Force -Path (Join-Path $tempProject 'Assets') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $tempProject 'Packages') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $tempProject 'ProjectSettings') | Out-Null

$packageUri = $package.Replace('\','/')
$manifest = @{
    dependencies = @{
        'com.mkyosei.dip.collector' = "file:$packageUri"
        'com.unity.test-framework' = '1.1.33'
    }
} | ConvertTo-Json -Depth 5
$manifest | Set-Content -LiteralPath (Join-Path $tempProject 'Packages\manifest.json') -Encoding UTF8

$versionOutput = (& $UnityPath -version 2>&1 | Out-String)
$versionMatch = [regex]::Match($versionOutput, '\d{4}\.\d+\.\d+[abfp]\d+')
$unityVersion = if ($versionMatch.Success) { $versionMatch.Value } else { 'unknown' }
if ($unityVersion -eq 'unknown') {
    throw "Could not resolve Unity version from executable: $UnityPath"
}
@(
    "m_EditorVersion: $unityVersion",
    'm_EditorVersionWithRevision:'
) | Set-Content -LiteralPath (Join-Path $tempProject 'ProjectSettings\ProjectVersion.txt') -Encoding UTF8

$testXml = Join-Path $out 'editmode-results.xml'
$testLog = Join-Path $out 'editmode-unity.log'
& $UnityPath `
    -batchmode `
    -nographics `
    -projectPath $tempProject `
    -runTests `
    -testPlatform EditMode `
    -testResults $testXml `
    -logFile $testLog
if ($LASTEXITCODE -ne 0) {
    throw "Unity EditMode tests failed with exit code $LASTEXITCODE. See $testLog"
}
if (-not (Test-Path -LiteralPath $testXml -PathType Leaf)) {
    throw "Unity test result XML was not generated: $testXml"
}

$report = Join-Path $out 'unity-collector-report.json'
$collectorLog = Join-Path $out 'collector-unity.log'
$externalRunId = "unity-dip-$($sourceSha.Substring(0,12))-$([guid]::NewGuid().ToString('N'))"

& $UnityPath `
    -batchmode `
    -nographics `
    -quit `
    -projectPath $tempProject `
    -executeMethod Dip.UnityCollector.Editor.Cli.UnityCollectorExactRevisionCli.Run `
    -dipProjectId '00000000-0000-0000-0000-000000000001' `
    -dipRepository 'https://github.com/MKyosei2/development-intelligence-platform' `
    -dipCommitSha $sourceSha `
    -dipBranch $sourceBranch `
    -dipExternalRunId $externalRunId `
    -dipAttemptNumber '1' `
    -dipQualityGate 'critical' `
    -dipOutput $report `
    -logFile $collectorLog
if ($LASTEXITCODE -ne 0) {
    throw "Unity exact-revision collector failed with exit code $LASTEXITCODE. See $collectorLog"
}
if (-not (Test-Path -LiteralPath $report -PathType Leaf)) {
    throw "Unity Collector report was not generated: $report"
}

$reportObject = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if ($reportObject.commitSha -ne $sourceSha) {
    throw "Unity report SHA mismatch. expected=$sourceSha actual=$($reportObject.commitSha)"
}
if ($reportObject.repository -ne 'https://github.com/MKyosei2/development-intelligence-platform') {
    throw 'Unity report repository identity is incorrect.'
}
if ($reportObject.externalRunId -ne $externalRunId) {
    throw 'Unity report externalRunId does not match the invoked run.'
}
if ([int]$reportObject.attemptNumber -ne 1) {
    throw 'Unity report attemptNumber is invalid.'
}
if ($reportObject.status -notin @('Succeeded','PartialSuccess')) {
    throw "Unity report status is not publishable runtime evidence: $($reportObject.status)"
}

foreach ($metric in @(
    'build_scene_count',
    'animation_clip_count',
    'scene_count_analyzed',
    'collector_issue_critical_count'
)) {
    if (-not @($reportObject.metrics | Where-Object { $_.name -eq $metric })) {
        throw "Unity Collector report is missing expected metric '$metric'."
    }
}

$pass = Join-Path $out 'UNITY_COLLECTOR_VERIFICATION_PASS.txt'
@(
    'STATUS=PASS',
    "Timestamp=$([DateTimeOffset]::UtcNow.ToString('o'))",
    "SourceCommit=$sourceSha",
    "SourceBranch=$sourceBranch",
    "UnityVersion=$unityVersion",
    "ExternalRunId=$externalRunId",
    "CollectorStatus=$($reportObject.status)",
    'QualityGate=critical',
    "TestResults=$testXml",
    "CollectorReport=$report"
) | Set-Content -LiteralPath $pass -Encoding UTF8

Write-Host "Unity exact-revision verification passed: $pass" -ForegroundColor Green

if (-not $KeepTemporaryProject) {
    Remove-Item -LiteralPath $tempProject -Recurse -Force -ErrorAction SilentlyContinue
}
