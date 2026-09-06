# QA Governance 強化（2026-08-14）

## 目的

QA Governanceは、権限を伴う制御プレーンとして扱う。品質結果だけでは不十分であり、どのプロジェクトを誰が操作できるか、例外を誰が承認したか、同一Evidenceが一度だけ処理されたか、復旧やリリース判断を再現できるかまで証明できる必要がある。

## プロジェクト単位の認可

`ProjectMembership`を認可のSource of Truthとする。ロールは`Viewer < Member < Maintainer < Owner`の順で権限が強くなり、グローバル`Admin`は復旧・運用者向けロールとする。新規ProjectはOwner membershipと同一トランザクションで作成する。

`ProjectScopeAuthorizationFilter`はmodel binding後にroute/bodyの`ProjectId`を評価する。間接識別子は`RequireProjectResourceAttribute`で保護し、QA run、pipeline run、incident、durable job、benchmark run、replay、artifactの各IDはController action実行前に所有Projectへ解決する。これにより、攻撃者が指定したProject GUIDだけを認可根拠にすることを防ぐ。

Productionは、認証、64文字以上のJWT key、GitHub OAuth資格情報、少なくとも1つの復旧用Admin login、PostgreSQL、QA sandboxが構成されていなければfail-closedとする。DevelopmentとTestingではローカル認証を無効にした状態でも利用できる。

### 既存Projectのmembership初期化

認可migrationは、`ProjectMembership`導入前から存在するProjectのOwnerを推測しない。構成済みのglobal Adminを復旧主体とし、Admin bearer tokenを使って`scripts/Bootstrap-ProjectMemberships.ps1`から対象Projectへ明示的にOwner membershipを付与する（または`-AllProjects`を使用する）。すべての付与は通常のmembership APIを経由するため監査対象になる。これにより、暗黙のロックアウトと危険な自動権限付与の両方を避ける。

例:

```powershell
.\scripts\Bootstrap-ProjectMemberships.ps1 `
  -BaseUrl http://localhost:8080 `
  -AdminBearerToken $env:DIP_ADMIN_TOKEN `
  -SubjectId "github:123456" `
  -Login "owner-login" `
  -AllProjects
```

## サーバー側で決定するActorと監査

Governance mutation用HTTP request DTOでは`Actor`や`ApprovedBy`を受け取らない。Actor identityはJWTの`sub`と`name` claimから決定する。Audit entryにはrequest/correlation ID、理由、subject、変更前後のJSONを保持する。

主なmutationには、finding state遷移、membership変更、waiver判定、policy lifecycle変更、release override、re-baseline、ownership/test-impact/retention rule作成、backup verification evidenceが含まれる。

## 冪等性と並行実行

完了したQA runは`qa_governance_processed_runs.qa_run_id UNIQUE`で保護する。処理にはPostgreSQLのtransaction-scoped advisory lockと`SERIALIZABLE` transactionを使用し、同じrun IDに異なるinput hashが渡された場合は拒否する。非リレーショナルのテストではkeyed in-process lockを使用する。

変更可能なGovernance aggregateはoptimistic version tokenを持つ。複数rowにまたがる不変条件（membershipのOwner保持、policy activation）はProject単位のtransaction/advisory lockで保護する。さらにDB側で、生成されたactive slotへのdeferrable unique constraintにより、ProjectごとにActive QA policyを最大1件に制限する。

## WaiverとPolicyのライフサイクル

Waiverは`Requested -> Active / Rejected -> Revoked / Expired`で遷移する。申請者は自分自身のwaiverを承認できない。承認済みwaiverは不変とし、変更する場合は新しい申請を作成する。

Policyは`Draft -> Approved -> Active -> Superseded / Archived`で遷移する。Policy更新はin-place編集ではなく不変な新versionとして作成する。ActivationはOwner操作であり、同じ直列化されたProject mutation内で以前のActive policyをsupersedeする。

## Release Gate Override（Release Gateの例外）

Release overrideは全体を無条件に迂回する仕組みではない。最新のnon-GO policy evaluationと正確なcommit SHAに紐付き、有効期限を持ち、申請者とは異なるOwner承認者を必要とし、reject/revokeにも対応する。Release Readinessは、評価対象とcommitが候補releaseと一致する、承認済みかつ期限内のoverrideだけを認める。

## Observability（可観測性）

PrometheusはQA run/gate total、open finding、flaky test、active waiver、self-health、governance mutation total、sandbox denialを出力する。`DIP QA Governance`を専用Grafana dashboardとして用意する。Prometheus ruleはQA self-health低下、sandbox denial、新規NO-GO gateを通知する。

## Backup・Restore・Disaster Recovery（災害復旧）

- `scripts/Backup-Dip.ps1`: PostgreSQL custom-format dumpを作成し、Docker Local artifact volumeをarchiveし、両方をhash化してmanifestを出力する。
- `scripts/Restore-Dip.ps1`: `-ConfirmRestore`を必須とする破壊的restore。hashを検証し、API/Worker/Webを停止してPostgreSQLとartifactをrestoreし、その後serviceを再起動する。
- `scripts/Verify-DisasterRecovery.ps1`: databaseとartifact Evidenceがbackup/restoreを越えて保持されることを隔離環境で検証する。

この文書で説明する運用backup/restoreはDocker Composeの`Local` artifact providerを対象としている。MinIO/S3運用ではdatabase backupに加えてbucket versioning/replicationまたはprovider固有backupを使用する。

## Formal Evidence（正式な証拠）

`Verify-PortfolioFormal.ps1`はfail-closedで動作する。WindowsではcleanなGit revision、.NET、Docker、実際のUnity Editorを要求し、solution tests、Universal QA、Governance tests、migration fresh/rollback/re-upgrade、DR verification、Governance load/concurrency、Unity verification、Project Control、P2、portfolio benchmarkを実行する。PASS artifactには正確なsource commitを記録する。
