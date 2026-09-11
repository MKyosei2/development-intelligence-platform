# ベンチマーク結果

この文書では、**Historical baseline**と**公開対象final SHAのexact-revision measurement**を分離します。Sourceを変更しただけでは過去のperformance値を新revisionへ継承しません。

## 公開ルール

公開対象revisionのperformance値として掲載できるのは、次をすべて満たすartifactだけです。

1. `artifacts/portfolio-benchmark/portfolio-benchmark-results.json` が実測runnerから生成されている。
2. artifactの `sourceCommit` が公開対象Git SHAと完全一致する。
3. `sourceWorkingTreeDirty` が `false` である。
4. 必須scenarioが完了し、失敗・未実行・infrastructure blockedをPASSへ読み替えていない。
5. 文書やReleaseで参照する値がraw artifactと一致する。

この条件を満たさない場合、値は`NOT YET MEASURED`のまま扱います。

| Scenario | Canonical gate | Current exact-revision evidence |
|---|---:|---|
| Project list p95 (250 projects) | <500 ms | NOT YET MEASURED |
| Collector ingestion p95 | <250 ms | NOT YET MEASURED |
| 100 req/s burst error rate | 0% | NOT YET MEASURED |
| 1M Metric query p95 | <1000 ms | NOT YET MEASURED |
| Paced soak error rate | <=0.1% | NOT YET MEASURED |
| API working-set growth during soak | <=256 MB | NOT YET MEASURED |

これらは`Run-PortfolioBenchmarks.ps1`内のgateと同一です。測定後に都合のよい閾値へ変更しません。

## Exact-revision guard

Runnerは実行開始時に次をfail-closedで確認します。

- `git rev-parse HEAD`が40桁SHAとして取得できる。
- working treeがcleanである。
- `-ExpectedSha`を指定した場合、checkout SHAと完全一致する。

最終提出では必ず次のように実行します。

```powershell
$sha = (git rev-parse HEAD).Trim()
.\scripts\Run-PortfolioBenchmarks.ps1 -ExpectedSha $sha
```

結果はcanonical artifactとそのSHA-256として保存します。

```text
artifacts/portfolio-benchmark/portfolio-benchmark-results.json
artifacts/portfolio-benchmark/portfolio-benchmark-results.sha256
```

## Historical baseline

`docs/09-portfolio/evidence/verified-baseline/`に保存されたbenchmarkは、measurement infrastructureが特定commitで実測を完了したことを示すbaselineです。Current revisionの値として再ラベルしません。

## Runtime Profile（実行プロファイル）

Runnerは専用`Benchmark` ASP.NET Core environmentを使用し、Native PostgreSQL上でProject List、Collector ingestion、burst、1M Metric aggregation、paced soakを測定します。Default sustained-load windowは900秒・25 requests/secondです。

## Final SHAの扱い

Portfolio公開直前にsourceをfreezeし、`.portfolio-ci-trigger`だけを変更するfinal PRをmainへmergeします。その結果できたmain SHAでrequired 11 workflowを全てGreenにした後、同じSHAを`-ExpectedSha`へ渡してbenchmarkを実測します。

測定後にsourceを1行でも変更した場合、そのartifactはhistorical Evidenceへ戻します。新しいSHAでは再度`NOT YET MEASURED`として扱います。
