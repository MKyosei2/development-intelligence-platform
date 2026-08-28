# Browser-only Web deployment

DIP's reviewer and end-user path is a normal HTTPS website. Client PCs do not run DIP executables, PowerShell, Docker, .NET, Python, PostgreSQL or Redis.

## Runtime topology

```text
Browser --HTTPS--> Caddy
                  |-- /healthz -> Dip.Api /health
                  |-- /api/* /hubs/* -> Dip.Api
                  `-- other routes -> Dip.Web

Private network:
Dip.Api / Dip.Worker -> PostgreSQL, Redis, Python Analysis, Artifact Store
```

`docker-compose.web.yml` remains the single hosted application definition. Public portfolio access does **not** select a separate demo backend. `PublicAccess__ReadOnly=true` changes authorization only:

- anonymous GET/HEAD/OPTIONS project reads are permitted;
- ProjectWrite remains authenticated Member/Admin only;
- Admin remains authenticated Admin only;
- anonymous mutating MVC requests are rejected by the project-scope authorization filter;
- GitHub App installation/repository-catalog endpoints require authentication even in public mode;
- the public Repository page returns stored project evidence without exposing server-side GitHub App installation details.

Use a dedicated portfolio deployment/database containing only information that is appropriate to publish. Do not enable anonymous read-only on a production database containing confidential company/project evidence.

## Reviewer experience

After deployment, reviewers only need the HTTPS URL shown at the top of `README.md`:

```text
GitHub README -> Open Development Intelligence Platform -> browser
```

There are no reviewer-side setup commands.

## Administrator one-time setup

The live deployment workflow expects a Linux host with Docker Engine + Docker Compose already installed and ports 80/443 reachable. Point the chosen `DIP_PUBLIC_HOST` DNS record at that host before the first deployment so Caddy can obtain HTTPS certificates.

Configure the following **GitHub repository variables** in Settings → Secrets and variables → Actions → Variables:

| Variable | Purpose |
|---|---|
| `DIP_LIVE_DEPLOY_ENABLED` | Set to `true` after server/DNS setup; then main pushes deploy automatically |
| `DIP_PUBLIC_HOST` | Public DNS hostname only, for example `dip.example.com` |
| `DIP_DEPLOY_USER` | SSH user on the deployment host |
| `DIP_DEPLOY_PATH` | Absolute server directory, for example `/home/dip/development-intelligence-platform` |
| `DIP_GHCR_USER` | GitHub/GHCR account used by the server to pull private images |
| `DIP_GITHUB_ADMIN_LOGIN` | Recovery/admin GitHub login |
| `DIP_GITHUB_APP_ID` | Optional GitHub App ID |
| `DIP_GITHUB_APP_INSTALLATION_ID` | Optional GitHub App installation ID |
| `DIP_GITHUB_APP_SLUG` | Optional GitHub App slug |

Configure these **GitHub Actions secrets**:

| Secret | Purpose |
|---|---|
| `DIP_DEPLOY_HOST` | Server hostname/IP used by SSH |
| `DIP_DEPLOY_HOST_KEY` | Trusted SSH known_hosts line for the server |
| `DIP_DEPLOY_SSH_KEY` | Private SSH deployment key |
| `DIP_GHCR_TOKEN` | Token with package read permission for server-side GHCR pull |
| `DIP_POSTGRES_PASSWORD` | Non-default PostgreSQL password |
| `DIP_GITHUB_OAUTH_CLIENT_ID` | GitHub OAuth client ID |
| `DIP_GITHUB_OAUTH_CLIENT_SECRET` | GitHub OAuth client secret |
| `DIP_JWT_SIGNING_KEY` | Random secret at least 64 characters |
| `DIP_GITHUB_WEBHOOK_SECRET` | Random webhook signing secret |
| `DIP_GITHUB_APP_PRIVATE_KEY_BASE64` | Optional GitHub App PEM encoded as base64 |
| `DIP_GITHUB_TOKEN` | Optional server-side GitHub integration token |

GitHub OAuth should use:

- Homepage: `https://<DIP_PUBLIC_HOST>/`
- Callback: `https://<DIP_PUBLIC_HOST>/api/v1/auth/github/callback`

No secret is stored in the repository. `deploy/web.env` exists only on the deployment server and is produced by GitHub Actions.

## Automated deployment

`.github/workflows/live-deploy.yml` performs the complete revision-to-browser path:

1. publication-safety and portfolio-readiness gates;
2. .NET Release build and core tests;
3. exact-Git-SHA GHCR images for API, Web, Worker and Python Analysis;
4. SSH upload of only the Compose/Caddy/environment deployment definition;
5. server pull + `docker compose up -d --no-build` using the exact image SHA;
6. public HTTPS `/healthz`, Web and `/api/projects` checks;
7. Playwright against the actual deployed browser URL;
8. GitHub `production` environment result and URL;
9. previous environment/image-tag rollback attempt if post-deployment verification fails.

A successful workflow therefore means the exact revision was not merely built: its public browser path was exercised after deployment.

## Initial portfolio content

The live deployment uses persistent PostgreSQL volumes. After the first successful deployment, sign in as the configured GitHub admin and import/register the reviewer-facing project through the normal DIP UI. That data remains in the same PostgreSQL-backed application used by the public read-only view.

This is data population, not a separate demo implementation. For a portfolio host, publish only data that is safe for anonymous viewing.

## README live URL

The patch intentionally leaves this marker until a real host exists:

```text
https://YOUR_DIP_HOST/
```

After the first successful deployment, replace it once with the actual `https://<DIP_PUBLIC_HOST>/` URL. Reviewers then click a single browser link and never see the deployment procedure above.
