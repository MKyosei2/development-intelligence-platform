# Browser-only Web deployment

Development Intelligence Platform (DIP) is intended to be used by end users entirely from a web browser. End-user PCs do not need PowerShell, `.cmd` launchers, Docker Desktop, .NET SDK, Python, or a locally installed DIP application.

## End-user flow

1. Open `https://<DIP_PUBLIC_HOST>/` in a modern browser.
2. Sign in with GitHub when authentication is required.
3. Choose/import a repository and use Project Overview, Repository, CI, QA, Intelligence and Governance from the browser.

No executable is downloaded or started on the client PC.

## Hosted topology

```text
Browser
  |
  | HTTPS :443
  v
Caddy (automatic TLS)
  |-- /api/* and /hubs/* -> Dip.Api
  `-- all other paths    -> Dip.Web (Blazor Server)

Private Docker network only:
  Dip.Web -> Dip.Api
  Dip.Api -> PostgreSQL / Redis / Python Analysis
  Worker  -> PostgreSQL / Redis / Python Analysis
```

Only ports 80/443 are published by `docker-compose.web.yml`. PostgreSQL, Redis, Python Analysis, Worker metrics and the raw Web/API container ports are not published to the host.

## Server prerequisites

This section is for the server administrator, not DIP users.

- A Linux server or VM with Docker Engine and Docker Compose v2.
- A DNS A/AAAA record for the public hostname pointing to the server.
- Inbound TCP 80 and TCP/UDP 443 allowed by the firewall/security group.
- GitHub OAuth App credentials for production authentication.

## First deployment

Clone the repository on the server, then create the production environment file:

```bash
cp deploy/web.env.example deploy/web.env
```

Edit `deploy/web.env` and replace every `CHANGE_ME` value. Do not commit `deploy/web.env`; repository ignore rules already exclude `.env`-style secret files.

Start the hosted stack:

```bash
docker compose --env-file deploy/web.env -f docker-compose.web.yml up -d --build
```

Check server-side status:

```bash
docker compose --env-file deploy/web.env -f docker-compose.web.yml ps
```

After DNS is resolving, Caddy obtains and renews HTTPS certificates automatically. End users then access only:

```text
https://<DIP_PUBLIC_HOST>/
```

## GitHub OAuth

Create/configure a GitHub OAuth App with:

```text
Homepage URL:
https://<DIP_PUBLIC_HOST>/

Authorization callback URL:
https://<DIP_PUBLIC_HOST>/api/v1/auth/github/callback
```

The API completes OAuth and posts the short-lived DIP token to the Web server callback at:

```text
https://<DIP_PUBLIC_HOST>/auth/callback
```

The browser receives an HttpOnly web session cookie. The API JWT is not stored in browser localStorage.

## GitHub App repository import

For the repository picker/import feature, configure the optional `DIP_GITHUB_APP_*` values in `deploy/web.env`. A GitHub webhook can target:

```text
https://<DIP_PUBLIC_HOST>/api/v1/webhooks/github
```

Keep the private key and webhook secret server-side. They are never required on an end-user PC.

## Updates

```bash
git pull --ff-only
docker compose --env-file deploy/web.env -f docker-compose.web.yml up -d --build --remove-orphans
```

## Stop or restart

```bash
docker compose --env-file deploy/web.env -f docker-compose.web.yml restart
```

To stop containers while preserving named volumes:

```bash
docker compose --env-file deploy/web.env -f docker-compose.web.yml down
```

Do not add `--volumes` unless persistent DIP data is intentionally being destroyed.

## Security model

- Production authentication is forced on by `docker-compose.web.yml`.
- Production database/JWT/OAuth/webhook secrets are required; development default credentials are not accepted by the API production guard.
- TLS terminates at Caddy and forwarded HTTPS/host headers are processed by the Web/API applications.
- Only Caddy is reachable from outside the Docker network.
- Database, Redis, analysis and worker services stay private.
- ASP.NET Data Protection keys are persisted in named volumes so login sessions and OAuth state survive container restarts.

## Developer verification

Developer and CI verification scripts under `scripts/` remain available for source validation. They are not part of the end-user startup path. A Windows user should never need to run them just to use DIP.
