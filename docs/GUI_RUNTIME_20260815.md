# GUI Runtime / One-click Startup — 2026-08-15

This phase makes the existing Development Intelligence Platform usable as one coherent GUI application instead of a collection of separately launched services.

## Runtime topology

```text
Start-DIP.cmd
  -> scripts/Start-Dip.ps1
  -> Docker Compose
       -> PostgreSQL
       -> Redis
       -> Python Analysis (FastAPI)
       -> Dip.Api (ASP.NET Core)
       -> Dip.Worker
       -> Dip.Web (Blazor Interactive Server)
  -> readiness probes
  -> http://127.0.0.1:5081
```

The default local mode intentionally disables user authentication so a developer can verify the product without configuring a GitHub OAuth application. Production still fails closed through `ProductionSecurityGuard`, and `-RequireAuthentication` enables the GitHub OAuth/JWT path for local verification.

## GUI surfaces

- Portfolio / all projects
- Create Project
- Project Truth
- Project Intelligence
- QA Governance
- System Status
- Settings
- authenticated QA artifact download
- authenticated Management Excel download

Blazor is configured for `InteractiveServer`, so buttons, forms, refresh actions, policy/waiver/governance operations and project creation are interactive rather than static HTML only.

## Authentication flow

```text
Browser
  -> Dip.Web /auth/login
  -> Dip.Api /api/v1/auth/github/login
  -> GitHub OAuth
  -> Dip.Api validates OAuth state and issues DIP JWT
  -> POST handoff to Dip.Web /auth/callback
  -> Dip.Web validates JWT against /api/v1/auth/me
  -> encrypted HttpOnly session cookie
  -> server-side ApiAuthenticationHandler forwards Bearer to Dip.Api
```

The API JWT is not stored in browser localStorage. Artifact and workbook downloads use a Web streaming proxy, avoiding inaccessible Docker-internal URLs and avoiding query-string access tokens.

## System Status

`GET /api/v1/system/status` separates DIP infrastructure health from the project-under-test. It probes:

- API
- PostgreSQL
- Redis
- Python Analysis
- Worker metrics
- artifact storage
- QA sandbox configuration
- GitHub configuration state

The GUI exposes this at `/system`.

## Formal GUI runtime evidence

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-GuiRuntime.ps1
```

The verifier creates an isolated Compose project with temporary host ports and volumes. It builds all GUI runtime images, waits for readiness, creates a disposable Project, verifies portfolio persistence plus Project Dashboard / Project Intelligence / QA Governance rendering, checks System Status, then destroys the isolated volume set.

Only a successful run creates:

```text
artifacts/gui-runtime-verification/GUI_RUNTIME_VERIFICATION_PASS.txt
artifacts/gui-runtime-verification/gui-runtime-verification.json
```

`Verify-PortfolioFormal.ps1` also invokes this GUI runtime verifier. Source presence is not presented as runtime proof.
