# GitHub CI local preflight

`.\scripts\Verify-GitHubCiPreflight.ps1` reproduces the highest-value checks before spending GitHub-hosted runner minutes.

The script is intentionally fail-closed. A missing runtime/tool such as `gitleaks`, PostgreSQL prerequisites, CMake, Python 3.12, or .NET 8 is reported as a failure/blocker rather than silently treated as PASS.

Run from PowerShell:

```powershell
Set-Location <development-intelligence-platform>
.\scripts\Verify-GitHubCiPreflight.ps1
```

For diagnosis only, continue after failures:

```powershell
.\scripts\Verify-GitHubCiPreflight.ps1 -ContinueOnFailure
```

Outputs:

```text
artifacts/github-ci-preflight/report.json
artifacts/github-ci-preflight/SUMMARY.md
artifacts/github-ci-preflight/logs/
```

Do not treat this local result as GitHub Actions evidence. Its purpose is to catch source/runtime failures before the hosted-runner quota resets. The publication SHA must still complete the repository's required GitHub workflows.
