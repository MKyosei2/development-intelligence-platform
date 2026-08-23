# Development Intelligence Platform

**Development Intelligence Platform (DIP)** は、ゲーム・映像・ソフトウェア開発で分散する **Git / CI / QA / Runtime / Performance / Project data** を Evidence として統合し、PM・Producer・Director・Tech Lead の意思決定を支援する browser-first の開発基盤です。

[![.NET CI](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/dotnet-ci.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/dotnet-ci.yml)
[![Integration and E2E](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/integration-test.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/integration-test.yml)
[![Security Scan](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/security-scan.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/security-scan.yml)

<p align="center">
  <img src="docs/09-portfolio/demo/dip-portfolio-demo.gif" alt="DIP portfolio demo using sample data" width="920" />
</p>

> The demo above uses **sample portfolio data** and is not runtime-verification evidence. Exact-revision verification is kept separately under [`docs/09-portfolio/evidence/`](docs/09-portfolio/evidence/).

## What it solves

大規模な開発では、計画・報告・GitHub・CI・QA・ゲームエンジン・障害情報が別々の場所に存在し、「進捗していると言える根拠」「今リリースしてよいか」「どの変更が不具合につながったか」の判断コストが上がります。DIP はこれらを同じ Evidence model に正規化し、**人の自己申告ではなく、検証可能な技術Evidenceを判断材料にする**ことを中心に設計しています。

### 1. Project Truth / Evidence Graph

- Claim と Evidence を分離し、`Confirmed / PartiallyConfirmed / Unverified / Contradicted / Stale` で関係を表現
- GitHub / CI / Build / Test / Runtime / Performance / Unity / Unreal / Maya などを共通Evidenceへ正規化
- Release Readiness、Risk、Blocker、Milestone、Critical Path、Change Impactを同じProject contextで追跡

### 2. Game Development QA / Defect Intelligence

- Python / C# / C++ の専用QAに加え、JS/TS/Go/Rust/Java/Kotlin/Ruby/PHP/Shell/Lua/Shader等をcapabilityベースで拡張
- Unity / Unreal / Native collector、runtime crash/timeout/memory/race/deadlock、Sentry / OpenTelemetry ingest
- Regression origin、stable fingerprint、isolated Verified Fix、Game Session Regression Replay

### 3. Management Operations

- Daily Cockpit、Risk / Decision / Action / Approval、RACI、Resource Capacity、Meeting mode
- GitHub / Jira / Linear / Slack / Teams / Google Sheets / Calendar / Redmine / WBSとの連携層
- `Detect → Decide → Assign → Approve → Act → Verify → Close → Review` をEvidenceと結びつける

## Architecture

<p align="center">
  <img src="docs/09-portfolio/architecture.svg" alt="DIP architecture" width="960" />
</p>

詳細: [`docs/09-portfolio/ARCHITECTURE_OVERVIEW.md`](docs/09-portfolio/ARCHITECTURE_OVERVIEW.md)

```text
Browser
  ↓ HTTPS
Caddy
  ├─ Dip.Web (Blazor)
  └─ Dip.Api
       ├─ PostgreSQL  ← durable source of truth
       ├─ Redis       ← delivery / coordination
       ├─ Dip.Worker  ← async jobs / ingestion
       └─ Python Analysis
             ↑
GitHub / CI / Unity / Unreal / Maya / Native / Runtime / PM tools
```

## Portfolio evidence

DIP は「source file が存在する」ことと「そのrevisionが検証済みである」ことを分けます。

- **Historical verified baseline**: 実際のGitHub Actions artifactから固定した Benchmark / QA / Windows .NET evidence  
  → [`docs/09-portfolio/evidence/verified-baseline/`](docs/09-portfolio/evidence/verified-baseline/)
- **Latest package/revision status**: 現在のrevisionで何が実行済み／未実行かを明示  
  → [`docs/09-portfolio/evidence/latest-revision.md`](docs/09-portfolio/evidence/latest-revision.md)
- **Runtime verification policy**: [`docs/09-portfolio/runtime-verification.md`](docs/09-portfolio/runtime-verification.md)

Historical verified baseline の代表値:

| Evidence | Measured result |
|---|---:|
| Project list p95 | **4.174 ms** |
| Collector p95 | **34.507 ms** |
| 100 req/s error rate | **0.0000%** (1,500 requests) |
| 1M Metric query p95 | **363.796 ms** |
| QA Bug Zoo recall | **19 / 19 = 100%** |
| QA clean-control false positives | **0 / 7 = 0%** |
| QA reproducibility | **9 / 9 = 100%** |

These values are bound to the exact verified commit recorded in the evidence files; they are **not automatically attributed to newer commits**.

## Screenshots

| Project Intelligence | QA / Defect Intelligence | Management Operations |
|---|---|---|
| ![](docs/09-portfolio/screenshots/project-intelligence.png) | ![](docs/09-portfolio/screenshots/qa-defect.png) | ![](docs/09-portfolio/screenshots/management-operations.png) |

The checked-in screenshots use deterministic sample data so reviewers can understand the product without credentials. For real runtime captures, see [`docs/09-portfolio/demo/README.md`](docs/09-portfolio/demo/README.md).

## Technology

- **Backend / Web:** .NET 8, C#, ASP.NET Core, Blazor
- **Data / jobs:** PostgreSQL, Redis, Worker
- **Analysis / automation:** Python, PowerShell
- **Native / engine:** C++, CMake, Unity, Unreal, Maya collectors
- **Delivery / observability:** Docker, Caddy, GitHub Actions, OpenTelemetry, Prometheus/Grafana
- **Tests:** Unit, Integration, E2E, UI E2E (Playwright), load/benchmark, QA Bug Zoo

## Run for development

End users use only a browser. Developers can run the hosted stack locally with the repository deployment configuration.

```bash
cp .env.example .env
docker compose -f docker-compose.web.yml up --build
```

### Use DIP with real GitHub repositories

For a persistent single-user localhost installation, copy `.env.example` to `.env` and configure either `DIP_GITHUB_TOKEN` (a repository-scoped fine-grained read-only token) or the GitHub App installation fields. The token/App needs repository Metadata, Contents, Actions and Pull requests read access for the repositories DIP should inspect.

Start the normal persistent stack:

```bash
docker compose up -d --build
```

Then open `http://localhost:5081/projects/import/github`. Importing with **Synchronize workspace now** and **Run initial QA after import** enabled downloads the selected branch into the persistent QA workspace, runs technology-aware QA, and stores Critical/Warning findings as durable Issues linked to the GitHub commit and DIP pipeline evidence. Subsequent QA runs synchronize a stale workspace before analysis.

Named volumes preserve PostgreSQL, Redis, QA workspaces, artifacts and the NuGet cache across normal `stop`, `start` and `down` operations. Do not use `docker compose down --volumes` unless those persistent data sets are intentionally being erased.

The System Status page exposes repository-access configuration separately from browser OAuth and reports whether the core QA runtime toolchain is present.

Production secrets must never be committed. Before making a fork/repository public, run:

```bash
python tools/verify_publication_safety.py --root .
python tools/validate_portfolio_readiness.py --root .
```

Windows release/publication helpers are documented in [`docs/09-portfolio/PUBLICATION_CHECKLIST.md`](docs/09-portfolio/PUBLICATION_CHECKLIST.md).

## Verification

Core local verification on Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-Solution.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-QaGovernance.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-P2.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-PortfolioFormal.ps1 -UnityPath "<Unity.exe>"
```

A revision is not called complete merely because these runners exist. See [`docs/IMPLEMENTATION_STATUS.md`](docs/IMPLEMENTATION_STATUS.md) for the implementation/runtime distinction.

## Repository guide

```text
src/                    Domain / Application / Infrastructure / API / Worker / Web
collectors/             Unity / Unreal / Maya / Native / Runtime collectors
services/               Python analysis service
tests/                  Unit / Integration / E2E / UI E2E / load tests
tools/                  QA, evidence, benchmark and publication tooling
scripts/                Windows verification and release helpers
docs/09-portfolio/      Reviewer-facing architecture, demo and evidence
```

- Reviewer brief: [`docs/09-portfolio/reviewer-brief.md`](docs/09-portfolio/reviewer-brief.md)
- Detailed implementation status: [`docs/IMPLEMENTATION_STATUS.md`](docs/IMPLEMENTATION_STATUS.md)
- Security policy: [`SECURITY.md`](SECURITY.md)
- Contribution guide: [`CONTRIBUTING.md`](CONTRIBUTING.md)

## License

MIT — see [`LICENSE`](LICENSE).
