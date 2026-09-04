# Development Intelligence Platform (DIP)

DIP is a development-intelligence platform for game, media and software teams. It connects Git/CI/QA/runtime/performance/project data into traceable evidence inside a shared Project Context, then uses that evidence for Project Truth, quality automation, performance analysis, release readiness and evidence-grounded debugging.

> **Native-first runtime:** Docker Desktop, Docker Engine and an external queue daemon are not required. PostgreSQL is the durable system of record and queue/coordination boundary; .NET and Python services run directly on the host.

## Engineering principles

- **Implemented ≠ Tested ≠ Runtime Verified.** DIP does not promote source presence or historical evidence into a current-revision claim.
- **Claim and Evidence are separate.** Project Truth can be Confirmed, Partially Confirmed, Unverified, Contradicted or Stale.
- **AI is not a source of truth.** Autonomous Debugging follows Hypothesis → Candidate → Isolated Verification → Human Review.
- **Broad capability is intentional.** Game-development infrastructure, QA, project intelligence, performance, management and production operations share one evidence model rather than existing as disconnected tools.
- **Production operations fail closed.** Authentication, native QA isolation, backup integrity, exact-revision packaging and readiness are explicit gates.

## Native architecture

```text
Browser
  │
  ├── Dip.Web (.NET / Blazor)        http://127.0.0.1:5081
  │       │
  │       └── Dip.Api (.NET)         http://127.0.0.1:5080
  │               │
  │               ├── PostgreSQL     durable truth + durable queue
  │               ├── Python Analysis http://127.0.0.1:8100
  │               └── filesystem     artifacts / workspaces
  │
  └── Dip.Worker (.NET Worker)       metrics :9091
          │
          ├── PostgreSQL-backed jobs / queue
          ├── native QA processes
          └── Unity / Unreal / C++ / toolchain integrations
```

The generic queue and the existing Durable Job subsystem are PostgreSQL-backed.

## Requirements

For normal local development:

- .NET 8 SDK
- PostgreSQL 16 or compatible supported PostgreSQL
- PostgreSQL client tools: `psql`, `pg_isready`, `pg_dump`, `pg_restore`
- Python 3.12+
- Git

Node.js 22+ is required only for browser E2E. Unity, Unreal, CMake and native analyzers are required only when exercising their corresponding integrations/QA capabilities.

## First run

Create a PostgreSQL login/database once. Example from a PostgreSQL administrator shell:

```sql
CREATE ROLE dip LOGIN PASSWORD 'change-me-local-only' CREATEDB;
CREATE DATABASE dip OWNER dip;
```

Then from PowerShell:

```powershell
$env:POSTGRES_HOST = "127.0.0.1"
$env:POSTGRES_PORT = "5432"
$env:POSTGRES_DB = "dip"
$env:POSTGRES_USER = "dip"
$env:POSTGRES_PASSWORD = "change-me-local-only"
.\scripts\Start-DIP.ps1
```

`Start-DIP.ps1` validates PostgreSQL, creates the Python virtual environment, restores/builds the .NET solution, starts Python Analysis/API/Worker/Web as host processes, records their PIDs under `.dip/runtime`, waits for health/readiness, and opens the UI.

Useful variants:

```powershell
.\scripts\Start-DIP.ps1 -NoBuild -NoBrowser
.\scripts\Start-DIP.ps1 -VerifyGui
.\scripts\Stop-DIP.ps1 -KeepData
```

Stopping DIP never stops PostgreSQL and never removes PostgreSQL data. Runtime/artifact deletion requires an explicit destructive switch.

## Runtime storage

Default local paths:

```text
.dip/runtime/          PID files + native process logs
.dip/python-venv/      Python analysis virtual environment
.dip/workspaces/       repository/QA workspaces
artifacts/durable/     durable local artifacts
artifacts/backups/     verified backups
```

Production should use explicit absolute paths outside the release directory.

## Backup / restore / DR

Native backup uses PostgreSQL client tools directly and archives ordinary filesystem artifacts:

