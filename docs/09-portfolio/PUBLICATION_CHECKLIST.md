# Portfolio公開Checklist

公開は段階的に行う: **Safetyを証明 → exact revisionを証明 → visibility変更 → reviewer-facing metadata完成 → release**。High-severity secret findingが未解決ならRepositoryはprivateのままにする。

## 安全性

```powershell
python tools/verify_publication_safety.py --root . --output artifacts/publication-safety/publication-safety.json
python tools/validate_portfolio_readiness.py --root . --output artifacts/portfolio-readiness/static.json
```

GitHub `Security Scan` workflowも必須。Historical outputはcurrent-SHA runの代替ではない。

## Exact-revision CI Gate（対象Revision固定のCI Gate）

Publication SHAについて次のreal execution成功を要求する。

- `Commit Message`
- `.NET CI`
- `Python CI`
- `Native Collector CI`
- `Integration and E2E Tests`
- `GUI E2E`
- `Native Package Build`
- `Security Scan`
- `Universal QA Benchmark`
- `Unreal Collector CI`
- `Portfolio Publication Readiness`

Runner/account/platform availabilityによりstep実行前に停止したjobはinfrastructure-blockedであり、source failureでもPASSでもない。

Native Package Buildはexact-SHA archiveとSHA-256 manifestを生成する必要がある。Docker image statusはsupported publication gateに含めない。

## Runtime Evidence（実行証拠）

実行:

```powershell
.\scripts\Verify-GuiRuntime.ps1 -RunPlaywright
.\scripts\Run-PortfolioBenchmarks.ps1
.\scripts\Verify-DatabaseMigrations.ps1
.\scripts\Verify-DisasterRecovery.ps1
```

Manifest/sourceCommitがpublication SHAと一致するEvidenceだけがcurrent-revision claimを支えられる。

## 公開状態 / Reviewer確認

Gate通過後、既存portfolio publication scriptからrepository visibility/release stateを変更する。Signed-out状態でrepositoryを確認し、README、architecture、implementation-vs-verification boundary、historical/current Evidence区別、source/test、release artifactへ到達できることを確認する。

Metadata topicには適用可能な範囲で`developer-tools`、`game-development`、`qa-automation`、`dotnet`、`blazor`、`postgresql`、`devops`、`unity`、`unreal-engine`を含める。PostgreSQL-backed queueingはcurrent runtime architectureの一部として説明する。
