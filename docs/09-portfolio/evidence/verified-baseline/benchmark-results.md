# ベンチマーク結果

この値はlive DIP stackから生成した実測値であり、estimateではない。

| Scenario | Target | Actual |
|---|---:|---:|
| Project list p95 | <500ms | **4.174 ms** |
| Collector p95 | <1000ms | **34.507 ms** |
| 100req/s error rate | <1% | **0.0000%** |
| 1M Metric query p95 | <2s | **363.796 ms** |

## Provenance（来歴）

- Commit: `621e900d08c048924646ffa672608b1ec7e8af21`
- Runner: `GitHub Actions 1000001826`
- OS: `Linux-6.17.0-1022-azure-x86_64-with-glibc2.39`
- Python: `3.12.13`
- Generated UTC: `2026-08-14T04:52:56.418071Z`
- Dataset: database-query scenario用に永続化した1,000,000 `Metrics` rows。
- Raw Evidence: `artifacts/portfolio-benchmark/benchmark-results.json`。
