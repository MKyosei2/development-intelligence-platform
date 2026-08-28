# Development Intelligence Platform

**Development Intelligence Platform（DIP）** は、ゲーム・映像・ソフトウェア制作で分散する **Git / CI / QA / Runtime / Performance / Project data** を、追跡可能な Evidence として同一の Project Context に統合する browser-first の開発基盤です。

[![.NET CI](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/dotnet-ci.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/dotnet-ci.yml)
[![Integration and E2E](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/integration-test.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/integration-test.yml)
[![GUI E2E](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/gui-e2e.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/gui-e2e.yml)
[![Security Scan](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/security-scan.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/security-scan.yml)

<p align="center">
  <img src="docs/09-portfolio/demo/dip-portfolio-demo.gif" alt="DIP portfolio demo using deterministic sample data" width="920" />
</p>

> GIF・スクリーンショットは決定論的サンプルデータによるデモです。**Implemented ≠ Tested ≠ Runtime Verified** を原則とし、過去RevisionのPASSを最新RevisionのPASSとして扱いません。

## What problem does DIP solve?

制作現場では、実装状況、GitHub、CI、QA、ゲームエンジン、Runtime、性能、WBS/Spreadsheetが別々に存在します。DIPはそれらをEvidenceへ正規化し、次の問いを同じ画面・同じRevisionで判断できるようにします。

- 報告された進捗のうち、客観的Evidenceで確認できる範囲はどこか
- 現在RevisionをReleaseしてよいか
- Regressionが製品側・QA側・Infrastructure側のどこにあるか
- Failureと変更Commitの関係は何か
- AIが出した修正候補が隔離検証でも成立するか
- Unity / Unreal / Native Runtimeの品質・性能EvidenceをどうProject判断へ接続するか

```text
Development / Production activity
              ↓
           Evidence
              ↓
Universal Project / Development Graph
              ↓
Deterministic evaluation
              ↓
Risk / Progress / QA / Regression / Debug hypothesis
              ↓
Action → Isolated verification → Human review
```

## Three core engineering contributions

### 1. Project Truth / Evidence Graph

人が報告した **Claim** と機械的に観測した **Evidence** を分離し、`Confirmed / PartiallyConfirmed / Unverified / Contradicted / Stale` として評価します。

Reported ProgressとEvidence-backed Progressを別々に保持し、未知のStatusは0%へ変換せず `unknown` として残します。ThresholdやPenaltyは `ProjectTruthPolicy` に集約し、Magic NumberをEvaluatorへ散在させません。

### 2. Evidence-grounded QA / Autonomous Debug

QA Finding、Regression baseline、Flaky、Soak、Fuzz、Mutation、Crash、Runtime Event、Root-cause candidateをEvidenceとして関連付けます。Autonomous DebugはAIをTruth Sourceにせず、次の境界を維持します。

```text
Hypothesis
  → Candidate change
  → Isolated verification
  → Evidence comparison
  → Human review
```

Soak harnessは「観測時間完了のためHarnessが終了したプロセス」と「対象自身のCrash」を区別します。

### 3. Game / production evidence ingestion

Unity Collector、Unreal Collector、Native Collector、Spreadsheet / Project Management adapterを既存のCollectorReport/Evidenceモデルへ統合します。

UnityはUPM/Editor/Runtime経路を持ちます。Unrealは依存なしの静的・Log・Insights収集に加え、`--unreal-editor-cmd` 指定時は UnrealEditor-Cmd を直接起動してAutomation Testを実行し、生成されたReportとEngine exitをEvidenceへ含めます。

## Architecture

<p align="center">
  <img src="docs/09-portfolio/architecture.svg" alt="DIP architecture" width="960" />
</p>

```text
Browser
  ↓
Dip.Web (Blazor)
  ↓
Dip.Api
  ├─ PostgreSQL       durable source of truth
  ├─ Redis            authenticated coordination / delivery
  ├─ Dip.Worker       durable asynchronous jobs
  └─ Python Analysis  authenticated analysis service

External evidence
  ├─ GitHub / CI
  ├─ Unity / Unreal
  ├─ Maya / Native / Runtime
  ├─ Spreadsheet
  └─ Project-management integrations
```

ProductionではPostgreSQLをDurable Source of Truthとし、Redis・Worker・Analysisの一時障害がProject Stateを勝手にTruthへ昇格させない構成です。`/api/v1/system/live` はProcess liveness、`/api/v1/system/ready` はtraffic admissionに必要なcritical dependencyのみを確認し、重いtoolchain/integration診断は認証済み `/status` に分離します。

詳細: [Architecture Overview](docs/09-portfolio/ARCHITECTURE_OVERVIEW.md)

## Game-development verification path

ゲーム開発基盤としての主要な流れは次です。

```text
Unity / Unreal / Native runtime
        ↓
CollectorReport / Runtime Event
        ↓
QA Finding / Performance / Incident
        ↓
Regression / Release Readiness
        ↓
Project Truth
        ↓
Developer / Lead decision
```

Unreal engine-native example:

```bash
python collectors/unreal-collector/collector.py \
  --root ./MyGame \
  --project-id 00000000-0000-0000-0000-000000000001 \
  --repository https://github.com/example/game \
  --commit-sha 0123456789abcdef0123456789abcdef01234567 \
  --external-run-id unreal-ci-1 \
  --unreal-editor-cmd "/path/to/UnrealEditor-Cmd" \
  --automation-filter Project \
  --output artifacts/unreal-report.json
```

## Performance / Soak evidence

Portfolio benchmarkはDevelopment環境ではなく専用の `Benchmark` runtime profileを使用し、結果JSONへruntime profile・exact source commit・dirty stateを記録します。標準Soakは **900秒 / 25 req/s** です。Release/Nightly用途では引数でさらに長い時間を指定できます。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Run-PortfolioBenchmarks.ps1 -UpdateDocumentation
```

**最新Revisionに対して実測していない数値は掲載しません。** 現在Revisionの測定状況は [benchmark-results.md](docs/09-portfolio/benchmark-results.md) を参照してください。

### Historical verified baseline

以下はコミットを固定した過去の検証Evidenceであり、比較用Baselineです。Historical results are **not automatically attributed to newer commits**.

| Scenario | Historical result |
|---|---:|
| Project list p95 | 4.174 ms |
| Collector p95 | 34.507 ms |
| 100 req/s error rate | 0.0000% |
| 1M Metric query p95 | 363.796 ms |
| QA Bug Zoo | 19 / 19 detected |
| Clean-control false positives | 0 / 7 |
| Stable-fingerprint reproduction | 9 / 9 |

Evidence: [verified-baseline](docs/09-portfolio/evidence/verified-baseline/)

## Reliability and production safety

Production deploymentはfail-closedを基本にします。

- Python Analysis: `APP_ENV=production` ではAPI key未設定で起動拒否
- Redis: production composeでpassword必須
- PostgreSQL / JWT / GitHub OAuth: ProductionSecurityGuardで危険なdefaultを拒否
- Durable Worker: lease ownership / heartbeat / stale-result discard
- Backup: DB dump + artifact archive + SHA-256 + atomic publication
- Restore: preflight → safety backup → restore → post-check。失敗時はsafety backupへ自動rollback
- Publication: Secret scan / static readiness / exact-SHA CI gateを通過するまで公開完了としない

## Quick start

ローカル開発用:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-DIP.ps1
```

またはDocker Compose:

```bash
docker compose up --build
```

Hosted/production構成は `deploy/web.env.example` を基に必要Secretを設定し、`docker-compose.web.yml` を使用します。SecretをRepositoryへCommitしないでください。

## Verification

Sourceの存在だけでは完成扱いしません。正式なWindows portfolio verification:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-PortfolioFormal.ps1 \
  -UnityPath "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe"
```

個別確認:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-DatabaseMigrations.ps1 -Configuration Release
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-DisasterRecovery.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-QaGovernance.ps1 -Configuration Release
powershell -ExecutionPolicy Bypass -File .\scripts\Run-PortfolioBenchmarks.ps1
```

GitHub-hosted runnerがstep開始前に失敗したRunは `Infrastructure Blocked` とし、Source FailureにもPASSにも変換しません。Portfolio-critical workflowはmanual dispatch可能ですが、**実際のworkflowが成功するまではGreenと記載しません**。

最新RevisionのEvidence境界: [Latest revision evidence](docs/09-portfolio/evidence/latest-revision.md)

## Technology

- **Backend / Worker:** .NET 8, C#, ASP.NET Core
- **Web:** Blazor
- **Persistence:** PostgreSQL, EF Core
- **Coordination:** Redis
- **Analysis / Collectors:** Python
- **Game / Native:** Unity, Unreal Engine, C++, CMake
- **QA / E2E:** xUnit, pytest, Playwright, GitHub Actions
- **Operations:** Docker Compose, OpenTelemetry, Prometheus, Grafana

## Reviewer guide

短時間で確認する場合は次の順序を推奨します。

1. このREADMEのProblem / Core Contributions / Architecture
2. [Engineering Case Study](docs/09-portfolio/ENGINEERING_CASE_STUDY.md)
3. [Implementation Status](docs/IMPLEMENTATION_STATUS.md)
4. [Latest Revision Evidence](docs/09-portfolio/evidence/latest-revision.md)
5. `ProjectTruth`, `P2ExperimentRunner`, Unity/Unreal Collectorのsource/tests

このRepositoryはGameplayそのもののShowcaseではなく、**game-development infrastructure / QA automation / developer productivity / backend-platform / release engineering / TA-TD tooling** を主題としています。

## License

See [LICENSE](LICENSE).
