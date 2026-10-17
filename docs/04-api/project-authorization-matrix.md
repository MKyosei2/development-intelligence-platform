# 認証・認可・Project/Resource操作権限表

> **文書ステータス:** As-Isの認可機構確認 / 行列は要承認  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 現状の認可機構
`ProjectScopeAuthorizationFilter`は`RequireProjectRoleAttribute`、`RequireProjectResourceAttribute`に関連するProject権限を処理する。認証ユーザーの`sub`からActorを解決し、public readonlyはGET/HEADで厳格に制限する経路がある。詳細なPolicyは各Controller・サービスを正とする。

## 2. 提案するRole Action Matrix（規範案）
**これは現行権限の一覧でなく、監査時に完成させるべき行列テンプレート。**
| リソース/操作 | Viewer | Member/QA | TechLead | ProjectManager | PlatformAdmin |
|---|---|---|---|---|---|
| Projectの可視データ参照 | 所属Projectのみ | 同左 | 同左 | 同左 | 委譲範囲のみ |
| QA実行要求 | 不可 | 承認済み対象のみ | 可 | ポリシー依存 | 緊急管理時のみ |
| Artifactダウンロード | 掲載許可された物のみ | 所属・秘密分類に依存 | 同左 | 同左 | 監査付き |
| Claim登録 | 不可または限定 | 担当タスクのみ | 可 | 可 | 原則業務外 |
| Release Go/NoGo決定 | 不可 | 不可 | 別途承認要件 | 担当権限 | 管理者だから自動承認とはしない |
| QA Waiver/Policy変更 | 不可 | 不可 | 役割分離付き承認 | 役割分離付き承認 | 管理権限に応じて |
| テナント全体の設定 | 不可 | 不可 | 不可 | 原則不可 | 明示的かつ監査対象 |

## 3. 防ぐべき認可の抜け道
- URLで他Projectの`artifactId`を直接指定する。
- 検索結果・CSV export・ページングで違うProjectのデータを返す。
- APIがResourceのProjectを取得しないまま、routeのProjectIdのみで認可する。
- Share URLやキャッシュのキーがTenantに紐付かない。
- Webhook/AgentのService TokenをUIユーザーの権限で扱う。
- archived/deleted Projectへの更新を見落とす。

## 4. Review Checklist
1. 全Controller ActionのRole Attributeと実行サービスの権限制約を表に埋める。
2. 正常ケース（所属Member）と拒否ケース（他Project、匿名、Viewer、無効Token）を自動化。
3. Audit Logに実際のActor、Project、Resource、Action、拒否理由が残ることを確認。
4. 権限変更直後に旧トークン／既存キャッシュで越境しない。

## 5. Production公開の条件
組織・チーム・プロジェクト単位の権限がすべてのデータ取得と変更経路に適用されるまで、`Multi-tenant ready`を宣言しない。

## 参照する実装・既存資料

- `docs/07-security/access-control.md`
- `src/Dip.Api/Authorization/ProjectScopeAuthorization.cs`
- `src/Dip.Api/Authorization/ProductionSecurityGuard.cs`
- `tests/Dip.IntegrationTests/GovernanceAuthorizationSurfaceTests.cs`
- `tests/Dip.IntegrationTests/ManagementOperationsAuthorizationSurfaceTests.cs`
