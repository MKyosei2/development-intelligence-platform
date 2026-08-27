# Contributing

Thank you for contributing to Development Intelligence Platform.

## Development environment

Required tools:

- .NET SDK 8.0;
- Docker Desktop or Docker Engine with Compose V2;
- Git;
- Visual Studio 2022, Visual Studio Code, or JetBrains Rider.

Start local infrastructure:

```bash
docker compose up -d
```

Restore, build, and test:

```bash
dotnet restore DevelopmentIntelligencePlatform.sln
dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
dotnet test DevelopmentIntelligencePlatform.sln -c Release --no-build
```

## Branches

Create a focused branch from the latest default branch.

Recommended names:

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
- Avoid blocking calls such as `.Result` and `.Wait()`.
- Validate external input at the boundary.
- Keep secrets and production data out of source control and logs.
- Add or update tests when behavior changes.
- Keep public APIs and JSON contracts backward compatible unless a breaking change is explicitly documented.
- Keep implemented-source claims separate from exact-revision runtime verification.

## Commit subjects

Use Conventional Commit-style subjects so reviewers can understand intent directly from history. Generic subjects such as `改善`, `修正`, `update`, or numbered variants do not describe the engineering change and should not be used for new commits.

Examples:

```text
feat(qa): add runtime defect evidence
fix(ci): restore integration test startup
perf(metrics): reduce million-row query latency
refactor(web): split management cockpit components
docs(portfolio): explain exact-revision evidence
```

Keep the subject imperative and at most 72 characters. Install the optional local template with:

```bash
git config commit.template .gitmessage
```

Pull requests and pushes to protected development branches validate commit subjects automatically.

## Pull requests

A pull request should contain:

- purpose and scope;
- implementation summary;
- testing performed;
- migration or configuration impact;
- screenshots for dashboard changes;
- known limitations and follow-up work;
- exact-revision evidence when the PR claims measured/runtime behavior.

Before requesting review, confirm that the applicable checks run locally or in CI:

```bash
dotnet format --verify-no-changes
dotnet build DevelopmentIntelligencePlatform.sln -c Release
dotnet test DevelopmentIntelligencePlatform.sln -c Release
```

If a required command cannot run, state the reason. Infrastructure-unavailable is not equivalent to PASS.

## Database changes

Entity changes that affect persistence require:

- an EF Core configuration update;
- a migration;
- rollback consideration;
- index and uniqueness review;
- integration-test coverage when practical.

Never edit an already-applied production migration.

## Security

Follow `SECURITY.md`. Never place vulnerabilities, tokens, secrets, or private payloads in a public issue or pull request.
