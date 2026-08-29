# Portfolio publication checklist

Publication is staged: **prove safety → prove the exact revision → change visibility → complete reviewer-facing metadata → release**. Keep the repository private if any high-severity secret finding is unresolved.

## Safety

```powershell
python tools/verify_publication_safety.py --root . --output artifacts/publication-safety/publication-safety.json
python tools/validate_portfolio_readiness.py --root . --output artifacts/portfolio-readiness/static.json
```

Also require the GitHub `Security Scan` workflow. Historical output does not replace a current-SHA run.

## Exact-revision CI gate

For the publication SHA require successful real execution of:

- `Commit Message`
- `.NET CI`
- `Python CI`
- `Native Collector CI`
- `Integration and E2E Tests`
- `GUI E2E`
- `Native Package Build`
- `Security Scan`
- `Universal QA Benchmark`
- `Unreal Collector CI`
- `Portfolio Publication Readiness`

A job that stops before executing steps due to runner/account/platform availability is infrastructure-blocked: neither source failure nor PASS.

The Native Package Build must produce an exact-SHA archive and SHA-256 manifest. Docker image status is not part of the supported publication gate.

## Runtime evidence

Run:

```powershell
.\scripts\Verify-GuiRuntime.ps1 -RunPlaywright
.\scripts\Run-PortfolioBenchmarks.ps1
.\scripts\Verify-DatabaseMigrations.ps1
.\scripts\Verify-DisasterRecovery.ps1
```

Only evidence whose manifest/sourceCommit equals the publication SHA can support a current-revision claim.

## Visibility and reviewer check

After the gates pass, use the existing portfolio publication scripts to change repository visibility/release state. Review the repository signed out and ensure the README, architecture, implementation-vs-verification boundary, historical/current evidence distinction, source/tests, and release artifacts are reachable.

Accurate metadata topics should include `developer-tools`, `game-development`, `qa-automation`, `dotnet`, `blazor`, `postgresql`, `devops`, `unity`, and `unreal-engine` as applicable. Redis should not be advertised as a current runtime dependency.
