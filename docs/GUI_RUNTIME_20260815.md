# GUI Runtime / Browser-first Web — 2026-08-15

DIP is delivered to end users as one hosted website rather than a locally launched Windows application.

## Runtime topology

```text
Browser
  -> HTTPS :443
  -> Caddy
       |-- /api/*, /hubs/* -> Dip.Api
       `-- other routes     -> Dip.Web (Blazor Interactive Server)

Private server network:
  Dip.Web -> Dip.Api
  Dip.Api -> PostgreSQL / Redis / Python Analysis
  Dip.Worker -> PostgreSQL / Redis / Python Analysis
```

The browser is the only client requirement. End users do not run `.cmd` launchers, PowerShell startup scripts or Docker Desktop.

## GUI surfaces

- Portfolio / all projects
- GitHub Repository Picker / Import
- Project Overview / Project Truth
- Repository / CI / QA
- Project Intelligence
- QA Governance
- System Status
- Settings
- authenticated QA artifact download
- authenticated Management Excel download

Blazor uses `InteractiveServer`, so UI interaction and API orchestration run on the hosted server while the browser maintains the interactive session.

## Authentication flow

```text
Browser
  -> Dip.Web /auth/login
  -> same-origin /api/v1/auth/github/login
  -> GitHub OAuth
  -> same-origin /api/v1/auth/github/callback
  -> Dip.Api validates OAuth state and issues DIP JWT
  -> POST handoff to Dip.Web /auth/callback
  -> Dip.Web validates JWT against internal Dip.Api
  -> encrypted HttpOnly session cookie
  -> server-side ApiAuthenticationHandler forwards Bearer to Dip.Api
```

The API JWT is not stored in browser localStorage. Artifact and workbook downloads use the Web streaming proxy.

## HTTPS / reverse proxy

`docker-compose.web.yml` is the hosted production topology and `deploy/Caddyfile` is its public ingress configuration. Only ports 80/443 are published. Caddy performs TLS and routes Web/API/SignalR traffic on the same public origin.

Forwarded scheme/host headers are processed by both ASP.NET Core services so OAuth callbacks, secure cookies and HTTPS redirects use the public URL instead of Docker-internal addresses.

See [`WEB_DEPLOYMENT.md`](WEB_DEPLOYMENT.md).

## Formal developer verification

The source verification scripts remain available for developers and CI:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-GuiRuntime.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-GuiPhase1.ps1
```

These scripts create isolated verification environments and are not part of the end-user access path. Source presence is not presented as runtime proof; formal PASS artifacts still require the corresponding verifier to complete successfully.
