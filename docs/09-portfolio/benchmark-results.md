# Benchmark Results

The previous `TBD` placeholders are replaced by an exact-revision measurement workflow. **No value is claimed for this modified revision until it has been measured on the target Windows machine.**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Run-PortfolioBenchmarks.ps1 -UpdateDocumentation
```

The command writes raw evidence to `artifacts/portfolio-benchmark/portfolio-benchmark-results.json` and, with `-UpdateDocumentation`, rewrites this file with the measured values.

| Scenario | Target | Current exact-revision evidence |
|---|---:|---|
| Project list p95 (250 projects) | <500 ms | NOT YET MEASURED |
| Collector p95 | <1000 ms | NOT YET MEASURED |
| 100 req/s error rate | <1% | NOT YET MEASURED |
| 1M Metric query p95 | <2000 ms | NOT YET MEASURED |

Older benchmark evidence is never presented as evidence for a newer source revision.


## Runtime profile

The runner uses the dedicated `Benchmark` ASP.NET Core environment (`ProductionLikeBenchmark` evidence label) rather than `Development`. Loopback authentication is disabled only inside the isolated benchmark harness and that fact is recorded in the result JSON. The default sustained-load window is 900 seconds at 25 requests/second; callers may explicitly choose a longer release/nightly window. No performance number is published until the exact revision is actually measured.
