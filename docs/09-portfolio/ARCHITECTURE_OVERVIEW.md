# Architecture Overview

DIP is a browser-first evidence platform. The architecture keeps **durable truth**, **delivery/coordination**, **analysis**, and **external evidence providers** separate so a failure in one adapter does not redefine project truth.

## Boundaries

1. **Presentation** — `Dip.Web` (Blazor) presents PM/Lead views and never stores secrets in the browser.
2. **API / application** — `Dip.Api` exposes authenticated project-scoped operations; `Dip.Application` contains use cases and decision logic.
3. **Domain** — `Dip.Domain` owns evidence, project-truth and lifecycle invariants without depending on infrastructure.
4. **Infrastructure** — PostgreSQL is the durable source of truth and durable queue/coordination store. Delivery semantics remain behind `IQueueClient`, with `PostgresQueueClient` as the current implementation.
5. **Async execution** — `Dip.Worker` consumes durable jobs and normalizes external/runtime evidence.
6. **Evidence providers** — GitHub/CI, Unity, Unreal, Maya, native/runtime collectors and management integrations map into provider-neutral contracts.
7. **Verification** — tests, QA Bug Zoo, security scans and benchmark runners emit commit-bound evidence. A newer revision does not inherit an older PASS automatically.

## Failure and trust model

- ingestion is idempotent where external IDs/revisions are available;
- ambiguous cross-source matches require explicit confirmation instead of silent merge;
- secrets remain server-side and production configuration fails closed when required values are blank;
- AI/Fix Agent output is advisory until a disposable worktree re-runs build/test/QA and produces verification evidence;
- personnel are not assigned a truthfulness or skill score; DIP evaluates the relationship between a work claim and technical evidence.

See `docs/02-architecture/` for subsystem-specific design documents.
