# SaaS運用費・利用上限・課金連動の仕様

> **文書ステータス:** Proposed / 価格・コスト実績なし  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. コストを分解する
`月額費用 = API/DB等の固定費 + CPU/GPU Runner時間 + Artifact保管・転送 + LLMトークン + 監視/Backup + サポート`。固定値は環境・契約で変わるため、本書では実績料金を示さない。

## 2. 計測・集計の候補
| Component | 単位 | 入力が必要な理由 |
|---|---|---|
| API/Web | Request・稼働時間 | 最低限のサービス費用 |
| DB | Storage GB・IOPS | Evidenceの伸長 |
| Artifact | Storage GB-days・Egress | Capture/Dumpの大容量 |
| QA CPU | vCPU-min・RAM GB-min | Build/Static/Fuzz |
| QA GPU | GPU-min・VRAM class | Render/Player・Capture |
| LLM | Token in/out・API回数 | 仮説・パッチ候補 |
| Customer support | 問い合わせ時間 | Enterprise対応 |

## 3. 予算制御
- Project/TenantごとのMonthly Budget、JobあたりMaxCost、GPU同時数、MaxToolDuration、RetryLimit。
- 上限超過は**事前の予約・実行停止・理由記録**で表現し、結果をProduct QA Failureと混同しない。
- EstimationとActualを分け、請求額は検証済み使用量だけから算出。
- AI効果は`cost per verified fix`で評価。単に生成リクエスト数を増やすことを価値としない。

## 4. Abuse Prevention
無限Job、異常に巨大なArtifact、大量のAPI呼び出し、GPU占有を制限。内部Worker再試行が顧客の請求重複を生まないよう、JobId/Attemptで冪等課金する。

## 5. リリース前の検証
計測漏れ、時間超過、Job cancelled、Sandbox failed、LLM 429/5xx、Artifact部分保存を含む課金負例を試験し、料金を事前提示できないケースを不正な「無料」と表示しない。

## 参照する実装・既存資料

- `docs/10-future/requirements/PRD-002-ENTERPRISE-AND-CREATOR.md`
- `docs/10-future/architecture/PROD-002-capacity-scheduling-and-fairness.md`
