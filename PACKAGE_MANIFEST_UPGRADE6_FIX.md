# Package manifest — Upgrade6 CI repair

- Base repository: `MKyosei2/development-intelligence-platform`
- Observed base revision: `0039e4ffedee26b48f557887820519ecab225b0d` (`Upgreade6`)
- Package type: repaired full-source reconstruction with current-main compatibility files restored
- Universal/unknown-language QA: intentionally deferred to the final architecture phase

## Primary repaired files

- `tests/Dip.EndToEndTests/Support/DipScenarioClient.cs`
- `src/Dip.Web/Services/ApiClient.cs`
- `src/Dip.Infrastructure/P2Intelligence/MediaRegressionRunner.cs`
- `src/Dip.Application/DependencyInjection.cs`
- `src/Dip.Infrastructure/P2Intelligence/OpenAiCompatibleRootCauseClient.cs`
- `src/Dip.Infrastructure/Hardware/HardwareConstraintRunner.cs`
- `src/Dip.Application/Features/QualityAutomation/QaFingerprint.cs`
- `src/Dip.Domain/ValueObjects/HealthScore.cs`

## Current-main files restored into the full package

The package also restores the current native collector and key repository operation files/workflows that were not present in the previous reconstructed ZIP.

- `src/Dip.Api/Controllers/IntelligenceController.cs`
- `src/Dip.Api/Controllers/PipelineRunsController.cs`
- `src/Dip.Web/Pages/Dashboard/ProjectIntelligence.razor`
- `src/Dip.Application/Features/Intelligence/AdaptiveQaPlanner.cs`
- `src/Dip.Application/Features/Intelligence/P2AnalysisService.cs`
- `src/Dip.Application/Features/Intelligence/RootCauseIntelligenceService.cs`
- current placeholder/support files under Web State, Application Behaviors/Mapping, and Web Properties
- `collectors/native-collector/**` current CMake/C++/test tree
- `.github/workflows/dotnet-ci.yml`, `integration-test.yml`, `docker-build.yml`, `release.yml`
- `.dockerignore`, `.editorconfig`, `.gitattributes`, `CHANGELOG.md`, `CONTRIBUTING.md`, `SECURITY.md`, `Start-DIP.cmd`, `Stop-DIP.cmd`

## Evidence

- Native collector: local CMake build + CTest passed (`1/1`).
- Python analysis package: `python -m compileall` passed.

- `artifacts/package-validation/UPGRADE6_FIX_STATIC_VALIDATION.txt`
- `docs/UPGRADE6_FIX_REPORT_20260811.md`
- `docs/NEXT_RECOMMENDED_FEATURES.md`

Runtime `.NET CI`, live E2E, and Unity BatchMode/runtime verification must still be executed on their capable target environments. The local environment used to build this ZIP did not provide the .NET 8 SDK, PowerShell, or Unity Editor.
