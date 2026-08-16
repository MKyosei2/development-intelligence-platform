# Browser-only Web deployment

DIP end users access one hosted HTTPS URL. Client PCs do not run DIP executables, PowerShell, Docker, .NET, or Python.

## Topology

```text
Browser --HTTPS--> Caddy
                  |-- /api/* /hubs/* -> Dip.Api
                  `-- other routes    -> Dip.Web
Private network: API/Worker -> PostgreSQL, Redis, Python Analysis, Artifact Store
```

Only Caddy publishes 80/443 in `docker-compose.web.yml`. Production authentication fails closed when required secrets are absent. Copy `deploy/web.env.example` to `deploy/web.env`, fill required values, then run on the server:

```bash
docker compose --env-file deploy/web.env -f docker-compose.web.yml up -d --build
```

GitHub OAuth homepage is `https://<host>/` and callback is `https://<host>/api/v1/auth/github/callback`. `deploy/web.env` is ignored and must never be committed. Sentry/OTel ingest, Jira/Linear, and external AI are optional server-side integrations.

Developer verification scripts under `scripts/` remain source/evidence tools; they are not an end-user startup path.
