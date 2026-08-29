# Deployment

Production deployment is exact-SHA and Native-first. `.github/workflows/live-deploy.yml` publishes .NET services, Python Analysis and an EF migration bundle into an immutable release directory, verifies hashes, snapshots durable state, updates the `current` symlink and restarts systemd user services.

Caddy is host-native and proxies public traffic to `127.0.0.1:5080` (API) and `127.0.0.1:5081` (Web). A separate offsite filesystem is mandatory for automated live deployment rollback evidence.
