# Contributing

Thank you for contributing to Development Intelligence Platform.

## Development environment

Required for the supported Native-first runtime:

- .NET SDK 8.0;
- PostgreSQL 16+ (or compatible supported PostgreSQL) plus `psql`, `pg_isready`, `pg_dump`, `pg_restore`;
- Python 3.12+;
- Git;
- Visual Studio 2022, Visual Studio Code, or JetBrains Rider.

Node.js is required for browser E2E. Unity/Unreal/CMake/native analyzers are required only for the corresponding integration or QA path. Docker and an external queue daemon are not required.

After creating the local `dip` PostgreSQL database/user, start the complete stack with:

```powershell
.\scripts\Start-DIP.ps1
```

Restore, build, and test directly when needed:

```bash
dotnet restore DevelopmentIntelligencePlatform.sln
dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
dotnet test DevelopmentIntelligencePlatform.sln -c Release --no-build
```

## Branches

Create a focused branch from the latest default branch. Recommended names:

```text
feature/short-description
fix/short-description
docs/short-description
test/short-description
```

Do not combine unrelated refactoring, formatting, behavior changes, and dependency updates in one pull request.

## Coding rules

- Preserve the dependency direction: `Domain <- Application <- Infrastructure <- Api / Worker`.
- Keep shared transport contracts in `Dip.Shared`.
- Use UTC for persisted timestamps.
- Pass `CancellationToken` through asynchronous operations.
- Avoid `.Result` / `.Wait()` in asynchronous request paths.
- Validate external input at the boundary.
- Keep secrets and production data out of source control and logs.
- Add/update tests when behavior changes.
- Preserve API/JSON compatibility unless a breaking change is explicitly documented.
- Keep Implemented, Tested and Runtime Verified claims separate.
- PostgreSQL is the durable coordination boundary. Do not introduce a second mandatory queue/cache daemon without an explicit architecture decision.
- Native QA process execution must preserve workspace, environment, resource and isolation boundaries.

## Commit subjects

Use Conventional Commit-style subjects. Examples:

```text
feat(qa): add runtime defect evidence
fix(ci): restore native integration startup
perf(metrics): reduce million-row query latency
refactor(web): split management cockpit components
docs(portfolio): explain exact-revision evidence
```

Keep the subject imperative and at most 72 characters. Optional template:

```bash
git config commit.template .gitmessage
```

## Pull requests

A pull request should state purpose/scope, implementation, tests, migration/configuration impact, screenshots when relevant, known limitations, and exact-revision evidence for measured/runtime claims.

Before review, run applicable checks:

```bash
dotnet format --verify-no-changes
dotnet build DevelopmentIntelligencePlatform.sln -c Release
dotnet test DevelopmentIntelligencePlatform.sln -c Release
```

For runtime changes also run:

```powershell
.\scripts\Verify-GuiRuntime.ps1 -RunPlaywright
.\scripts\Verify-DatabaseMigrations.ps1
```

If a required command cannot run, state why. Infrastructure-unavailable is not PASS.

## Database changes

Persistence changes require EF configuration/migration updates, rollback consideration, index/uniqueness review, and integration coverage when practical. Never edit an already-applied production migration.

## Security

Follow `SECURITY.md`. Never place vulnerabilities, tokens, secrets, or private payloads in a public issue or pull request.
