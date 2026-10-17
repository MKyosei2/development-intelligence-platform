# 可観測性・アラート・QA診断カタログ

> **文書ステータス:** 実装構成の観測ポイントと提案閾値の設計  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 既存の監視資産
`infrastructure/monitoring/`にはPrometheus、Grafanaダッシュボード、OpenTelemetry Collector設定がある。本書はその用途を整理するもので、**導入済みファイルが本番で24時間動いている証拠ではない**。

## 2. 監視体系
| Subsystem | 指標・ログ | なぜ監視するか | 異常時の第一対応 |
|---|---|---|---|
| API | availability、p95/p99、401/403/5xx | 使用不能・権限異常 | 直近Deploy/DB確認 |
| Postgres | connections、locks、storage、replication | 進捗・Evidence欠損予防 | Disk/接続原因調査 |
| Worker | queue length、oldest queued、lease expiry | ジョブ詰まりを検出 | Kill/再試行/Owner確認 |
| QA Runner | exit codes、timeout、attestation errors | 非信頼コード隔離失敗 | 隔離不能なら実行拒否 |
| Evidence | hash mismatch、old SHA、NOT_EVALUATED割合 | 誤ったQuality Gateを防ぐ | Ingestion/Integrity調査 |
| LLM | error、latency、tokens、unsafe proposal | 品質と予算を守る | Rate limit/Model切替検討 |
| GPU QA | sampleCount、driver、frame p95/p99 | 誤計測・性能退行 | Telemetryと設定検証 |
| Auth | failure、denied project access | 侵入・越境兆候 | Token/Actor確認 |

## 3. SLI/SLO設計
- **API Availability:** `valid_successful_requests / eligible_requests`。利用者の4xxを数えるかを定義。
- **Job Completion:** `jobs_completed_within_deadline / accepted_eligible_jobs`。キャンセル・無効入力は別扱い。
- **Evidence Integrity:** `verified_artifacts / required_artifacts`。レコード保存だけでは成功扱いしない。
- **Restore Ability:** 決められた時間内にテスト復元で整合した割合。

閾値はTBD。実測していない99.99%や5分RTOを約束しない。

## 4. Alertルールの提案
`SEV1`: 不正なクロステナントアクセス、Secret漏えい、Artifact改ざん確定。`SEV2`: 主要API障害、Queueの継続停滞、復元不能。`SEV3`: 部分機能遅延、特定外部Toolの不可用。ルールには「誰に、どのDashboard、Runbook、通知頻度」を関連づける。

## 5. 故障注入
Worker kill→Lease stale Alert、DB切断→API error、Artifact破損→Integrity Failure、LLMタイムアウト→Experiment未評価。ログ／メトリクス／トレースが同じJobId・ProjectIdを指すことを検証する。

## 参照する実装・既存資料

- `infrastructure/monitoring/prometheus/prometheus.yml`
- `infrastructure/monitoring/grafana/dashboards/dip-operational.json`
- `infrastructure/monitoring/otel/otel-collector-config.yml`
- `docs/10-future/architecture/ARCH-005-OBSERVABILITY.md`
