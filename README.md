# Development Intelligence Platform

Development Intelligence Platform（DIP）は、GitHub、CI/CD、ゲームエンジン、DCCツール、テスト、ビルド、性能計測に分散した情報を統合し、PM・プロデューサー・テックリードの意思決定を支援する基盤です。

## プロジェクト状態

**P0の主要コードと自動検証経路は実装済みです。ただし、最新コミットのCIがすべて成功するまでは「検証完了」とは扱いません。**

完了条件は次のいずれかです。

1. 最新コミットで`.NET CI`、`Integration and E2E Tests`、Pythonテスト、Native Collector CIがすべて成功する。
2. Windowsで次を実行し、`artifacts/final-verification/PROJECT_COMPLETION.txt`が生成される。

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-Solution.ps1"
```

詳細は[`docs/IMPLEMENTATION_STATUS.md`](docs/IMPLEMENTATION_STATUS.md)を参照してください。

## P0の処理経路

```text
Collector / GitHub Webhook
  -> ASP.NET Core API
  -> PostgreSQL + Outbox
  -> Redis Stream
  -> Worker
  -> Quality Rule / Quality Gate
  -> Dashboard API
  -> Blazor Dashboard
```

## 構成

| プロジェクト | 役割 |
|---|---|
| `Dip.Domain` | Entity、Value Object、状態遷移 |
| `Dip.Shared` | API・Worker・Collector間のContract |
| `Dip.Application` | ユースケース、品質ルール、Repository Interface |
| `Dip.Infrastructure` | PostgreSQL、Redis、GitHub連携 |
| `Dip.Api` | HTTP API、Webhook、Collector Report受付 |
| `Dip.Worker` | Outbox配信、Quality Gate評価、再試行、Dead Letter |
| `Dip.Web` | Blazor Dashboard |
| `services/python-analysis` | FastAPI分析サービス |
| `collectors/native-collector` | C++ Native Collector |
| `tests/Dip.Application.Tests` | Application Unit Test |
| `tests/Dip.Infrastructure.Tests` | Infrastructure Unit Test |
| `tests/Dip.IntegrationTests` | API / PostgreSQL Integration Test |
| `tests/Dip.EndToEndTests` | API・Redis・Workerを使うLive E2E Test |

## 必要環境

- .NET SDK 8.0
- Docker DesktopまたはDocker Engine + Compose V2
- PowerShell 5.1以上またはPowerShell 7
- Git
- CMake / C++20対応コンパイラ（Native Collectorを検証する場合）
- Python 3.11以上（分析サービスを検証する場合）

## セットアップ

```powershell
git clone <repository-url>
cd development-intelligence-platform
docker compose up -d
dotnet restore DevelopmentIntelligencePlatform.sln
dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
```

Unit Test：

```powershell
dotnet test tests/Dip.Domain.Tests/Dip.Domain.Tests.csproj -c Release --no-build
dotnet test tests/Dip.Application.Tests/Dip.Application.Tests.csproj -c Release --no-build
dotnet test tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj -c Release --no-build
```

Integration Test：

```powershell
dotnet test tests/Dip.IntegrationTests/Dip.IntegrationTests.csproj -c Release --no-build
```

Live E2E TestはPostgreSQL、Redis、`Dip.Api`、`Dip.Worker`を起動した状態で実行します。

```powershell
$env:DIP_API_URL = "http://127.0.0.1:7001"
dotnet test tests/Dip.EndToEndTests/Dip.EndToEndTests.csproj -c Release --no-build
```

## アプリケーション起動

```powershell
dotnet run --project src/Dip.Api/Dip.Api.csproj
dotnet run --project src/Dip.Worker/Dip.Worker.csproj
dotnet run --project src/Dip.Web/Dip.Web.csproj
```

主要環境変数：

```text
ConnectionStrings__PostgreSql
Redis__ConnectionString
GitHub__WebhookSecret
GitHub__Token
Api__BaseUrl
```

## Quality Gate

品質判定は、Collectorが付与した明示的Severity、未解決Issue、Baseline差分を評価します。

- `fps`、`throughput`等は低下をRegressionとして扱う。
- `memory`、`latency`、`frame_time`、`size`等は増加をRegressionとして扱う。
- 10%以上をWarning、20%以上をCriticalとして扱う。
- Criticalが1件以上なら`Failed`、Warningが1件以上なら`Warning`、違反なしなら`Passed`。

## GitHub Webhook

- Payload URL: `https://<host>/api/v1/webhooks/github`
- Content type: `application/json`
- Events: `push`、`pull_request`、`check_run`、`workflow_run`、`ping`
- HMAC SHA-256署名検証とDelivery IDによる冪等性を実装しています。

## ライセンス

本リポジトリはルートの`LICENSE`に従います。
