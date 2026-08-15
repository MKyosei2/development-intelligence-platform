# Upgrade6 CI repair report — 2026-08-11

Base repository: `MKyosei2/development-intelligence-platform`  
Observed main revision before repair: `0039e4ffedee26b48f557887820519ecab225b0d` (`Upgreade6`)

## GitHub Actions failure observed

The latest `.NET CI` restored successfully but failed during `dotnet build`.
The build log contained 7 compiler errors:

- `DipScenarioClient.GetP2DailyChangeAsync` missing.
- `DipScenarioClient.GetP2DecisionBriefAsync` missing.
- `DipScenarioClient.GetP2HealthChangeAsync` missing.
- `ApiClient.GetForecastAsync` missing.
- `ApiClient.GetDecisionBriefAsync` missing.
- Two C# declaration-space collisions for `detail` in `MediaRegressionRunner`.

`Integration and E2E Tests` also stopped at its build stage, so API integration,
service startup and live E2E did not run on that revision.

## Repairs included

- Added typed P2 E2E client calls for daily change, decision brief and health change.
- Added Web `ApiClient` forecast/decision-brief methods.
- Renamed visual/video regression detail locals to remove CS0136 collisions.
- Restored current-main files omitted from the previous reconstructed package,
  including the legacy intelligence API/page/services needed by the current source tree.
- Added missing legacy intelligence DI registrations so the restored controller
  cannot compile successfully and then fail immediately on service activation.
- Added checked Windows job-object memory-size conversion.
- Removed the no-await async portability method warning by returning `Task.FromResult`.
- Made QA fingerprint line formatting and `HealthScore` formatting culture-invariant.
- Corrected the QA unsupported-language exception parameter target.

## Validation performed in this package environment

`artifacts/package-validation/UPGRADE6_FIX_STATIC_VALIDATION.txt` checks:

- all JSON parses;
- all MSBuild XML parses;
- all 13 solution project references exist;
- all 7 Upgrade6 compiler-blocker contracts are present/fixed;
- legacy intelligence DI registrations exist;
- P2 decision routes required by E2E exist;
- no merge-conflict markers exist;
- the high-impact analyzer safety fixes above are present.

The static validation result is `PASS`.

Additional executable validation performed here:

- Python analysis `compileall`: PASS.
- Current-main native collector CMake configure/build: PASS.
- Native collector CTest: PASS (`1/1`, 0 failures).

## Required final proof on Windows / GitHub Actions

This package environment does not have the .NET 8 SDK, PowerShell, or Unity Editor, so it does
not claim a real `dotnet build`, live .NET E2E pass, or Unity runtime verification. Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-Upgrade6Fixes.ps1
```

A repository revision should only be described as fully verified after `.NET CI`
and `Integration and E2E Tests` are green for that exact revision.
