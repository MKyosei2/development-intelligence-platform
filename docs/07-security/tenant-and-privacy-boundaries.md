# テナント境界・顧客知財・AI外部送信設計

> **文書ステータス:** To-Be商用要件 / 実装完了の表明ではない  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 顧客データの区分
| 種類 | 秘匿性 | 保存・処理の原則 |
|---|---|---|
| 公開OSS | Public | ライセンスと再配布制限を保持 |
| 未公開ゲームソース | Restricted | 顧客同意・目的限定、暗号化、アクセス監査 |
| アート・映像・音声 | Restricted | IP/著作権と外部サービス利用制限を考慮 |
| QAログ/クラッシュダンプ | Confidential | Secret/PII混入を前提に扱う |
| AI prompt/response | Confidential | 外部送信先・保存範囲・保持期間を別承認 |
| Runner資格情報 | Secret | Vault/短寿命Token、ログに出さない |

## 2. 候補のデータ所有階層
`Tenant -> Organization -> Team -> Project -> Revision -> Evidence / Artifact / Job`。この階層自体は将来製品案。現行の`OrganizationTenant`や`ProjectMembership`があることと、商用レベルのテナント分離完了を同一視しない。

## 3. 脅威と拒否ケース
- Project Aの共有URL・Artifact IDをProject Bから閲覧できる。
- DB検索・エクスポート・キャッシュ・ログ・バックアップ経由でテナント横断データが混入する。
- AIの外部API送信前に顧客同意と送信対象の秘匿区分を見落とす。
- 利用停止後もAgent/Runnerが長期Tokenでアクセスし続ける。
- 削除申請時にArtifactの派生画像、Prompt、モデルログ、オフサイトバックアップが残る。

## 4. アーキテクチャ選択肢
1. **顧客別専用インスタンス:** 分離が明確だが運用費用が大きい。
2. **共有サービス＋論理分離:** 安価だが全クエリ、Queue、Artifactに境界を要求する。
3. **機密度別ハイブリッド:** GPU RunnerやRestricted Assetだけ別境界に置く。

採否は脅威モデル、負荷試験、費用、データ所在地、顧客の権利条件に基づきADRで記録する。

## 5. 必須設計資料
データフロー図、処理目的と根拠、プロセッサー契約、秘密管理、権限表、Retentions/Deletion Matrix、Incident通知、AuditとトレースのPII制限、AIモデル供給者ごとの送信条件。

## 6. 検証
他テナントのtokenとresource idの組み合わせでAPI/worker/Artifactへアクセスを試みる。ログやエラー応答に情報が漏れないことを確認。消去テストはバックアップ保持の例外を明示する。

## 参照する実装・既存資料

- `docs/10-future/architecture/ARCH-002-MULTI-TENANCY.md`
- `src/Dip.Domain/Features/Identity/Entities/OrganizationTenant.cs`
- `src/Dip.Api/Authorization/ProjectScopeAuthorization.cs`
- `src/Dip.Infrastructure/P2Intelligence/OpenAiCompatibleFixProposalAgent.cs`
