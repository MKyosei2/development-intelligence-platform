# Development Intelligence Platform (DIP)

DIPは、ゲーム・メディア・ソフトウェア開発チーム向けのDevelopment Intelligence（開発インテリジェンス）基盤です。Git / CI / QA / Runtime / Performance / Project Dataを共通のProject Contextへ集約し、出所を追跡できるEvidence（検証証拠）として扱います。そのEvidenceを基に、Project Truth、品質自動化、性能分析、リリース準備状況、Evidenceに基づくデバッグを支援します。

> **Native-first runtime:** Docker Desktop、Docker Engine、外部Queue Daemonは必須ではありません。PostgreSQLを永続的なSystem of RecordとQueue / Coordination Boundaryとして使用し、.NETとPythonサービスはホスト上で直接実行します。

## エンジニアリング原則

- **Implemented ≠ Tested ≠ Runtime Verified.** ソースが存在することや過去のEvidenceだけを根拠に、現在のリビジョンを検証済みとは扱いません。
- **ClaimとEvidenceを分離する。** Project TruthはConfirmed、Partially Confirmed、Unverified、Contradicted、Staleの状態を持ちます。
- **AIを真実の根拠にしない。** Autonomous Debuggingは `Hypothesis → Candidate → Isolated Verification → Human Review` の順で進めます。
- **広い機能範囲は意図した設計です。** Game Development Infrastructure、QA、Project Intelligence、Performance、Management、Production Operationsを別々のツールとして分断せず、共通のEvidence Modelで扱います。
- **Production Operationsはfail-closedを基本とする。** Authentication、Native QA Isolation、Backup Integrity、Exact-Revision Packaging、Readinessを明示的なGateとして扱います。

## Nativeアーキテクチャ

```text
Browser
  │
  ├── Dip.Web (.NET / Blazor)        http://127.0.0.1:5081
  │       │
  │       └── Dip.Api (.NET)         http://127.0.0.1:5080
  │               │
  │               ├── PostgreSQL     durable truth + durable queue
  │               ├── Python Analysis http://127.0.0.1:8100
  │               └── filesystem     artifacts / workspaces
  │
  └── Dip.Worker (.NET Worker)       metrics :9091
          │
          ├── PostgreSQL-backed jobs / queue
          ├── native QA processes
          └── Unity / Unreal / C++ / toolchain integrations
```

汎用Queueと既存のDurable Job subsystemはいずれもPostgreSQLを利用します。

## 必要環境

通常のローカル開発に必要なものは次のとおりです。

- .NET 8 SDK
- PostgreSQL 16、または互換性のあるサポート対象PostgreSQL
- PostgreSQL client tools: `psql`, `pg_isready`, `pg_dump`, `pg_restore`
- Python 3.12+
- Git

Node.js 22+はBrowser E2Eを実行する場合のみ必要です。Unity、Unreal、CMake、Native Analyzerも、対応するIntegration / QA機能を実行する場合のみ必要です。

## 初回起動

PostgreSQLのLogin / Databaseを一度だけ作成します。PostgreSQL管理者Shellでの例:

```sql
CREATE ROLE dip LOGIN PASSWORD 'change-me-local-only' CREATEDB;
CREATE DATABASE dip OWNER dip;
```

その後、PowerShellから実行します。

```powershell
$env:POSTGRES_HOST = "127.0.0.1"
$env:POSTGRES_PORT = "5432"
$env:POSTGRES_DB = "dip"
$env:POSTGRES_USER = "dip"
$env:POSTGRES_PASSWORD = "change-me-local-only"
.\scripts\Start-DIP.ps1
```

`Start-DIP.ps1`はPostgreSQLの確認、Python仮想環境の作成、.NET SolutionのRestore / Build、Python Analysis / API / Worker / Webのホストプロセス起動、`.dip/runtime`へのPID記録、Health / Readiness待機、UI起動までを行います。

用途に応じて次のオプションも使用できます。

```powershell
.\scripts\Start-DIP.ps1 -NoBuild -NoBrowser
.\scripts\Start-DIP.ps1 -VerifyGui
.\scripts\Stop-DIP.ps1 -KeepData
```

DIPを停止してもPostgreSQL自体は停止せず、PostgreSQL Dataも削除しません。Runtime / Artifactの削除には明示的な破壊的Switchが必要です。

## Runtime保存先

ローカルでの既定Path:

```text
.dip/runtime/          PID files + native process logs
.dip/python-venv/      Python analysis virtual environment
.dip/workspaces/       repository/QA workspaces
artifacts/durable/     durable local artifacts
artifacts/backups/     verified backups
```

ProductionではRelease Directory外の明示的なAbsolute Pathを使用してください。

## バックアップ / 復元 / DR

Native BackupはPostgreSQL Client Toolsを直接使用し、通常のFilesystem ArtifactもArchiveします。

