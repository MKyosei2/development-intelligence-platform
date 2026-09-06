# ベンチマーク結果

以前の`TBD` placeholderはexact-revision measurement workflowへ置き換えられている。**この変更revisionについて、target Windows machineで実測するまでは値を主張しない。**

実行:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Run-PortfolioBenchmarks.ps1 -UpdateDocumentation
```

Commandはraw Evidenceを`artifacts/portfolio-benchmark/portfolio-benchmark-results.json`へ書き出し、`-UpdateDocumentation`指定時は実測値でこのfileを書き換える。

| Scenario | Target | Current exact-revision evidence |
|---|---:|---|
| Project list p95 (250 projects) | <500 ms | NOT YET MEASURED |
| Collector p95 | <1000 ms | NOT YET MEASURED |
| 100 req/s error rate | <1% | NOT YET MEASURED |
| 1M Metric query p95 | <2000 ms | NOT YET MEASURED |

古いbenchmark Evidenceを新しいsource revisionのEvidenceとして提示しない。

## Runtime Profile（実行プロファイル）

Runnerは`Development`ではなく専用`Benchmark` ASP.NET Core environment（Evidence labelは`ProductionLikeBenchmark`）を使用する。Loopback authenticationはisolated benchmark harness内だけで無効化し、その事実をresult JSONへ記録する。Default sustained-load windowは900秒・25 requests/second。Callerはより長いrelease/nightly windowを明示指定できる。Exact revisionを実測するまでperformance numberは公開しない。
