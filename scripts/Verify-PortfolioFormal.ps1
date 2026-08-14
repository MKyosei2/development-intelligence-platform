[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$UnityPath,[string[]]$RequiredQaLanguages=@('python','csharp','cpp','javascript','customlang'))
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
if($env:OS-ne'Windows_NT'){throw 'Formal portfolio verification must run on Windows.'}
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
foreach($c in 'git','dotnet','docker'){if(-not(Get-Command $c -ErrorAction SilentlyContinue)){throw "$c is required."}}
if(-not(Test-Path -LiteralPath $UnityPath -PathType Leaf)){throw "Unity Editor not found: $UnityPath"}
$status=(& git -C $root status --porcelain|Out-String).Trim();if($status){throw 'Formal verification requires a clean working tree.'}
$sha=(& git -C $root rev-parse HEAD|Out-String).Trim()
$out=Join-Path $root 'artifacts\formal-portfolio-verification';New-Item -ItemType Directory -Force -Path $out|Out-Null
Remove-Item (Join-Path $out 'FORMAL_PORTFOLIO_VERIFICATION_PASS.txt') -Force -ErrorAction SilentlyContinue
& (Join-Path $PSScriptRoot 'Verify-Solution.ps1') -Configuration Release;if($LASTEXITCODE-ne0){throw 'Solution verification failed'}
& (Join-Path $PSScriptRoot 'Verify-UniversalQa.ps1') -RequireLanguages $RequiredQaLanguages;if($LASTEXITCODE-ne0){throw 'Universal QA verification failed'}
& (Join-Path $PSScriptRoot 'Verify-QaGovernance.ps1') -Configuration Release;if($LASTEXITCODE-ne0){throw 'QA Governance verification failed'}
& (Join-Path $PSScriptRoot 'Verify-GuiRuntime.ps1');if($LASTEXITCODE-ne0){throw 'GUI runtime verification failed'}
& (Join-Path $PSScriptRoot 'Verify-DatabaseMigrations.ps1') -Configuration Release;if($LASTEXITCODE-ne0){throw 'Database migration verification failed'}
& (Join-Path $PSScriptRoot 'Verify-DisasterRecovery.ps1');if($LASTEXITCODE-ne0){throw 'Disaster recovery verification failed'}
& (Join-Path $PSScriptRoot 'Run-GovernanceLoadTest.ps1');if($LASTEXITCODE-ne0){throw 'Governance load/concurrency verification failed'}
& (Join-Path $PSScriptRoot 'Verify-UnityCollector.ps1') -UnityPath $UnityPath;if($LASTEXITCODE-ne0){throw 'Unity runtime verification failed'}
& (Join-Path $PSScriptRoot 'Verify-ProjectControl.ps1');if($LASTEXITCODE-ne0){throw 'Project Control verification failed'}
& (Join-Path $PSScriptRoot 'Verify-P2.ps1');if($LASTEXITCODE-ne0){throw 'P2 verification failed'}
& (Join-Path $PSScriptRoot 'Run-PortfolioBenchmarks.ps1');if($LASTEXITCODE-ne0){throw 'Portfolio benchmark failed'}
$bench=Get-Content (Join-Path $root 'artifacts\portfolio-benchmark\portfolio-benchmark-results.json') -Raw|ConvertFrom-Json
if($bench.sourceCommit-ne$sha){throw "Benchmark evidence belongs to $($bench.sourceCommit), expected $sha"}
$payload=[ordered]@{schema='dip-formal-portfolio-verification-v4';sourceCommit=$sha;verifiedAtUtc=[DateTimeOffset]::UtcNow.ToString('o');os=[Runtime.InteropServices.RuntimeInformation]::OSDescription;dotnet=(& dotnet --version|Out-String).Trim();unityPath=(Resolve-Path $UnityPath).Path;qaLanguages=$RequiredQaLanguages;benchmark=$bench.scenarios}
$payload|ConvertTo-Json -Depth 10|Set-Content (Join-Path $out 'formal-verification.json') -Encoding UTF8
@('FORMAL PORTFOLIO VERIFICATION PASS',"Commit=$sha","VerifiedAtUtc=$($payload.verifiedAtUtc)",'Windows=PASS','.NET=PASS','Unity=PASS','UniversalQA=PASS','QaGovernance=PASS','GuiRuntime=PASS','DatabaseMigration=PASS','DisasterRecovery=PASS','GovernanceLoad=PASS','AuthorizationHardening=PASS','BackupRestoreDR=PASS','ProjectControl=PASS','P2=PASS','Benchmark=PASS')|Set-Content (Join-Path $out 'FORMAL_PORTFOLIO_VERIFICATION_PASS.txt') -Encoding UTF8
Write-Host "FORMAL PORTFOLIO VERIFICATION PASS ($sha)" -ForegroundColor Green
