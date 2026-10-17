# データ保持・削除・知的財産保護仕様

> **文書ステータス:** Proposed / 法務・契約確認が必要  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 対象
DIPは未公開ソース・画像・音声・ログ・Prompt・クラッシュDumpを扱う可能性がある。**顧客の素材を保存する権利があるとは自動的に言えない。**

## 2. データごとのライフサイクル案
| Data | 収集 | 利用 | 保存 | 削除・例外 |
|---|---|---|---|---|
| Git snapshot | 許諾/権限を確認 | QA検査のみ | Workspaceを使い捨て | Job終了後にwipeを検証 |
| QA Log | Secret/PIIをSanitize | Failure診断 | 期限付き、暗号化 | 法的保全時の例外 |
| Screenshots/GPU Capture | IP許諾を確認 | 差分/性能証拠 | Artifact ACL＋digest | 派生画像含め削除 |
| Evidence metadata | 必須箇所だけ | Trace/監査 | 契約期間 | 匿名化・参照整合対応 |
| LLM prompt/response | 個別同意＋転送先の承諾 | 推論と検証 | 最小限 | 外部モデル提供者の保持条件 |
| Backup | 災害復旧 | Restore限定 | 世代管理 | 削除期限との整合 |

## 3. 顧客側の操作
Export、Retention Policy変更、Deletion Request、Artifact共有リンクの失効、Tenant Closureなどの機能要件を定義する。実装前は手作業で対応可能な範囲と対応不可な範囲を明記する。

## 4. 安全な削除の条件
DBレコード、Object Store、Search Index、Queue payload、temporary workspace、キャッシュ、外部APIログを棚卸しし、消去完了のReceiptを発行する。バックアップ内の即時個別削除ができない場合、アクセス禁止と保存期限の制限を契約で説明する。

## 5. 知的財産・AI利用
未公開ゲームの素材を第三者LLMへ送る前に、契約・モデルプロバイダーの学習利用と保持設定を確認する。権利者未確認のゲームアセットはデモ用の公開リポジトリへ含めない。

## 参照する実装・既存資料

- `docs/10-future/architecture/ARCH-002-MULTI-TENANCY.md`
- `docs/07-security/tenant-and-privacy-boundaries.md`
