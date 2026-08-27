# Portfolio publication checklist

Publication is a staged operation: **prove safety → prove the exact revision → change visibility → complete reviewer-facing metadata → release**. The repository must stay private if any high-severity secret finding is open.

## 1. Worktree safety

```powershell
python tools/verify_publication_safety.py --root . --output artifacts/publication-safety/publication-safety.json
python tools/validate_portfolio_readiness.py --root . --output artifacts/portfolio-readiness/static.json
```

Also run the GitHub `Security Scan` workflow. The local scanner is deliberately smaller than Gitleaks/CodeQL/dependency audit and does not replace them.

If a real credential is found in Git history, deleting only the current file is insufficient: rotate/revoke the credential first, then rewrite the affected history using an appropriate history-rewrite procedure and re-run the full scan.

## 2. Exact-revision CI gate

For the SHA that will be published, require successful execution of:

- `Commit Message`
- `.NET CI`
- `Python CI`
- `Native Collector CI`
- `Integration and E2E Tests`
- `GUI E2E`
- `Docker Build`
- `Security Scan`
- `Universal QA Benchmark`
- `Unreal Collector CI`
- `Portfolio Publication Readiness`

A workflow that fails **before any step starts** is infrastructure-unavailable, not a source PASS. Do not relabel it green.

The tracked `.portfolio-ci-trigger` is included in every path-filtered main verification workflow. Updating it for a portfolio milestone deliberately forces the full matrix to execute even when a subsystem source path did not otherwise change.

## 3. Visibility change

After both gates pass:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Portfolio.ps1 -MakePublic
```

The script fails closed if the worktree is dirty, the branch is not `main`, required current-SHA workflows are missing/non-successful, or the publication safety check fails.

## 4. Reviewer-facing GitHub metadata

After the repository is public, complete the repository surface before sending it to recruiters/reviewers:

- Description: keep the current one-sentence product statement focused on development intelligence for game/media/software teams.
- Topics: use accurate tags such as `developer-tools`, `game-development`, `qa-automation`, `dotnet`, `blazor`, `postgresql`, `redis`, `devops`, `unity`, and `unreal-engine` where applicable.
- Homepage: set only when a real read-only/live demo exists. Do not use a placeholder URL.
- README: confirm the first screen shows the demo plus the three engineering problems, not a long feature inventory.
- Social preview: use a readable product screenshot/architecture image if configured in GitHub.
- Main branch: enable appropriate branch/ruleset protection after CI is operational.

Metadata is presentation, not verification. It must never substitute for the exact-revision gate.

## 5. Live demo boundary

A hosted read-only demo is optional but valuable for portfolio review. If deployed:

- use deterministic/sample data only;
- keep production/repository credentials server-side;
- disable or sandbox destructive/external write operations;
- expose the exact deployed commit SHA in the UI/status page;
- link it as the GitHub Homepage only after successful smoke/security verification.

The checked-in GIF remains a demonstration even when a live demo exists.

## 6. `v1.0.0-portfolio` release

After public visibility and exact-revision green CI:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-PortfolioRelease.ps1 -Version 1.0.0-portfolio
```

The tag triggers `.github/workflows/release.yml`, which re-runs publication safety/build/tests and produces SHA-256 manifests before GitHub Release creation.

## 7. Final reviewer check

Open the repository in a signed-out/incognito browser and confirm that a reviewer can reach, without credentials:

1. the README demo and engineering summary;
2. architecture overview;
3. implementation-vs-verification status;
4. historical evidence with exact commit attribution;
5. source/tests for one core technical path (for example Project Truth);
6. release artifacts, if published.

If any of those depend on private access, the portfolio publication is not complete.
