# Development Intelligence Platform

Development Intelligence Platform（DIP）は、GitHub、CI/CD、ゲームエンジン、DCCツール、テスト、ビルド、性能計測などに分散した情報を統合し、PM・プロデューサー・テックリードの意思決定を支援する基盤です。

## プロジェクト状態

**P0 MVPはコード完成状態です。**

最終的な終了宣言は、リポジトリに含まれる検証スクリプトが、ビルドだけでなく実サービスを使った一連の処理まで完走した場合にのみ発行します。

```powershell
powershell -ExecutionPolicy Bypass `
    -File ".\scripts\Verify-Solution.ps1"
```

成功時は、次が表示されます。

```text
PROJECT COMPLETION VERIFIED
```

同時に、次の証拠ファイルが生成されます。

```text
artifacts/final-verification/PROJECT_COMPLETION.txt
```

このファイルが存在し、内容が`PROJECT COMPLETION VERIFIED`であることを、本プロジェクトのP0 MVP終了宣言とします。途中で失敗した場合は終了宣言を生成せず、`PROJECT_COMPLETION_FAILED.txt`と各プロセスのログを残します。

検証後もAPI、Worker、Web、PostgreSQL、Redisを動かし続ける場合は、次を使います。

```powershell
powershell -ExecutionPolicy Bypass `
    -File ".\scripts\Verify-Solution.ps1" `
    -KeepServicesRunning
```

## 最終検証で保証する範囲

検証スクリプトは、次を順番に確認します。

1. .NET 8 SDK、Docker CLI、Gitの存在
2. Docker Engineの稼働。停止中ならDocker Desktopを自動起動
3. 専用ComposeプロジェクトによるPostgreSQLとRedisの起動
4. Solution全体のRestoreとRelease Build
5. Domain Unit Test
6. PostgreSQLを使用するAPI Integration Test
7. `Dip.Api`、`Dip.Worker`、`Dip.Web`の実起動
8. Project作成
9. Collector Reportの受付・検証・DB保存
10. OutboxからRedisへの発行
11. WorkerによるQuality Gate評価
12. Dashboardへの評価結果反映
13. PipelineRun=`Succeeded`、Quality Gate=`Passed`

Docker Desktop自体が未インストール、またはWSL 2が破損している場合は、ソースコードからDocker Engineを作り出すことはできません。その場合、スクリプトは曖昧に続行せず、修復対象を明示して停止します。

## 現在の構成

| プロジェクト | 役割 |
|---|---|
| `Dip.Domain` | Entity、Value Object、状態遷移、ドメインルール |
| `Dip.Shared` | API・Worker・Collector間の共通Contract |
| `Dip.Application` | ユースケース、検証、Repository／外部サービスInterface |
| `Dip.Infrastructure` | PostgreSQL、GitHub API、Redis Queue、外部サービス実装 |
| `Dip.Api` | HTTP API、Webhook、Collector Report受付 |
| `Dip.Worker` | Outbox配信、非同期処理、Quality Gate評価、再試行 |
| `Dip.Web` | PM・テックリード向けDashboard |
| `Dip.Domain.Tests` | Domain層のUnit Test |
| `Dip.IntegrationTests` | PostgreSQLを使用するAPI Integration Test |

## アーキテクチャ

```text
GitHub / CI / Unity / Maya / Unreal / Generic JSON
                         |
                         v
                  ASP.NET Core API
                         |
               PostgreSQL + Outbox
                         |
                         v
                  Redis Job Queue
                         |
                         v
                 Quality Worker
                         |
                         v
                 Quality Gate Result
                         |
                         v
                 Blazor Dashboard
```

依存方向は`Domain <- Application <- Infrastructure <- Api / Worker`を基準にします。`Dip.Shared`はAPI、Web、Worker、Collector間のContractを提供します。

## 必要環境

- .NET SDK 8.0
- Docker DesktopまたはDocker Engine + Compose V2
- Windows PowerShell 5.1以上、またはPowerShell 7
- Visual Studio 2022 17.x、Visual Studio Code、またはJetBrains Rider
- Git

## 通常セットアップ

```powershell
git clone <repository-url>
cd development-intelligence-platform

