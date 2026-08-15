# Priority implementation package — 2026-08-11

Base repository: `MKyosei2/development-intelligence-platform`
Observed base revision: `5f1dc31c94f58497bb51fbe250b6e0374e98b45e` (`main`)

## Implemented in this package

- Fail-closed P0/P1/P2 and portfolio-readiness verification entry points.
- QA Bug Zoo verifier backed by the existing Python/C#/C++ QA engines.
- 18 Bug Zoo manifests including three clean controls.
- Unity Collector 0.2.0 priority QA completion: Build Settings, Animation, Scene, Runtime Performance and tests.
- Existing Collector API -> Application -> Outbox/Redis/Worker -> Quality Gate path preserved/reconstructed for the source package.
- Current P2 repository/service registrations retained while unknown-language Universal QA remains intentionally deferred.

## Evidence bundled

See `artifacts/package-validation/`.

- `STATIC_PACKAGE_VALIDATION.txt`: syntax/path/static source validation.
- `CPP_BUG_ZOO_NATIVE_VALIDATION.txt`: native C++ sanitizer behavior checked in the generation environment.
- `PYTHON_COMPILEALL.txt`: Python analysis service source compilation check.
- `PYTHON_TESTS.txt`: notes that a Python service test directory was not present in the reconstructed source tree.

## Runtime verification status

This package is **not** marked P0/P1/P2 runtime-verified by the generation environment because .NET SDK 8 and Unity Editor were not available there. Run on the target Windows development machine:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-PortfolioReadiness.ps1 -UnityPath "C:\Path\To\Unity.exe"
```

The scripts are fail-closed: final PASS evidence is generated only after the required verification stages succeed.
