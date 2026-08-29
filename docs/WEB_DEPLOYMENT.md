# Web deployment — Native-first

DIP production deployment does not require Docker or a container registry.

## Host prerequisites

- Linux host with .NET 8 runtime
- Python 3.12+
- PostgreSQL server reachable by the deployment account
- PostgreSQL client tools (`psql`, `pg_dump`, `pg_restore`)
- `systemd --user` available for the deployment account; enable linger for that account so services survive SSH logout
- Caddy installed/configured with `deploy/Caddyfile`
- a durable artifact/workspace directory outside release directories
- a separately mounted offsite backup filesystem

## Release model

`Live Native Deployment` creates `releases/<GITHUB_SHA>`, verifies the release SHA-256 manifest, creates a Python venv, applies the EF migration bundle, atomically changes the `current` symlink and restarts four user services: analysis, api, worker and web.

The public reverse proxy always targets loopback `5080`/`5081`, so release switching does not require changing application addresses.

## Rollback

Before mutation the workflow stores a PostgreSQL custom-format dump plus artifact archive and hashes, mirrors them to a distinct filesystem, and records the previous release. A deployment failure stops the new services, restores database/artifacts, moves `current` back and restarts the previous release.

Production configuration is based on `deploy/web.env.example`. Secrets must be supplied by GitHub environment secrets/host secret management and must never be committed.

Optional integration/notification settings can be kept in `$DIP_DEPLOY_PATH/host-overrides.env`; generated systemd units load it after the core deployment environment without putting those secrets into the release archive.