docker compose up -d

dotnet restore DevelopmentIntelligencePlatform.sln
dotnet build DevelopmentIntelligencePlatform.sln `
    -c Release `
    --no-restore

dotnet test DevelopmentIntelligencePlatform.sln `
    -c Release `
    --no-build
```

通常の`docker compose up -d`は、P0 MVPに必要なPostgreSQLとRedisを起動します。

| サービス | ポート | 用途 |
|---|---:|---|
| PostgreSQL | `5432` | 永続データ |
| Redis | `6379` | Queue |
| MinIO API | `9000` | 任意のArtifact保存 |
| MinIO Console | `9001` | 任意のMinIO管理画面 |

MinIOはP0 MVPの必須経路ではないため、`storage`プロファイルへ分離しています。

```powershell
docker compose --profile storage up -d
```

状態確認と停止：

```powershell
docker compose ps
docker compose logs -f
docker compose down
```

データも削除する場合だけ、次を使用してください。

```powershell
docker compose down -v
```

最終検証スクリプトは、通常環境と衝突しない専用Composeプロジェクトと専用ポートを使い、検証終了時にその専用データだけを削除します。

## アプリケーション起動

API、Worker、Webは別ターミナルで起動します。

```powershell
dotnet run --project src/Dip.Api/Dip.Api.csproj
dotnet run --project src/Dip.Worker/Dip.Worker.csproj
dotnet run --project src/Dip.Web/Dip.Web.csproj
```

Visual Studioでは、次をマルチスタートアップに設定します。

| プロジェクト | アクション |
|---|---|
| `Dip.Api` | 開始 |
| `Dip.Worker` | 開始 |
| `Dip.Web` | 開始 |
| その他 | なし |

## ローカル設定

TokenやSecretをGit管理対象へ直接書かず、User Secretsまたは環境変数を使用します。

```powershell
dotnet user-secrets init `
    --project src/Dip.Api/Dip.Api.csproj

dotnet user-secrets set `
    "ConnectionStrings:PostgreSql" `
    "Host=localhost;Port=5432;Database=dip;Username=dip;Password=dip-local-password" `
    --project src/Dip.Api/Dip.Api.csproj

dotnet user-secrets set `
    "GitHub:WebhookSecret" `
    "replace-locally" `
    --project src/Dip.Api/Dip.Api.csproj

dotnet user-secrets set `
    "GitHub:Token" `
    "replace-locally" `
    --project src/Dip.Api/Dip.Api.csproj
```

環境変数では、階層区切りに`__`を使います。

```text
ConnectionStrings__PostgreSql
Redis__ConnectionString
GitHub__WebhookSecret
GitHub__Token
```

## GitHub Webhook

- Payload URL: `https://<host>/api/v1/webhooks/github`
- Content type: `application/json`
- Secret: `GitHub:WebhookSecret`と同じ値
- Events: `push`、`pull_request`、`check_run`、`workflow_run`、`ping`

本番環境ではHTTPSを必須とし、Webhook SecretとGitHub TokenをSecret Managerで管理します。

## P0 MVP終了範囲

```text
CollectorまたはGitHub Webhook
→ API受付
→ 入力検証
→ PostgreSQL保存
→ Outbox
→ Redis Queue
→ Worker処理
→ Quality Gate判定
→ Dashboard表示
```

P0終了後の拡張対象は、MinIOへのArtifact本格保存、Python Analysis Service、OpenTelemetry、通知チャネル追加、負荷試験、長期運用向けMigration戦略です。これらはP0 MVP終了宣言を妨げる必須項目ではありません。

## ライセンス

本リポジトリはルートの`LICENSE`に従います。現在は明示的な許可がない限り、複製、再配布、改変を認めないプロプライエタリ条件です。
