# DIP API・非同期要求・エラー応答の詳細設計

> **文書ステータス:** 主要Endpoint群はAs-Is / JSON Schema等はレビュー対象  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. API設計の原則
外部APIは**許可されたProject/Resource操作**だけを受け付ける。入力バリデーション、認可、同一Revisionの紐付け、冪等性、失敗の明示はすべてHTTP境界で始める。実際のrouteとDTOは各Controllerが正本。

## 2. API群
| API群 | 既存実装 | 目的と確認すべきもの |
|---|---|---|
| Projects / Dashboards | `ProjectDashboardController`ほか | Project可視範囲、Pagination、鮮度 |
| Collector / Evidence | `CollectorsController`, `EvidenceIngestionController` | 署名/Token、payload検査、重複 |
| QA | `QaController`, `QualityAutomationController` | Command認可、RunId、失敗種別 |
| QA Governance | `QaGovernanceController` | Waiver、policy変更、監査 |
| Artifacts | `ArtifactsController`, `QaArtifactsController` | Project/Artifact所有権、path traversal |
| Claims / Truth | `ProjectClaimsController`, `ClaimEvidenceAssessmentsController` | Actor、Claim Subject、判断理由 |
| Release | `ReleaseReadinessController`, `ReleaseEvidenceController` | Exact SHA、release authority |
| Security / Auth | `AuthController` | Auth token、Role／Policy scope |

## 3. 提案する共通エラー契約
以下は**将来の統一仕様例**。現在のControllerが返すJSONと一致することは保証しない。
```json
{
  "code": "QA_TARGET_NOT_EVALUATED",
  "message": "Required runtime tool is unavailable.",
  "requestId": "<correlation-id>",
  "projectId": "<project-id>",
  "retryable": false,
  "details": { "tool": "unity-editor", "status": "NOT_EVALUATED" }
}
```

| HTTP | 代表例 | 扱い |
|---|---|---|
| 400 | 必須フィールド欠落・値域外 | 再試行で改善しない |
| 401 | 未認証 | 認証必要 |
| 403 | 別Project・操作権限不足 | 外部へ存在確認を漏らさない |
| 404 | 存在しないか不可視のResource | 不必要な対象情報を漏らさない |
| 409 | Revision不整合、重複実行競合 | 冪等キー・最新状態を案内 |
| 413 | Artifact/JSON上限超過 | 保存せず理由提示 |
| 429 | Project/Token別の制限 | Retry-After等を契約化 |
| 5xx | DB/Runner/内部異常 | 未確定・観測不能として扱う |

## 4. 非同期Jobの応答ルール案
長時間QAでは、受理時にJobIdを返し、`GET status`とログ/Artifact参照APIを分離する。202や409などのHTTP表現を採用するかは既存API互換のRFCで決める。JobがQueuedでもToolが成功したとはみなさない。

## 5. 重要な検証
- IDOR: Project AのトークンでProject BのRun/Artifactにアクセスできない。
- JSON bodyのProjectIdとrouteのProjectIdが違えば拒否する。
- Retryで同一Webhook / SourceEventを二重保存しない。
- 長い出力・危険なファイル名・URL・Unicode/制御文字を無害化する。
- 分析サービス不可でもHTTP成功を品質PASSへ誤変換しない。

## 参照する実装・既存資料

- `docs/04-api/api-overview.md`
- `docs/04-api/error-format.md`
- `src/Dip.Api/Authorization/ProjectScopeAuthorization.cs`
- `src/Dip.Api/Controllers/QualityAutomationController.cs`
- `src/Dip.Api/Controllers/ReleaseReadinessController.cs`

## レビュー・受け入れチェック

- [ ] OpenAPI/Controller routeに対する仕様の自動差分を取る
- [ ] 認証なし・権限なし・別Project IDで負例を実施
- [ ] QA Run/Jobの成功とHTTP受付成功を分離した
