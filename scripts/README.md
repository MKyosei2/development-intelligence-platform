# Repository automation scripts

The scripts in this directory are for local startup, developers, CI, formal verification and evidence collection.

## Standard local runtime

- `Start-DIP.ps1`: standard Windows launcher for the same full stack defined by `../docker-compose.yml`. It starts Docker Desktop automatically when installed but stopped, builds/starts PostgreSQL, Redis, Python Analysis, API, Worker and Web, verifies their runtime endpoints, then opens the browser.
- `Stop-DIP.ps1`: stops the local stack while preserving persistent Docker volumes by default. `-RemoveData` also removes those volumes.

Repository-root `Start-DIP.cmd` and `Stop-DIP.cmd` are double-click wrappers around these PowerShell scripts. There is no separate demo backend or reduced-function runtime.

Useful startup options:

```powershell
.\scripts\Start-DIP.ps1
.\scripts\Start-DIP.ps1 -NoBuild
.\scripts\Start-DIP.ps1 -VerifyGui
.\scripts\Start-DIP.ps1 -NoBrowser
```

`-VerifyGui` runs the existing Playwright UI E2E suite after all runtime health checks pass. Node.js/npm is required only for that optional verification flag, not for the normal DIP startup path.

Hosted end users still access DIP from a browser and do not run PowerShell or Docker themselves. Hosted deployment is documented in [`../docs/WEB_DEPLOYMENT.md`](../docs/WEB_DEPLOYMENT.md).

## Developer / CI scripts

- `setup-development.ps1`: prepare development tools and Python virtual environment
- `verify-environment.ps1`: validate developer toolchain and required paths
- `build-all.ps1`: build C#, Python and C++ components
- `test-all.ps1`: run .NET, Python, C++ and optional k6 tests
- `format-all.ps1`: run `dotnet format` and `ruff`
- `generate-sample-data.ps1`: generate sample data and SHA-256 manifests
- `package-release.ps1`: package release artifacts
- `validate-schemas.ps1`: validate sample reports against JSON schemas
- `Verify-Solution.ps1`: P0/source verification
- `Verify-GuiRuntime.ps1`: isolated developer GUI runtime verification
- `Verify-GuiPhase1.ps1`: repository-onboarding GUI E2E verification
- `Verify-QaGovernance.ps1`: QA governance verification
- `Verify-P1.ps1` / `Verify-P2.ps1`: platform evidence verification
- `Run-UniversalQaBenchmark.ps1`: measured native-toolchain QA bootstrap benchmark

Run developer scripts from the repository root. Generated evidence belongs under `artifacts/` or `reports/`.

Windows-specific verification remains only where Windows itself is the subject of the evidence. It is not required to use the hosted DIP website.
