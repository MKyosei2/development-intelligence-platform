# ベンチマーク結果

この文書では、**Historical baseline**と**公開対象final SHAのexact-revision measurement**を分離します。Sourceを変更しただけでは過去のperformance値を新revisionへ継承しません。

## 公開ルール

公開対象revisionのperformance値として掲載できるのは、次をすべて満たすartifactだけです。

1. `artifacts/portfolio-benchmark/portfolio-benchmark-results.json`が実測runnerから生成されている。
2. artifactの`sourceCommit`が公開対象Git SHAと完全一致する。
3. 必須scenarioが完了し、失敗・未実行・infrastructure blockedをPASSへ読み替えていない。
4. 文書へ転記する値がraw artifactと一致する。

この4条件を満たさない場合、表は`NOT YET MEASURED`のままにします。

| Scenario | Target | Current exact-revision evidence |
|---|---:|---|
| Project list p95 (250 projects) | <500 ms | NOT YET MEASURED |
| Collector p95 | <1000 ms | NOT YET MEASURED |
| 100 req/s error rate | <1% | NOT YET MEASURED |
| 1M Metric query p95 | <2000 ms | NOT YET MEASURED |

## Historical baseline

`docs/09-portfolio/evidence/verified-baseline/`に保存されたbenchmarkは、measurement infrastructureが特定commitで実測を完了したことを示すbaselineです。Current revisionの値として再ラベルしません。

## Runtime Profile（実行プロファイル）

Runnerは専用`Benchmark` ASP.NET Core environment（Evidence labelは`ProductionLikeBenchmark`）を使用します。Loopback authenticationをisolated benchmark harness内で無効化した場合は、その事実をresult JSONへ記録します。Default sustained-load windowは900秒・25 requests/secondです。Release / nightlyではより長いwindowを明示できます。

## Final SHAの扱い

Portfolio公開直前にsourceをfreezeし、そのfinal SHAでbenchmarkを再実行します。測定後にsourceを1行でも変更した場合、そのartifactはhistorical Evidenceへ戻し、新しいSHAでは再度`NOT YET MEASURED`から扱います。これにより「測定後の修正だけを行い、古い数値をcurrentとして残す」状態を防ぎます。