```powershell
.\scripts\Backup-Dip.ps1
.\scripts\Restore-Dip.ps1 -BackupDirectory <path> -ConfirmRestore
.\scripts\Verify-DisasterRecovery.ps1
```

Backupには次を含みます。

```text
database.dump
artifacts.zip
backup-manifest.json
offsite-location.txt    # when an offsite mirror is configured
```

Restore前にHashを検証します。Production DeploymentではDeployment Rootとは異なるFilesystem上のOffsite Directoryが必要です。

## DBマイグレーション

```powershell
.\scripts\Verify-DatabaseMigrations.ps1
```

Verifierは一時的なNative PostgreSQL Databaseを使用し、該当する場合はPrevious / Latest EF Migration Sequenceを適用します。さらにLatest Revisionを再適用してIdempotenceを確認し、`__EFMigrationsHistory`を検査した後、一時Databaseを削除します。

## QA分離

DIPはRepository QAをDocker内ではなくNative Processとして実行します。既存のProcess ExecutorはExecutable Allowlist、Canonical Workspace Check、Environment Sanitization、Timeout / Process-Tree Termination、Resource Controlを提供します。ProductionではExternal Network / Filesystem Isolationが明示的にAttest / Configureされない限りfail-closedを維持します。

Windowsでは、専用Low-Privilege Account + ACLで限定したTemporary Workspace + Job Object Resource Limits + Firewall / Network IsolationをHard Boundaryとして想定しています。Production SecretsはQA Child Processへ渡しません。

## BenchmarkとSoak

```powershell
.\scripts\Run-PortfolioBenchmarks.ps1
```

BenchmarkではProject List Latency、Collector Ingestion、Controlled Burst Load、大規模Metric Aggregation、Paced Soak / Memory Growthを測定します。一時的なPostgreSQL Benchmark Databaseを作成し、終了後に削除します。Dockerや外部Queue Serviceは起動しません。

現在リビジョンの数値として扱えるのは、ArtifactにそのExact SHAが記録された実行結果だけです。過去のBenchmark値はBaselineであり、後続Commitへ継承しません。

## CIとRelease

`.github/workflows/docker-build.yml` は互換性のため既存Pathを維持していますが、Workflow名は現在 **Native Package Build** であり、Dockerは実行しません。以下をPublishします。

- Dip.Api
- Dip.Worker
- Dip.Web
- Python Analysis source/runtime requirements
- deployment/scripts
- EF migration bundle
- exact-SHA release manifest + SHA-256 manifest

`Live Native Deployment`はSHAごとのImmutable Release DirectoryをDeployし、`current` symlinkを切り替え、User-level systemd servicesを再起動します。その後Public Readiness / Browser Behaviorを検証し、失敗時は以前のDatabase / Artifact / Release Stateへ戻します。

Caddyは通常のHost Reverse Proxyとして `deploy/Caddyfile` を使用し、安定したLoopback Endpoint（`5080` / `5081`）へ接続します。

## レビュアー向けCapability Contract

- **Project Truth** — Freshness / Contradictionを扱う、Evidenceに紐づいたClaim State。
- **Evidence-Grounded Autonomous Debugging** — Hypothesis / Candidate生成よりも、Isolated VerificationとHuman Reviewを優先します。
- **Flexible Spreadsheet Intelligence** — Spreadsheet Ingestion / Analysisも同じProject / Evidence Modelに参加します。
- **Historical PASS ≠ Current Revision PASS** — 過去Evidenceを後続SHAへ暗黙に昇格させません。

## 中核となるProduct Area

DIPにはProject Truth / Evidence Graph、QA / Defect Intelligence、Evidence-Grounded Autonomous Debugging、Development / Repository Graph、Performance / Hardware Intelligence、Release / Readiness Evidence、Project / Management Intelligence、Spreadsheet Intelligence、Unity / Unreal / Native Collector、Integration、Operational Evidenceが含まれます。

重要なアーキテクチャ上の制約は、これらの機能がProject Scope、Evidence Provenance、Verification Stateを共有することです。互いに無関係なFeature Demoとして実装しているわけではありません。

## ポートフォリオ公開前のVerification

`docs/09-portfolio/PUBLICATION_CHECKLIST.md` を使用してください。公開対象のExact SHAについて、実際にWorkflowが成功している必要があります。Stepを1つも実行できないInfrastructure Failureは、Source FailureでもPASSでもありません。

想定するWorkflow Setには `.NET CI`、Python CI、Native / Unreal Collector、Integration / E2E、GUI E2E、**Native Package Build**、Security Scan、Universal QA Benchmark、Portfolio Publication Readinessを含みます。

## 旧Dockerファイル

互換性やMigration Referenceのため、一部の `docker-compose*.yml` / Dockerfile PathがRepository Historyに残る場合があります。これらは、サポート対象 / 既定のLocal、CI、Release、Production Runtimeではありません。新しい運用作業では上記のNative-first Pathを使用してください。
