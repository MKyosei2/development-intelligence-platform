param([switch]$SkipDotNet,[switch]$SkipNative)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
  $required=@(
    'src/Dip.Application/Features/Intelligence/P2OrchestrationService.cs',
    'src/Dip.Application/Features/Intelligence/AdaptiveQaPlanner.cs',
    'src/Dip.Application/Features/Intelligence/RootCauseIntelligenceService.cs',
    'src/Dip.Infrastructure/Intelligence/MinimumHardwareRunner.cs',
    'src/Dip.Infrastructure/Intelligence/RegressionRunner.cs',
    'src/Dip.Infrastructure/Intelligence/ReliabilityRunner.cs',
    'src/Dip.Api/Controllers/IntelligenceController.cs'
  )
  foreach($f in $required){if(!(Test-Path $f)){throw "Missing P2 file: $f"}}
  if(!$SkipDotNet){ dotnet restore DevelopmentIntelligencePlatform.sln; dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore; dotnet test DevelopmentIntelligencePlatform.sln -c Release --no-build }
  if(!$SkipNative -and (Test-Path '.\scripts\Build-P1-NativeTools.ps1')){ & '.\scripts\Build-P1-NativeTools.ps1' }
  'P2 verification PASS' | Tee-Object -FilePath 'artifacts/P2_VERIFICATION_PASS.txt'
} finally { Pop-Location }
