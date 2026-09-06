# GitHub CI Local Preflight（ローカル事前検証）

`.\scripts\Verify-GitHubCiPreflight.ps1`は、GitHub-hosted runnerのminutesを消費する前に、価値の高いcheckをlocalで再現する。

Scriptは意図的にfail-closedである。`gitleaks`、PostgreSQL prerequisite、CMake、Python 3.12、.NET 8など必要runtime/toolがない場合、黙ってPASSにせずfailure/blockerとして報告する。

PowerShellから実行:

```powershell
Set-Location <development-intelligence-platform>
.\scripts\Verify-GitHubCiPreflight.ps1
```

診断目的でfailure後も継続する場合:

```powershell
.\scripts\Verify-GitHubCiPreflight.ps1 -ContinueOnFailure
```

出力:

```text
artifacts/github-ci-preflight/report.json
artifacts/github-ci-preflight/SUMMARY.md
artifacts/github-ci-preflight/logs/
```

このlocal resultをGitHub Actions Evidenceとして扱わない。目的はhosted-runner quota reset前にsource/runtime failureを見つけることである。Publication SHA自身がrepositoryのrequired GitHub workflowを完了する必要がある。
