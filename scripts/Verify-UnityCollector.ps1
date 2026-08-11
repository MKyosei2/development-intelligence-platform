[CmdletBinding()]
param(
    [string]$UnityPath = $env:UNITY_EDITOR_PATH,
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
    'Tests\Editor\BuildSettingsAnalyzerTests.cs',
    'Tests\Editor\AnimationAnalyzerTests.cs',
    'Tests\Runtime\PerformanceProbeTests.cs',
    'Editor\Dip.UnityCollector.Editor.asmdef',
    'Runtime\Dip.UnityCollector.Runtime.asmdef'
)
foreach ($relative in $required) {
    $path = Join-Path $package $relative
    if (-not (Test-Path -LiteralPath $path)) { throw "Unity Collector required file is missing: $relative" }
    if ((Get-Item -LiteralPath $path).Length -eq 0) { throw "Unity Collector file is empty: $relative" }
}

$emptySources = @(Get-ChildItem -LiteralPath $package -Recurse -File -Filter '*.cs' | Where-Object { $_.Length -eq 0 })
if ($emptySources.Count -gt 0) {
    throw "Unity Collector still contains empty C# source files: $($emptySources.FullName -join ', ')"
}

$staticEvidence = Join-Path $out 'UNITY_COLLECTOR_STATIC_PASS.txt'
@(
    'STATUS=PASS',
    "Timestamp=$([DateTimeOffset]::UtcNow.ToString('u'))",
    'Checks=required source files; no empty C# sources; test assemblies present'
) | Set-Content -LiteralPath $staticEvidence -Encoding UTF8

if ($StaticOnly) {
    Write-Host "Unity Collector static verification passed: $staticEvidence" -ForegroundColor Green
    return
}

if (-not $UnityPath) { throw 'UnityPath was not supplied. Set -UnityPath or UNITY_EDITOR_PATH, or use -StaticOnly for structural verification only.' }
$UnityPath = (Resolve-Path -LiteralPath $UnityPath).Path
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable not found: $UnityPath" }

if (Test-Path -LiteralPath $tempProject) { Remove-Item -LiteralPath $tempProject -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $tempProject 'Assets') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $tempProject 'Packages') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $tempProject 'ProjectSettings') | Out-Null

$packageUri = $package.Replace('\\','/')
$manifest = @{
    dependencies = @{
        'com.mkyosei.dip.collector' = "file:$packageUri"
        'com.unity.test-framework' = '1.1.33'
    }
} | ConvertTo-Json -Depth 5
$manifest | Set-Content -LiteralPath (Join-Path $tempProject 'Packages\manifest.json') -Encoding UTF8

$versionOutput = (& $UnityPath -version 2>&1 | Out-String)
$versionMatch = [regex]::Match($versionOutput, '\d{4}\.\d+\.\d+[abfp]\d+')
$unityVersion = if ($versionMatch.Success) { $versionMatch.Value } else { '2022.3.0f1' }
@("m_EditorVersion: $unityVersion", 'm_EditorVersionWithRevision:') | Set-Content -LiteralPath (Join-Path $tempProject 'ProjectSettings\ProjectVersion.txt') -Encoding UTF8

$testXml = Join-Path $out 'editmode-results.xml'
$testLog = Join-Path $out 'editmode-unity.log'
& $UnityPath -batchmode -nographics -projectPath $tempProject -runTests -testPlatform EditMode -testResults $testXml -logFile $testLog
if ($LASTEXITCODE -ne 0) { throw "Unity EditMode tests failed with exit code $LASTEXITCODE. See $testLog" }
if (-not (Test-Path -LiteralPath $testXml)) { throw "Unity test result XML was not generated: $testXml" }

$report = Join-Path $out 'unity-collector-report.json'
$collectorLog = Join-Path $out 'collector-unity.log'
& $UnityPath -batchmode -nographics -quit -projectPath $tempProject `
    -executeMethod Dip.UnityCollector.Editor.Cli.UnityCollectorCli.Run `
    -dipProjectId '00000000-0000-0000-0000-000000000001' `
    -dipCommitSha 'unity-verification' `
    -dipBranch 'verification' `
    -dipOutput $report `
    -logFile $collectorLog
if ($LASTEXITCODE -ne 0) { throw "Unity Collector BatchMode smoke failed with exit code $LASTEXITCODE. See $collectorLog" }
if (-not (Test-Path -LiteralPath $report)) { throw "Unity Collector report was not generated: $report" }

$json = Get-Content -LiteralPath $report -Raw
foreach ($metric in @('build_scene_count','animation_clip_count','scene_count_analyzed')) {
    if ($json -notmatch [regex]::Escape($metric)) { throw "Unity Collector report is missing expected metric '$metric'." }
}

$pass = Join-Path $out 'UNITY_COLLECTOR_VERIFICATION_PASS.txt'
@(
    'STATUS=PASS',
    "Timestamp=$([DateTimeOffset]::UtcNow.ToString('u'))",
    "UnityVersion=$unityVersion",
    "TestResults=$testXml",
    "CollectorReport=$report"
) | Set-Content -LiteralPath $pass -Encoding UTF8
Write-Host "Unity Collector verification passed: $pass" -ForegroundColor Green

if (-not $KeepTemporaryProject) {
    Remove-Item -LiteralPath $tempProject -Recurse -Force -ErrorAction SilentlyContinue
}
