# Development Intelligence Platform

Development Intelligence Platform (DIP) は、GitHub、CI/CD、ゲームエンジン、DCCツール、テスト、ビルド、性能計測などに分散した情報を統合し、PM・プロデューサー・ディレクター・テックリードの意思決定を支援するWebプラットフォームです。

## Browser-first

DIPの利用者は**Webブラウザだけ**で利用します。利用者PCへのアプリのインストール、`.cmd` / PowerShellの実行、Docker Desktop、.NET SDK、Pythonの導入は不要です。

```text
Browser
  -> HTTPS
  -> Dip.Web (Blazor Server)
  -> Dip.Api
  -> PostgreSQL / Redis / Python Analysis / Worker
```

公開環境ではCaddyをHTTPS入口にし、ブラウザからは1つのURLだけを使用します。API、PostgreSQL、Redis、Python Analysis、Workerはサーバー内部ネットワークで動作します。

利用者:

```text
https://<DIP_PUBLIC_HOST>/
```

サーバーへの導入方法は [`docs/WEB_DEPLOYMENT.md`](docs/WEB_DEPLOYMENT.md) を参照してください。

## 主なGUI導線

- GitHub接続 / Repository Picker
- DIP Project Import
- Portfolio / Active Project
- Project Overview
- Repository
- CI
- QA
- Project Intelligence
- QA Governance
- System Status / Settings

GitHub OAuth（ユーザー認証）とGitHub App（Repository権限）は分離しています。GitHub Appの秘密鍵、OAuth secret、DIP JWT signing keyはすべてサーバー側に保持し、ブラウザへ配布しません。

## Hosted Web deployment

管理者はLinuxサーバー/VM上でDocker Engine + Docker Compose v2を使用します。エンドユーザーはこの操作を行いません。

```bash
cp deploy/web.env.example deploy/web.env
# deploy/web.env の CHANGE_ME を安全な値へ変更

docker compose --env-file deploy/web.env -f docker-compose.web.yml up -d --build
```

公開するのは80/443だけです。CaddyがHTTPS証明書を取得・更新し、`/api/*` と `/hubs/*` をAPIへ、それ以外をBlazor Webへ転送します。

GitHub OAuth App:

```text
Homepage URL:              https://<DIP_PUBLIC_HOST>/
Authorization callback:   https://<DIP_PUBLIC_HOST>/api/v1/auth/github/callback
```

詳細: [`docs/WEB_DEPLOYMENT.md`](docs/WEB_DEPLOYMENT.md)

## Project Truth / Evidence First

DIPの中心は単なるQAダッシュボードではありません。Git、CI、Build、Test、Runtime、Performance、Unity、Maya、QAなどの客観Evidenceを集約し、人間の報告（Claim）と分離して管理します。

主な状態:

- `Confirmed`: Evidenceが報告を支持
- `PartiallyConfirmed`: 一部のみ確認可能
- `Unverified`: Evidence不足
- `Contradicted`: 現在のEvidenceと不一致
- `Stale`: Evidenceが古い

PM向け画面では、Project Truth、Evidence-backed Progress、Risk、Blocker、Report vs Reality、Evidence Timelineを優先し、技術ログは詳細Evidenceとして下位に配置します。

## Architecture

| Project | Role |
|---|---|
| `Dip.Domain` | Entity / Value Object / Evidence / Claim / Truth |
| `Dip.Shared` | API・Worker・Collector間Contract |
| `Dip.Application` | Use case / Project Truth / Quality rules |
| `Dip.Infrastructure` | PostgreSQL / Redis / GitHub integration / persistence |
| `Dip.Api` | HTTP API / OAuth / Webhook / SignalR |
| `Dip.Worker` | Outbox / Quality Gate / retry / background jobs |
| `Dip.Web` | PM-first browser UI (Blazor Interactive Server) |
| `services/python-analysis` | FastAPI analysis service |
| `collectors/native-collector` | Native evidence collector |

## GitHub repository onboarding

GUIの標準導線は次です。

```text
GitHub sign-in
  -> Repository Picker
  -> DIP Project import
  -> Project Overview
  -> Repository / CI / QA / Intelligence
```

GitHub Appを使う場合は、サーバーの `deploy/web.env` に以下を設定します。

```text
DIP_GITHUB_APP_ID
DIP_GITHUB_APP_INSTALLATION_ID
DIP_GITHUB_APP_SLUG
DIP_GITHUB_APP_PRIVATE_KEY_BASE64
```

これらを利用者PCへ設定する必要はありません。

## Web security

`docker-compose.web.yml` はProductionモードで認証を必須化し、開発用の既定パスワードを許可しません。公開Web構成では以下を前提にしています。

- HTTPS必須
- GitHub OAuth必須
- 64文字以上のJWT signing key
- 強いPostgreSQL password / webhook secret
- API/DB/Redis/Analysis/Workerを外部へ直接公開しない
- ASP.NET Data Protection keyの永続化
- HttpOnly web session cookie
- Bearer tokenをbrowser localStorageへ保存しない

## Developer / CI

ソース開発・検証では従来どおり `.NET 8`、Docker Compose、Python、必要に応じてC++/Unity等を使用できます。ただし、これは**開発者向け**であり、DIP利用者の起動手順ではありません。

ローカル開発用Compose:

```bash
docker compose up -d --build
```

代表的な検証:

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-Solution.ps1"
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-GuiPhase1.ps1"
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-QaGovernance.ps1"
```

PowerShell検証はWindows固有Evidenceや開発時のFormal Verificationのために残しており、Web利用者には不要です。

## Documentation

- Hosted browser deployment: [`docs/WEB_DEPLOYMENT.md`](docs/WEB_DEPLOYMENT.md)
- GUI runtime architecture: [`docs/GUI_RUNTIME_20260815.md`](docs/GUI_RUNTIME_20260815.md)
- Universal QA: [`docs/02-architecture/universal-qa.md`](docs/02-architecture/universal-qa.md)
- Project Truth P0: [`docs/02-architecture/project-truth-p0.md`](docs/02-architecture/project-truth-p0.md)
- P1 Technical Evidence: [`docs/02-architecture/p1-technical-evidence.md`](docs/02-architecture/p1-technical-evidence.md)
- P2 Decision Intelligence: [`docs/02-architecture/p2-decision-intelligence.md`](docs/02-architecture/p2-decision-intelligence.md)
- Runtime verification: [`docs/09-portfolio/runtime-verification.md`](docs/09-portfolio/runtime-verification.md)

## GitHub Webhook

```text
https://<DIP_PUBLIC_HOST>/api/v1/webhooks/github
```

PayloadはHMAC SHA-256署名検証とDelivery IDによる冪等性処理を行います。

## License

This repository is licensed according to [`LICENSE`](LICENSE).
