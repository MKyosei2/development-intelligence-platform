# Portfolio publication checklist

Publication is a two-stage operation: **prove safety first, then change GitHub visibility**. The repository must stay private if any high-severity secret finding is open.

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

## 4. `v1.0.0-portfolio` release

After public visibility and exact-revision green CI:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-PortfolioRelease.ps1 -Version 1.0.0-portfolio
```

The tag triggers `.github/workflows/release.yml`, which re-runs publication safety/build/tests and produces SHA-256 manifests before GitHub Release creation.
