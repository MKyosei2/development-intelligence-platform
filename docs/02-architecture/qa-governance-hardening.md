# QA Governance Hardening (2026-08-14)

## Goal

QA Governance is treated as a privileged control plane. A quality result is not enough: the platform must prove which project the caller may operate, who approved exceptions, whether the same evidence was processed once, and whether recovery and release decisions are reproducible.

## Project-scoped authorization

`ProjectMembership` is the authorization source of truth. Roles are ordered `Viewer < Member < Maintainer < Owner`; global `Admin` is the recovery/operator role. New projects are provisioned atomically with an Owner membership.

`ProjectScopeAuthorizationFilter` evaluates route/body `ProjectId` values after model binding. Indirect identifiers are protected with `RequireProjectResourceAttribute`; QA run, pipeline run, incident, durable job, benchmark run, replay and artifact identifiers are resolved to their owning project before the controller action executes. This prevents authorization from depending on an attacker-supplied project GUID alone.

Production fails closed unless authentication, a >=64-character JWT key, GitHub OAuth credentials, at least one recovery Admin login, PostgreSQL and QA sandboxing are configured. Development and Testing remain usable with local authentication disabled.


### Existing-project membership bootstrap

The authorization migration intentionally does **not** guess owners for projects that existed before `ProjectMembership`. A configured global Admin is the recovery principal. Use `scripts/Bootstrap-ProjectMemberships.ps1` with an Admin bearer token to grant an explicit Owner membership to selected projects (or `-AllProjects`). Every grant flows through the normal membership API and is therefore audited. This avoids both silent lockout and unsafe automatic privilege assignment.

Example:

```powershell
.\scripts\Bootstrap-ProjectMemberships.ps1 `
  -BaseUrl http://localhost:8080 `
  -AdminBearerToken $env:DIP_ADMIN_TOKEN `
  -SubjectId "github:123456" `
  -Login "owner-login" `
  -AllProjects
```

## Server-derived actors and audit

HTTP request DTOs do not accept `Actor` or `ApprovedBy` for governance mutations. Actor identity is derived from the JWT `sub` and `name` claims. Audit entries preserve request/correlation identifiers, reason, subject, and before/after JSON.

Important mutations include finding transitions, membership changes, waiver decisions, policy lifecycle changes, release overrides, re-baselines, ownership/test-impact/retention rule creation and backup verification evidence.

## Idempotency and concurrency

Completed QA runs are guarded by `qa_governance_processed_runs.qa_run_id UNIQUE`. Processing uses a PostgreSQL transaction-scoped advisory lock plus `SERIALIZABLE` transaction; a different input hash for the same run ID is rejected. Non-relational tests use a keyed in-process lock.

Mutable governance aggregates carry optimistic version tokens. Cross-row invariants (membership Owner retention and policy activation) use a project-scoped transaction/advisory lock. The database additionally enforces at most one Active QA policy per project with a deferrable unique constraint over a generated active slot.

## Waiver and policy lifecycle

Waivers use `Requested -> Active / Rejected -> Revoked / Expired`. A requester cannot approve their own waiver. Approved waivers are immutable; changes require a new request.

Policies use `Draft -> Approved -> Active -> Superseded / Archived`. Policy updates are new immutable versions, not in-place edits. Activation is an Owner action and supersedes the previous Active policy in the same serialized project mutation.

## Release gate override

A release override is not a global bypass. It is bound to the latest non-GO policy evaluation and an exact commit SHA, has an expiry, requires a requester and a different Owner approver, and supports rejection/revocation. Release Readiness only honors an approved, unexpired override whose evaluation and commit match the candidate release.

## Observability

Prometheus exports QA run/gate totals, open findings, flaky tests, active waivers, self-health, governance mutation totals and sandbox denials. `DIP QA Governance` is provisioned as a dedicated Grafana dashboard. Prometheus rules alert on degraded QA self-health, sandbox denials and newly observed NO-GO gates.

## Backup, restore and disaster recovery

- `scripts/Backup-Dip.ps1`: creates a PostgreSQL custom-format dump, archives the Docker Local artifact volume, hashes both, and emits a manifest.
- `scripts/Restore-Dip.ps1`: destructive restore that requires `-ConfirmRestore`, verifies hashes, stops API/Worker/Web, restores PostgreSQL and artifacts, then restarts services.
- `scripts/Verify-DisasterRecovery.ps1`: isolated proof that database and artifact evidence survive backup/restore.

Operational backup/restore targets the Docker Compose `Local` artifact provider. MinIO/S3 deployments should use bucket-versioning/replication or provider-native backup in addition to database backup.

## Formal evidence

`Verify-PortfolioFormal.ps1` is fail-closed. On Windows it requires a clean Git revision, .NET, Docker and a real Unity Editor. It runs solution tests, Universal QA, Governance tests, migration fresh/rollback/re-upgrade, DR verification, Governance load/concurrency, Unity verification, Project Control, P2 and portfolio benchmarks. PASS artifacts contain the exact source commit.