```powershell
.\scripts\Backup-Dip.ps1
.\scripts\Restore-Dip.ps1 -BackupDirectory <path> -ConfirmRestore
.\scripts\Verify-DisasterRecovery.ps1
```

A backup contains:

```text
database.dump
artifacts.zip
backup-manifest.json
offsite-location.txt    # when an offsite mirror is configured
```

Hashes are verified before restore. Production deployment requires an offsite directory on a different filesystem from the deployment root.

## Database migrations

```powershell
.\scripts\Verify-DatabaseMigrations.ps1
```

The verifier uses a temporary native PostgreSQL database, applies the previous/latest EF migration sequence when applicable, re-applies the latest revision to prove idempotence, checks `__EFMigrationsHistory`, and removes the temporary database.

## QA isolation

DIP executes repository QA as native processes rather than inside Docker. The existing process executor provides executable allowlisting, canonical workspace checks, environment sanitization, timeout/process-tree termination and resource controls. Production remains fail-closed unless external network and filesystem isolation are explicitly attested/configured.

On Windows the intended hard boundary is a dedicated low-privilege account + ACL-scoped temporary workspace + Job Object resource limits + firewall/network isolation. Production secrets are not passed to QA child processes.

## Benchmarks and soak

```powershell
.\scripts\Run-PortfolioBenchmarks.ps1
```

The benchmark still measures project-list latency, collector ingestion, controlled burst load, large Metric aggregation and paced soak/memory growth. It creates a temporary PostgreSQL benchmark database and removes it afterwards. No Docker or external queue service is started.

Current-revision numbers must come from a run whose artifact records that exact SHA. Historical benchmark numbers are baselines only and are not inherited by later commits.

## CI and release

The file `.github/workflows/docker-build.yml` is retained as an existing repository path for compatibility, but the workflow itself is now named **Native Package Build** and does not invoke Docker. It publishes:

- Dip.Api
- Dip.Worker
- Dip.Web
- Python Analysis source/runtime requirements
- deployment/scripts
- EF migration bundle
- exact-SHA release manifest + SHA-256 manifest

`Live Native Deployment` deploys immutable per-SHA release directories, switches a `current` symlink, restarts user-level systemd services, verifies public readiness/browser behavior, and restores the previous database/artifact/release state on failure.

Caddy is a normal host reverse proxy using `deploy/Caddyfile` and stable loopback endpoints (`5080`/`5081`).

## Reviewer-facing capability contract

- **Project Truth** — evidence-linked claim state with freshness/contradiction handling.
- **Evidence-Grounded Autonomous Debugging** — hypothesis/candidate generation is subordinate to isolated verification and human review.
- **Flexible Spreadsheet Intelligence** — spreadsheet ingestion/analysis participates in the same project/evidence model.
- **Historical PASS ≠ Current Revision PASS** — historical evidence is never silently promoted to a later SHA.

## Core product areas

DIP includes Project Truth/Evidence Graph, QA and defect intelligence, evidence-grounded autonomous debugging, development/repository graphs, performance and hardware intelligence, release/readiness evidence, project/management intelligence, spreadsheet intelligence, Unity/Unreal/native collectors, integrations and operational evidence.

The important architectural constraint is that these capabilities share project scope, evidence provenance and verification state; they are not treated as unrelated feature demos.

## Verification before portfolio publication

Use `docs/09-portfolio/PUBLICATION_CHECKLIST.md`. The exact publication SHA must have real successful workflow execution; a zero-step infrastructure failure is neither a source failure nor a PASS.

The expected workflow set includes `.NET CI`, Python CI, native/unreal collectors, Integration/E2E, GUI E2E, **Native Package Build**, Security Scan, Universal QA Benchmark and Portfolio Publication Readiness.

## Legacy Docker files

Some `docker-compose*.yml` / Dockerfile paths may remain in repository history for compatibility and migration reference. They are **not** the supported/default local, CI, release or production runtime. New operational work should use the Native-first paths above.
