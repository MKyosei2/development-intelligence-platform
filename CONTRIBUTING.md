# Contributing

Thank you for contributing to Development Intelligence Platform.

## Development environment

Required tools:

- .NET SDK 8.0;
- Docker Desktop or Docker Engine with Compose V2;
- Git;
- Visual Studio 2022, Visual Studio Code, or JetBrains Rider.

Start the local infrastructure:

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

Do not combine unrelated refactoring, formatting, behavior changes, and
dependency updates in one pull request.

## Coding rules

- Preserve the dependency direction:
  `Domain <- Application <- Infrastructure <- Api / Worker`.
- Keep shared transport contracts in `Dip.Shared`.
- Use UTC for persisted timestamps.
- Pass `CancellationToken` through asynchronous operations.
- Avoid blocking calls such as `.Result` and `.Wait()`.
- Validate external input at the boundary.
- Keep secrets and production data out of source control and logs.
- Add or update tests when behavior changes.
- Keep public APIs and JSON contracts backward compatible unless the breaking
  change is documented.

## Commit messages

Use short, imperative messages that describe one logical change.

Examples:

```text
Add collector report validation
Fix duplicate pipeline run handling
Document local Redis setup
```

## Pull requests

A pull request should contain:

- purpose and scope;
- implementation summary;
- testing performed;
- migration or configuration impact;
- screenshots for dashboard changes;
- known limitations and follow-up work.

Before requesting review, confirm that:

```bash
dotnet format --verify-no-changes
dotnet build DevelopmentIntelligencePlatform.sln -c Release
dotnet test DevelopmentIntelligencePlatform.sln -c Release
```

If a command cannot be run, state the reason in the pull request.

## Database changes

Entity changes that affect persistence require:

- an EF Core configuration update;
- a migration;
- rollback consideration;
- index and uniqueness review;
- integration-test coverage when practical.

Never edit an already-applied production migration.

## Security

Follow `SECURITY.md`. Never place vulnerabilities, tokens, secrets, or private
payloads in a public issue or pull request.


## Commit subjects

Use Conventional Commit-style subjects so a reviewer can understand change intent from history:

```text
feat(qa): add runtime defect evidence
fix(ci): restore integration test startup
refactor(web): split management cockpit components
docs(portfolio): add verified evidence index
```

Keep the subject imperative and at most 72 characters. Install the optional local template with `git config commit.template .gitmessage`. Pull requests validate commit subjects automatically.
