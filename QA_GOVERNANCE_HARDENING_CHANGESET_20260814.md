# QA Governance Hardening Changeset — 2026-08-14

This changeset integrates the requested security, consistency, operational and UI hardening into the existing Development Intelligence Platform architecture.

## Implemented

1. Project-scoped membership authorization with Viewer/Member/Maintainer/Owner and global Admin recovery.
2. Indirect project-resource authorization for QA runs, pipeline runs, incidents, durable jobs, benchmark runs, replay resources and artifact downloads.
3. Server-derived audit actors; Governance request DTOs no longer accept Actor/ApprovedBy.
4. Before/after/reason/request/correlation audit evidence.
5. Exactly-once QA Governance ledger with input hash, PostgreSQL advisory lock and SERIALIZABLE transaction.
6. Optimistic concurrency tokens and project mutation serialization.
7. Formal Policy lifecycle/versioning and single-active DB invariant.
8. Two-person, expiring Waiver workflow.
9. Commit/evaluation-bound two-person Release Gate Override workflow.
10. `QaGovernanceService` split into Run Processor, Core Engine, Administration and Query/DTO boundary.
11. Dedicated Governance Prometheus metrics, Grafana dashboard and alert rules.
12. Operational backup/restore scripts plus isolated DR proof.
13. Governance DTO boundary and keyset pagination for high-volume Finding state reads.
14. Formal Governance load/concurrency runner.
15. Governance management UI for findings, waivers, policies, overrides, memberships, audit and backup proof.
16. Production startup guard for authentication/OAuth/admin recovery/JWT/database/sandbox/default credentials.
17. Migration fresh/rollback/re-upgrade verification and exact-revision Formal verification integration.
18. Explicit existing-project membership bootstrap/recovery script through the audited Admin API.
19. Anonymous endpoint preservation for signed GitHub webhooks/metrics while project-bearing resources stay scoped.

## Evidence rule

The generated package does not claim Windows/.NET/Docker/Unity PASS unless those checks are executed on the exact packaged revision in a compatible environment. The generation environment did not contain those runtimes. Formal runners are included so the final evidence is measured rather than fabricated.


## GitHub baseline

The latest connected `main` checked during packaging was `c26252f41aeb160e465e5083d72390ca8b120654` (2026-08-14). This hardening package builds on the Universal QA + QA Governance changes already present at that baseline; it does not remove those features.
