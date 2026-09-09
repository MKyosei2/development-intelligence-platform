# 正式なRuntime検証

RevisionのFormal Evidenceは、target Windows machine上でfail-closed verificationを完了した場合にのみ生成します。**正式経路はNative-firstであり、Dockerの存在をPASS条件にしません。**

## Formal verificationの前提

- Windows
- .NET 8 SDK
- Git
- PostgreSQL 16-compatible server
- PostgreSQL client tools (`psql`, `pg_dump`, `pg_restore`)
- Python 3.12+
- Node.js / npm（Browser E2E）
- 実Unity Editor（Unity runtime verificationを含む場合）

PostgreSQLは到達可能なnative serverを使用します。検証用databaseは専用名で作成し、既存のapplication databaseを再利用・破壊しません。

## Formal runnerが確認するもの

`Verify-PortfolioFormal.ps1`はclean Git working treeを要求し、同じsource commitに対して次をfail-closedで確認します。

1. Solution restore / Release build
2. Domain / Application / Infrastructure tests
3. Native PostgreSQL Integration / live E2E
4. Universal QA / QA Governance
5. Native GUI runtime + Playwright
6. Database migration sequence / idempotence
7. Backup / restore / disaster recovery
8. Governance load / concurrency
9. 実Unity Collector runtime
10. Project Control / P2 checks
11. Portfolio benchmark
12. benchmark artifactの`sourceCommit`が検証対象SHAと一致すること

途中の必須phaseが失敗した場合、Formal PASS artifactを生成しません。

## GUI verification

`Verify-GuiPhase1.ps1`は独自Docker Compose stackを構築しません。既存static validationを実行した後、GitHub GUI E2Eでも使用している`Verify-GuiRuntime.ps1`へdelegationし、Native PostgreSQL / Python Analysis / Dip.Api / Dip.Worker / Dip.Web / Playwrightを検証します。

これにより「GitHub CIではNative、formal localだけDocker」というruntime差異をなくします。

## Database / DR

Migration、backup、restore、DRはPostgreSQL client toolsを直接使用します。Formal verificationではnative client pathを使用するため、Docker fallbackの有無はverification PASSの根拠になりません。

Backup Evidenceにはsource commit、database / artifact hash、offsite mirrorを使用した場合はmirror位置を記録します。Restore前にhashを確認し、Productionではdeployment rootとは異なるoffsite storageを要求します。

## Exact-revision rule

Historical P0 / Unity / benchmark Evidenceを新revisionへ昇格しません。Formal artifact、GUI artifact、benchmark artifactはそれぞれsource commitを記録し、公開対象SHAと一致する場合だけcurrent revisionのEvidenceとして扱います。

GitHub Actionsの未実行、cancelled、runner不足、外部service障害はProduct PASSへ読み替えません。
