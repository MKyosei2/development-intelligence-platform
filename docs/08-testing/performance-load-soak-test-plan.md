# パフォーマンス・負荷・Soak・資源枯渇試験

> **文書ステータス:** 既存Runner/負荷計画に対する詳細化案  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 対象を区分
| 区分 | 測るもの | 観測主体 |
|---|---|---|
| API Load | p50/p95/p99、error、rate limit | API/負荷実行器 |
| DB Query | Evidence検索、Project一覧、100万Metric | PostgreSQL/Profiler |
| Queue Load | Job滞留、Lease、再試行、処理能力 | DB/Worker |
| CPU Memory Soak | 対象process RAM/CPU、GC、リーク傾向 | 対象プロセス |
| GPU Soak | Frame Time/VRAM、temperature等 | 実GPU + Telemetry |
| Multi-tenant fairness | 他Projectへの遅延影響 | Scheduler/tenant metrics |

## 2. 既存の目標値
現行`docs/08-testing/load-test-plan.md`はDashboard 20VU、Collector 100req/s、100万Metric Query、Queue 1万件、Python 20並列、Error Rate 1%未満を計画している。**数値は試験条件・目標であり、達成した実測値ではない。**

## 3. 実験設計
1. DIPとTargetのSHA、OS、CPU/RAM、DB version、storage、他の負荷を記録する。
2. 事前にデータ量、プロジェクト数、ユーザー数、Evidence/Artifact量を固定。
3. Warmup後、Steady 30min、Peak 5min、recovery、Soak数時間以上を別々に実行。
4. DB再起動・Worker kill・遅い外部LLMを故障注入し、リソース回復を確認。
5. 測定時刻と最小Sample、負荷実行器自身の限界を公開する。

## 4. 合否基準を分離
- 機能正確性：同時負荷でEvidence/Release判定が壊れない。
- 速度：p95/p99、throughput、Queue Ageが事前閾値に収まる。
- 信頼性：Timeout、Error rate、Dead letter、リトライ数。
- 故障復旧：Kill後に復帰、重複確定がない。
- 資源：Memory leak傾向、disk fill、orphan processがない。

## 5. 出力テンプレート
`case, host, commit, dataset, VU/reqPerSec, duration, sampleCount, p95, p99, throughput, errors, memoryPeak, queueAge, recoverySeconds, rawArtifactDigest, verdict`。閾値未決定時はTBD、Run未実施時はNOT_EVALUATEDとする。

## 参照する実装・既存資料

- `docs/08-testing/load-test-plan.md`
- `tests/load/k6/dashboard-load.js`
- `tests/load/k6/pipeline-load.js`
- `tests/load/k6/webhook-load.js`
- `scripts/Run-GovernanceLoadTest.ps1`
