# コントリビューションガイド

Development Intelligence PlatformへのContributionありがとうございます。

## 開発環境

サポート対象のNative-first Runtimeに必要なもの:

- .NET SDK 8.0;
- PostgreSQL 16+（または互換性のあるサポート対象PostgreSQL）と `psql`, `pg_isready`, `pg_dump`, `pg_restore`;
- Python 3.12+;
- Git;
- Visual Studio 2022、Visual Studio Code、またはJetBrains Rider。

Node.jsはBrowser E2Eを実行する場合に必要です。Unity / Unreal / CMake / Native Analyzerは対応するIntegration / QA Pathでのみ必要です。Dockerと外部Queue Daemonは必須ではありません。

Local `dip` PostgreSQL Database / Userを作成した後、次のCommandでStack全体を起動します。

```powershell
.\scripts\Start-DIP.ps1
```

必要に応じてRestore、Build、Testを直接実行します。

```bash
dotnet restore DevelopmentIntelligencePlatform.sln
dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore
dotnet test DevelopmentIntelligencePlatform.sln -c Release --no-build
```

## ブランチ

最新のDefault Branchから、目的を絞ったBranchを作成してください。推奨Name:

```text
feature/short-description
fix/short-description
docs/short-description
test/short-description
```

無関係なRefactoring、Formatting、Behavior Change、Dependency Updateを1つのPull Requestへ混在させないでください。

## コーディング規約

- Dependency Direction `Domain <- Application <- Infrastructure <- Api / Worker` を維持する。
- Shared Transport Contractは `Dip.Shared` に置く。
- Persisted TimestampにはUTCを使用する。
- Asynchronous Operationへ `CancellationToken` を引き継ぐ。
- Asynchronous Request Pathで `.Result` / `.Wait()` を避ける。
- External InputはBoundaryでValidateする。
- SecretとProduction DataをSource Control / Logへ残さない。
- Behaviorを変更した場合はTestを追加 / 更新する。
- Breaking Changeを明示的にDocumentしない限り、API / JSON Compatibilityを維持する。
- Implemented、Tested、Runtime VerifiedのClaimを分離する。
- PostgreSQLをDurable Coordination Boundaryとする。明示的なArchitecture Decisionなしに2つ目の必須Queue / Cache Daemonを導入しない。
- Native QA Process ExecutionではWorkspace、Environment、Resource、Isolation Boundaryを維持する。

## コミット件名

Conventional Commit形式のSubjectを使用します。例:

```text
feat(qa): add runtime defect evidence
fix(ci): restore native integration startup
perf(metrics): reduce million-row query latency
refactor(web): split management cockpit components
docs(portfolio): explain exact-revision evidence
```

Subjectは命令形・72文字以内にします。必要ならTemplateを設定できます。

```bash
git config commit.template .gitmessage
```

## プルリクエスト

Pull RequestにはPurpose / Scope、Implementation、Test、Migration / Configuration Impact、必要に応じたScreenshot、Known Limitation、Measured / Runtime Claimに対するExact-Revision Evidenceを記載してください。

Review前に該当するCheckを実行します。

```bash
dotnet format --verify-no-changes
dotnet build DevelopmentIntelligencePlatform.sln -c Release
dotnet test DevelopmentIntelligencePlatform.sln -c Release
```

Runtime Changeでは次も実行します。

```powershell
.\scripts\Verify-GuiRuntime.ps1 -RunPlaywright
.\scripts\Verify-DatabaseMigrations.ps1
```

必要なCommandを実行できない場合は理由を記載してください。Infrastructure-unavailableはPASSではありません。

## Database変更

Persistence変更ではEF Configuration / Migration Update、Rollback Consideration、Index / Uniqueness Review、可能な場合はIntegration Coverageが必要です。すでにProductionへ適用済みのMigrationは編集しないでください。

## セキュリティ

`SECURITY.md` に従ってください。Vulnerability、Token、Secret、Private PayloadをPublic Issue / Pull Requestへ載せないでください。
