# Benchmark Results

以下は推定値ではなく、GitHub Actions 上で実際に DIP の PostgreSQL / Redis / Python analysis / API を起動して取得した実測値です。

| Scenario | Target | Actual |
|---|---:|---:|
| Project list p95 | <500ms | **6.391 ms** |
| Collector p95 | <1000ms | **27.743 ms** |
| 100req/s error rate | <1% | **0.0000%** |
| 1M Metric query p95 | <2s | **314.268 ms** |

## Measurement details

- Project list: 150 samples; p50 3.242 ms / p95 6.391 ms / p99 9.540 ms
- Collector ingestion: 40 samples; p50 16.769 ms / p95 27.743 ms / p99 394.663 ms
- 100 req/s: 1,500 requests over 14.997 s; achieved 100.02 req/s; failures 0
- 1M Metric query: 1,000,000 rows in PostgreSQL; 10 measured samples; p50 282.116 ms / p95 314.268 ms

## Provenance

- Evidence generated UTC: `2026-08-14T04:34:16.635652Z`
- GitHub Actions merge commit measured: `20ce96e9cf108115d11641187177b29efdf76516`
- Runner: `GitHub Actions 1000001643`
- OS: `Linux-6.17.0-1020-azure-x86_64-with-glibc2.39`
- Python: `3.12.13`
- Artifact: `portfolio-benchmark-evidence` from workflow run `31770142295`
- Measurement implementation: `scripts/measure_portfolio_benchmarks.py`

Benchmark values are updated only from generated evidence; the repository does not substitute estimated values when a measurement has not run.
