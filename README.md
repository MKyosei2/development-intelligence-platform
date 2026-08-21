# Development Intelligence Platform

**Development Intelligence Platform (DIP)** は、ゲーム・映像・ソフトウェア開発で分散する **Git / CI / QA / Runtime / Performance / Project data** を Evidence として統合し、PM・Producer・Director・Tech Lead の意思決定を支援する browser-first の開発基盤です。

[![.NET CI](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/dotnet-ci.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/dotnet-ci.yml)
[![Integration and E2E](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/integration-test.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/integration-test.yml)
[![Security Scan](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/security-scan.yml/badge.svg)](https://github.com/MKyosei2/development-intelligence-platform/actions/workflows/security-scan.yml)

<p align="center">
  <img src="docs/09-portfolio/demo/dip-portfolio-demo.gif"
       alt="DIP portfolio demo using sample data"
       width="920" />
</p>

> The demo above uses **sample portfolio data** and is not runtime-verification evidence.  
> Exact-revision verification is kept separately under [`docs/09-portfolio/evidence/`](docs/09-portfolio/evidence/).

---

## What it solves

大規模な開発では、計画・報告・GitHub・CI・QA・ゲームエンジン・障害情報が別々の場所に存在し、

- 現在どこまで進んでいるのか
- その進捗を裏付けるEvidenceは何か
- どの変更が不具合につながったのか
- 現在リリースしてよい状態なのか
- QAそのものが正常に機能しているのか

といった判断に大きなコストが掛かります。

DIPはこれらを同じEvidence modelへ正規化し、**人の自己申告ではなく、検証可能な技術Evidenceを判断材料にすること**を中心に設計しています。

### 1. Project Truth / Evidence Graph

- Claim と Evidence を分離
- `Confirmed / PartiallyConfirmed / Unverified / Contradicted / Stale` でEvidenceとの関係を表現
- GitHub / CI / Build / Test / Runtime / Performance / Unity / Unreal / Maya などを共通Evidenceへ正規化
- Release Readiness / Risk / Blocker / Milestone / Critical Path / Change Impact を同じProject contextで追跡

### 2. Game Development QA / Defect Intelligence

- Python / C# / C++ の専用QA
- JS / TS / Go / Rust / Java / Kotlin / Ruby / PHP / Shell / Lua / Shader等をcapabilityベースで拡張
- Unity / Unreal / Native collector
- Runtime crash / timeout / memory / race / deadlock
- Sentry / OpenTelemetry ingest
- Regression origin
- Stable fingerprint
- Isolated Verified Fix
- Game Session Regression Replay
- QA Governance / Release Gate / Waiver / Ownership

### 3. Management Operations

- Daily Cockpit
- Risk / Decision / Action / Approval
- RACI
- Resource Capacity
- Meeting mode
- GitHub / Jira / Linear / Slack / Teams / Google Sheets / Calendar / Redmine / WBSとの連携層
- `Detect → Decide → Assign → Approve → Act → Verify → Close → Review` をEvidenceと接続

---

## Architecture

<p align="center">
  <img src="docs/09-portfolio/architecture.svg"
       alt="DIP architecture"
       width="960" />
</p>

詳細:

[`docs/09-portfolio/ARCHITECTURE_OVERVIEW.md`](docs/09-portfolio/ARCHITECTURE_OVERVIEW.md)

```text
Browser
  ↓
Dip.Web (Blazor)
  ↓
Dip.Api
  ├─ PostgreSQL
  │    └─ durable source of truth
  │
  ├─ Redis
  │    └─ delivery / coordination
  │
  ├─ Dip.Worker
  │    └─ async jobs / ingestion
  │
  └─ Python Analysis
       └─ analysis / intelligence

GitHub
CI
Unity
Unreal
Maya
Native
Runtime
PM tools
  ↓
Evidence ingestion
  ↓
DIP
```

Hosted deploymentではCaddyを経由してHTTPSでBrowserへ公開する構成も用意しています。

---

## Screenshots

| Project Intelligence | QA / Defect Intelligence | Management Operations |
|---|---|---|
| ![](docs/09-portfolio/screenshots/project-intelligence.png) | ![](docs/09-portfolio/screenshots/qa-defect.png) | ![](docs/09-portfolio/screenshots/management-operations.png) |

The checked-in screenshots use deterministic sample data so reviewers can understand the product without credentials.

---

## Portfolio Evidence

DIPでは、

> 「source fileが存在する」

ことと、

> 「そのrevisionが実際に検証済みである」

ことを分離しています。

### Historical verified baseline

実際のGitHub Actions artifactから固定したBenchmark / QA / Windows .NET evidence:

[`docs/09-portfolio/evidence/verified-baseline/`](docs/09-portfolio/evidence/verified-baseline/)

### Latest revision status

現在のrevisionで何が実行済み・未実行なのか:

[`docs/09-portfolio/evidence/latest-revision.md`](docs/09-portfolio/evidence/latest-revision.md)

### Runtime verification policy

[`docs/09-portfolio/runtime-verification.md`](docs/09-portfolio/runtime-verification.md)

### Representative measured results

| Evidence | Measured result |
|---|---:|
| Project list p95 | **4.174 ms** |
| Collector p95 | **34.507 ms** |
| 100 req/s error rate | **0.0000%** (1,500 requests) |
| 1M Metric query p95 | **363.796 ms** |
| QA Bug Zoo recall | **19 / 19 = 100%** |
| QA clean-control false positives | **0 / 7 = 0%** |
| QA reproducibility | **9 / 9 = 100%** |

These values are bound to the exact verified commit recorded in the evidence files.

They are **not automatically attributed to newer commits**.

---

## Technology

### Backend / Web

- C#
- .NET 8
- ASP.NET Core
- Blazor

### Data / Jobs

- PostgreSQL
- Redis
- Dip.Worker

### Analysis / Automation

- Python
- PowerShell

### Native / DCC / Game Engine

- C++
- CMake
- Unity
- Unreal Engine
- Maya

### Delivery / Observability

- Docker
- Caddy
- GitHub Actions
- OpenTelemetry
- Prometheus
- Grafana

### Verification

- Unit Test
- Integration Test
- E2E
- Playwright UI E2E
- Load Test
- Benchmark
- QA Bug Zoo
- Runtime Evidence

---

# Run locally — Optional

> **書類選考・ポートフォリオ確認のためにローカル実行する必要はありません。**
>
> GIF / Screenshots / Architecture / Evidence だけでも主要機能と検証結果を確認できます。

実際にDIPを操作したい場合は、以下の方法で **通常のFull DIP runtime** を起動できます。

簡易版・Fake Backend・SQLite版などには切り替えません。

起動対象は通常の `docker-compose.yml` に定義されている以下の構成です。

```text
PostgreSQL
Redis
Python Analysis
Dip.Api
Dip.Worker
Dip.Web
```

## Requirement

Windows環境では、事前に **Docker Desktop** のインストールのみ必要です。

Docker Desktopを手動で起動しておく必要はありません。

---

## 1. Project folder pathをコピー

エクスプローラーで、

```text
development-intelligence-platform
```

フォルダを開きます。

エクスプローラー上部のアドレスバーをクリックして、プロジェクトフォルダのフルパスをコピーしてください。

例:

```text
C:\Users\username\Documents\GitHub\development-intelligence-platform
```

環境によっては、

```text
C:\Users\username\ドキュメント\GitHub\development-intelligence-platform
```

などになります。

---

## 2. PowerShellを開く

Windows PowerShellを通常通り開きます。

開いた場所はどこでも構いません。

例えば、

```text
PS C:\Users\username>
```

の状態でも問題ありません。

---

## 3. 最初の1行だけ自分のProject pathへ変更

下のコード先頭にある、

```powershell
Set-Location -LiteralPath "<ここにdevelopment-intelligence-platformのフルパスを貼り付ける>"
```

の `<...>` 部分を、先ほどコピーしたパスへ変更してください。

例:

```powershell
Set-Location -LiteralPath "C:\Users\username\Documents\GitHub\development-intelligence-platform"
```

---

## 4. 以下をすべてPowerShellへコピー＆ペースト

```powershell
Set-Location -LiteralPath "<ここにdevelopment-intelligence-platformのフルパスを貼り付ける>"

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Development Intelligence Platform" -ForegroundColor Cyan
Write-Host " Full Runtime Startup" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

# ------------------------------------------------------------
# 1. Repository check
# ------------------------------------------------------------

Write-Host "[1/6] Checking repository..." -ForegroundColor Cyan

if (-not (Test-Path -LiteralPath ".\docker-compose.yml" -PathType Leaf)) {
    throw @"
docker-compose.yml が見つかりません。

Set-Location に指定したパスが
development-intelligence-platform フォルダそのものか確認してください。
"@
}

Write-Host "[PASS] Repository" -ForegroundColor Green


# ------------------------------------------------------------
# 2. Docker check / automatic startup
# ------------------------------------------------------------

Write-Host "[2/6] Checking Docker..." -ForegroundColor Cyan

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw @"
Dockerコマンドが見つかりません。

Docker Desktopをインストールしてから再実行してください。
"@
}

docker info *> $null

if ($LASTEXITCODE -ne 0) {

    Write-Host "Docker Engine is not running." -ForegroundColor Yellow
    Write-Host "Starting Docker Desktop automatically..." -ForegroundColor Yellow

    $DockerCandidates = @(
        "$env:ProgramFiles\Docker\Docker\Docker Desktop.exe",
        "$env:LOCALAPPDATA\Docker\Docker Desktop.exe"
    )

    $DockerDesktop = $DockerCandidates |
        Where-Object {
            Test-Path -LiteralPath $_ -PathType Leaf
        } |
        Select-Object -First 1

    if (-not $DockerDesktop) {
        throw @"
Docker Desktop.exe が見つかりません。

Docker Desktopが正常にインストールされているか確認してください。
"@
    }

    Start-Process -FilePath $DockerDesktop | Out-Null

    $DockerDeadline = (Get-Date).AddSeconds(120)

    do {
        Start-Sleep -Seconds 2

        docker info *> $null

        if ($LASTEXITCODE -eq 0) {
            break
        }

    } while ((Get-Date) -lt $DockerDeadline)

    if ($LASTEXITCODE -ne 0) {
        throw "Docker Engineが120秒以内に起動しませんでした。"
    }
}

Write-Host "[PASS] Docker Engine" -ForegroundColor Green


# ------------------------------------------------------------
# 3. Local runtime configuration
# ------------------------------------------------------------

Write-Host "[3/6] Preparing runtime configuration..." -ForegroundColor Cyan

$env:DIP_REQUIRE_AUTHENTICATION = "false"
$env:ASPNETCORE_ENVIRONMENT = "Development"

$env:API_PORT = "5080"
$env:WEB_PORT = "5081"
$env:ANALYSIS_PORT = "8100"
$env:WORKER_METRICS_PORT = "9091"

docker compose config --quiet

if ($LASTEXITCODE -ne 0) {
    throw "docker-compose.yml の構成確認に失敗しました。"
}

Write-Host "[PASS] Compose configuration" -ForegroundColor Green


# ------------------------------------------------------------
# 4. Build + Start
# ------------------------------------------------------------

Write-Host "[4/6] Building and starting Full DIP..." -ForegroundColor Cyan
Write-Host ""
Write-Host "This may take several minutes on the first run." -ForegroundColor DarkGray
Write-Host ""

docker compose up -d --build --remove-orphans

if ($LASTEXITCODE -ne 0) {

    Write-Host ""
    Write-Host "===== Containers =====" -ForegroundColor Yellow

    docker compose ps -a

    Write-Host ""
    Write-Host "===== Recent logs =====" -ForegroundColor Yellow

    docker compose logs --tail=120

    throw "DIPのビルドまたは起動に失敗しました。"
}

Write-Host ""
Write-Host "[PASS] Build / Startup" -ForegroundColor Green


# ------------------------------------------------------------
# 5. Health checks
# ------------------------------------------------------------

Write-Host "[5/6] Checking Full Runtime..." -ForegroundColor Cyan

$Checks = @(
    @{
        Name = "Python Analysis"
        Url  = "http://127.0.0.1:8100/health"
    },
    @{
        Name = "API"
        Url  = "http://127.0.0.1:5080/health"
    },
    @{
        Name = "Worker"
        Url  = "http://127.0.0.1:9091/metrics"
    },
    @{
        Name = "Web"
        Url  = "http://127.0.0.1:5081/"
    }
)

foreach ($Check in $Checks) {

    $Deadline = (Get-Date).AddSeconds(180)
    $Healthy = $false

    do {

        try {

            $Response = Invoke-WebRequest `
                -Uri $Check.Url `
                -UseBasicParsing `
                -TimeoutSec 3 `
                -ErrorAction Stop

            if (
                $Response.StatusCode -ge 200 -and
                $Response.StatusCode -lt 400
            ) {
                $Healthy = $true
            }

        }
        catch {
            $Healthy = $false
        }

        if (-not $Healthy) {
            Start-Sleep -Seconds 1
        }

    } while (
        -not $Healthy -and
        (Get-Date) -lt $Deadline
    )

    if (-not $Healthy) {

        Write-Host ""
        Write-Host "===== Containers =====" -ForegroundColor Yellow

        docker compose ps -a

        Write-Host ""
        Write-Host "===== Recent logs =====" -ForegroundColor Yellow

        docker compose logs --tail=120

        throw "Health Checkに失敗しました: $($Check.Name)"
    }

    Write-Host `
        "[PASS] $($Check.Name) - $($Check.Url)" `
        -ForegroundColor Green
}


# ------------------------------------------------------------
# 6. Open browser
# ------------------------------------------------------------

Write-Host ""
Write-Host "[6/6] Opening browser..." -ForegroundColor Cyan
Write-Host ""

Write-Host "==============================================" -ForegroundColor Green
Write-Host " DIP FULL RUNTIME IS READY" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green

Write-Host ""
Write-Host "Web:" -ForegroundColor White
Write-Host "http://127.0.0.1:5081/" -ForegroundColor Green

Write-Host ""
Write-Host "Runtime endpoints:" -ForegroundColor White
Write-Host "Python Analysis : http://127.0.0.1:8100/health"
Write-Host "API             : http://127.0.0.1:5080/health"
Write-Host "Worker          : http://127.0.0.1:9091/metrics"
Write-Host "Web             : http://127.0.0.1:5081/"

Write-Host ""

Start-Process "http://127.0.0.1:5081/"
```

---

## Successful startup

正常に起動すると、PowerShellに最低でも以下が表示されます。

```text
[PASS] Repository
[PASS] Docker Engine
[PASS] Compose configuration
[PASS] Build / Startup
[PASS] Python Analysis
[PASS] API
[PASS] Worker
[PASS] Web

DIP FULL RUNTIME IS READY
```

その後、既定ブラウザで自動的に、

```text
http://127.0.0.1:5081/
```

が開きます。

ブラウザが開くのは、以下すべてのHTTP確認が成功した後です。

| Runtime | Endpoint |
|---|---|
| Python Analysis | `http://127.0.0.1:8100/health` |
| Dip.Api | `http://127.0.0.1:5080/health` |
| Dip.Worker | `http://127.0.0.1:9091/metrics` |
| Dip.Web | `http://127.0.0.1:5081/` |

---

# Browser Verification

ブラウザが開いたら、次の順番で確認できます。

## 1. Portfolio page

最初の画面が正常に表示されることを確認します。

以下のような問題がないことを確認してください。

```text
White screen
Unhandled exception
Infinite loading
HTTP error
```

---

## 2. Local development

画面左側で、

```text
Local development
```

が表示されていることを確認します。

ローカル確認ではAuthenticationを無効化しています。

これは別Backendへ切り替えているのではなく、通常のFull runtimeをローカルDevelopment設定で実行している状態です。

---

## 3. Manual Project

左側から、

```text
Manual Project
```

を開きます。

例えば以下を入力できます。

```text
Project name:
DIP Runtime Verification

Repository URL:
https://github.com/MKyosei2/development-intelligence-platform

Default branch:
main
```

その後、

```text
Create project
```

を押します。

---

## 4. Project Dashboard

正常ならProject Dashboardへ遷移します。

この操作では、

```text
Browser
  ↓
Dip.Web
  ↓
Dip.Api
  ↓
PostgreSQL
```

の書き込み経路を実際に通ります。

したがってProject作成が成功した場合、単純な静的HTML表示だけではなく、

**Web → API → Database**

まで機能していることを確認できます。

---

## 5. Main pages

左側から以下のページを順番に確認できます。

```text
Dashboard

Repository

CI / Builds

QA

Intelligence

Management

Plan & Sources

QA Governance

System Status

Settings
```

確認ポイント:

- ページが正常に表示される
- 無限Loadingにならない
- Unhandled exceptionが発生しない
- Project selectionが維持される
- 別ページへ移動できる

---

## 6. Persistence check

一度、

```text
F5
```

でブラウザを再読み込みしてください。

作成したProjectが維持されていれば、PostgreSQLへの永続化も確認できます。

---

## 7. System Status

左側の、

```text
System Status
```

を開きます。

ブラウザ側からRuntime状態を確認できます。

---

# What is actually running?

ローカル確認時も、別のDemo実装は使用しません。

通常の、

```text
docker-compose.yml
```

から以下を起動しています。

```text
Browser
   ↓
Dip.Web
   ↓
Dip.Api
   ├─ PostgreSQL
   ├─ Redis
   ├─ Dip.Worker
   └─ Python Analysis
```

つまり、

```text
Portfolio implementation
Local runtime
Docker runtime
```

で別々のBackendを用意しているわけではありません。

同じDIPを異なる確認経路から利用しています。

---

# Stop DIP

確認終了後は、リポジトリフォルダのPowerShellで、

```powershell
docker compose down
```

を実行すると停止できます。

通常の停止ではPostgreSQLなどのpersistent Docker volumeは削除しません。

ローカルデータも含めて完全に削除したい場合のみ、

```powershell
docker compose down --volumes
```

を使用してください。

> `--volumes` はローカルのDatabase dataも削除するため、必要な場合のみ使用してください。

---

# GitHub Integration

GitHub App / GitHub OAuth等が設定されていないローカル環境では、GitHub連携画面がDisconnected状態になる場合があります。

これは異常ではありません。

GitHub integrationにはServer-side credentialが必要であり、認証情報をRepositoryへコミットしない設計になっています。

Manual ProjectはGitHub credentialなしでもローカルRuntime確認に利用できます。

---

# Verification

Core local verification on Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-Solution.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-QaGovernance.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-P2.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-PortfolioFormal.ps1 -UnityPath "<Unity.exe>"
```

A revision is not called complete merely because these runners exist.

実装済みとRuntime検証済みは区別しています。

詳細:

[`docs/IMPLEMENTATION_STATUS.md`](docs/IMPLEMENTATION_STATUS.md)

---

# Publication Safety

Production secrets must never be committed.

公開前には以下を実行します。

```bash
python tools/verify_publication_safety.py --root .
python tools/validate_portfolio_readiness.py --root .
```

Publication checklist:

[`docs/09-portfolio/PUBLICATION_CHECKLIST.md`](docs/09-portfolio/PUBLICATION_CHECKLIST.md)

---

# Repository Guide

```text
src/
├─ Dip.Domain
├─ Dip.Shared
├─ Dip.Application
├─ Dip.Infrastructure
├─ Dip.Api
├─ Dip.Worker
└─ Dip.Web

collectors/
├─ Unity
├─ Unreal
├─ Maya
├─ Native
├─ Hardware
├─ Capture Agent
└─ Crash Watchdog

services/
└─ Python Analysis

tests/
├─ Domain
├─ Application
├─ Infrastructure
├─ Integration
├─ E2E
├─ UI E2E
└─ Load / Benchmark

tools/
└─ QA / Evidence / Benchmark / Publication tooling

scripts/
└─ Windows verification / development / release helpers

docs/
└─ Architecture / Evidence / Portfolio documentation
```

---

# Reviewer Resources

### Reviewer brief

[`docs/09-portfolio/reviewer-brief.md`](docs/09-portfolio/reviewer-brief.md)

### Architecture overview

[`docs/09-portfolio/ARCHITECTURE_OVERVIEW.md`](docs/09-portfolio/ARCHITECTURE_OVERVIEW.md)

### Implementation status

[`docs/IMPLEMENTATION_STATUS.md`](docs/IMPLEMENTATION_STATUS.md)

### Runtime verification

[`docs/09-portfolio/runtime-verification.md`](docs/09-portfolio/runtime-verification.md)

### Evidence

[`docs/09-portfolio/evidence/`](docs/09-portfolio/evidence/)

### Security

[`SECURITY.md`](SECURITY.md)

### Contribution guide

[`CONTRIBUTING.md`](CONTRIBUTING.md)

---

# License

MIT — see [`LICENSE`](LICENSE).