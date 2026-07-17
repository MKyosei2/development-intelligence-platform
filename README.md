# Development Intelligence Platform

Development Intelligence Platform（DIP）は、GitHub、CI/CD、ゲームエンジン、DCCツール、テスト、ビルド、性能計測などに分散した情報を統合し、PM・プロデューサー・テックリードの意思決定を支援する基盤です。

## 現在の構成

| プロジェクト | 役割 |
|---|---|
| `Dip.Domain` | Entity、Value Object、状態遷移、ドメインルール |
| `Dip.Shared` | API・Worker・Collector間の共通Contract |
| `Dip.Application` | ユースケース、検証、Repository／外部サービスInterface |
| `Dip.Infrastructure` | PostgreSQL、GitHub API、Queue、外部サービス実装 |
| `Dip.Api` | HTTP API、Webhook、Collector Report受付 |
| `Dip.Worker` | 非同期処理、品質評価、再試行 |
| `Dip.Web` | PM・テックリード向けDashboard |
| `Dip.Domain.Tests` | Domain層のUnit Test |

## アーキテクチャ

```text
GitHub / CI / Unity / Maya / Unreal / Generic JSON
                         |
                         v
                  ASP.NET Core API
                         |
                 Redis Job Queue
                         |
          +--------------+--------------+
          |              |              |
          v              v              v
   GitHub Worker   Quality Worker   Analysis Worker
          |              |              |
          +--------------+--------------+
                         |
                         v
                    PostgreSQL
                         |
                    MinIO Storage
                         |
                         v
                 Blazor Dashboard
```

依存方向は `Domain <- Application <- Infrastructure <- Api / Worker` を基準にします。`Dip.Shared`はAPI、Web、Worker、Collector間のContractを提供します。

## 必要環境

- .NET SDK 8.0
- Docker DesktopまたはDocker Engine + Compose V2
- Visual Studio 2022 17.x、Visual Studio Code、またはJetBrains Rider
- Git

## セットアップ

```bash
git clone <repository-url>
cd development-intelligence-platform
docker compose up -d
dotnet restore DevelopmentIntelligencePlatform.sln
dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
dotnet test DevelopmentIntelligencePlatform.sln -c Release --no-build
```

Docker Composeは次を起動します。

| サービス | ポート | 用途 |
|---|---:|---|
| PostgreSQL | `5432` | 永続データ |
| Redis | `6379` | Queue・Cache |
| MinIO API | `9000` | Artifact保存 |
| MinIO Console | `9001` | MinIO管理画面 |

状態確認と停止：

```bash
docker compose ps
docker compose logs -f
docker compose down
```

データも削除する場合だけ `docker compose down -v` を使用してください。

## アプリケーション起動

```bash
dotnet run --project src/Dip.Api/Dip.Api.csproj
dotnet run --project src/Dip.Worker/Dip.Worker.csproj
dotnet run --project src/Dip.Web/Dip.Web.csproj
```

API、Worker、Webは別ターミナルで起動します。

## ローカル設定

TokenやSecretをGit管理対象へ直接書かず、User Secretsまたは環境変数を使用します。

```bash
dotnet user-secrets init --project src/Dip.Api/Dip.Api.csproj
dotnet user-secrets set "ConnectionStrings:PostgreSql" "Host=localhost;Port=5432;Database=dip;Username=dip;Password=dip-local-password" --project src/Dip.Api/Dip.Api.csproj
dotnet user-secrets set "GitHub:WebhookSecret" "replace-locally" --project src/Dip.Api/Dip.Api.csproj
dotnet user-secrets set "GitHub:Token" "replace-locally" --project src/Dip.Api/Dip.Api.csproj
```

環境変数では、階層区切りに `__` を使います。

```text
ConnectionStrings__PostgreSql
GitHub__WebhookSecret
GitHub__Token
```

Docker用の値はルートの `.env` で上書きできます。`.env`はコミットしないでください。

```dotenv
POSTGRES_DB=dip
POSTGRES_USER=dip
POSTGRES_PASSWORD=change-me
POSTGRES_PORT=5432
REDIS_PORT=6379
MINIO_ROOT_USER=dipadmin
MINIO_ROOT_PASSWORD=change-me
MINIO_API_PORT=9000
MINIO_CONSOLE_PORT=9001
```

## GitHub Webhook

- Payload URL: `https://<host>/api/webhooks/github`
- Content type: `application/json`
- Secret: `GitHub:WebhookSecret`と同じ値
- Events: 実装済みイベントのみ

本番環境ではHTTPSを必須とし、Webhook SecretとGitHub TokenをSecret Managerで管理します。

## 品質方針

- Domainの不変条件をUnit Testで保証する
- WebhookをDelivery IDで冪等化する
- Queue投入とDB更新はOutbox Patternで整合性を保証する
- 外部APIへTimeout、Retry、Circuit Breakerを設定する
- Secret、Token、機密性のあるWebhook本文を本番ログへ出さない
- 日時はUTCで保存する

## 今後の優先実装

1. Solution全体のビルドとテスト安定化
2. PostgreSQL MigrationとEntity Configuration
3. Redis QueueとWorkerの接続
4. GitHub Webhookの冪等性とOutbox Pattern
5. 根拠付きQuality Gate判定
6. Collector Reportの保存・評価・表示
7. Blazor Dashboard
8. Python Analysis Service
9. OpenTelemetry、監視、セキュリティ
10. Integration／E2E／負荷テスト

## MVP完成条件

```text
CollectorまたはGitHub Webhook
→ API受付
→ 入力検証
→ PostgreSQL保存
→ Redis Queue
→ Worker処理
→ Quality Gate判定
→ Dashboard表示
```

重複送信、外部サービス停止、Queue障害、不正入力のテストが通ることも条件とします。

## ライセンス

公開利用を許可する場合は、ルートへ適切な `LICENSE` を追加してください。
