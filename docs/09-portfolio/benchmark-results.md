# ベンチマーク結果

この文書では、**Historical baseline**と**公開対象final SHAのexact-revision measurement**を分離します。Sourceを変更しただけでは過去のperformance値を新revisionへ継承しません。

## 公開ルール

公開対象revisionのperformance値として扱えるのは、次をすべて満たすartifactだけです。

1. `artifacts/portfolio-benchmark/portfolio-benchmark-results.json` が実測runnerから生成されている。
2. artifactの `sourceCommit` が公開対象Git SHAと完全一致する。
3. `sourceWorkingTreeDirty` が `false` である。
4. `passed` が `true` であり、必須scenarioが完了している。
5. 失敗・未実行・infrastructure blockedをPASSへ読み替えていない。
6. Releaseで公開するJSONとSHA-256が、final evidenceに束ねたartifactと一致する。

## Current exact-revision evidence の参照方法

Current revisionの数値は、このtracked Markdownへ直接書き戻しません。Benchmark実測後に数値をcommitするとGit SHAが変わり、実測対象revisionと文書revisionが一致しなくなるためです。

公開時のcanonical sourceは、**公開対象SHAから作成されたGitHub Releaseの次のasset**です。

```text
portfolio-benchmark-results.json
portfolio-benchmark-results.sha256
FINAL_EVIDENCE_SUMMARY.json
SHA256SUMS.txt
```

Release asset内の `sourceCommit` がRelease/tagのcommit SHAと一致し、`sourceWorkingTreeDirty == false`、`passed == true` を満たす場合、そのJSONに記録された値をCurrent exact-revision measurementとして扱います。

該当するRelease assetが存在しないSHA、または上記条件を満たさないSHAは **NOT YET MEASURED** です。

| Scenario | Canonical gate | Canonical current value |
|---|---:|---|
| Project list p95 (250 projects) | <500 ms | matching Release assetの実測値 |
| Collector ingestion p95 | <250 ms | matching Release assetの実測値 |
| 100 req/s burst error rate | 0% | matching Release assetの実測値 |
| 1M Metric query p95 | <1000 ms | matching Release assetの実測値 |
| Paced soak error rate | <=0.1% | matching Release assetの実測値 |
| API working-set growth during soak | <=256 MB | matching Release assetの実測値 |

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

Portfolio公開直前にsourceをfreezeし、`.portfolio-ci-trigger`を含むfinal publication revisionをmainへ反映します。そのmain SHAでrequired 11 workflowを全てGreenにした後、同じSHAを`-ExpectedSha`へ渡してbenchmarkを実測します。

Benchmark、Unity、Unreal、Project Truth、CI matrixを同じDIP SHAへ束ね、`tools/finalize_portfolio_evidence.py`でfail-closedに検証します。Releaseへ公開する値は、このfinal evidence bundleに含まれるartifactだけを使用します。

測定後にsourceを1行でも変更した場合、そのartifactはhistorical Evidenceへ戻します。新しいSHAでは再測定が必要です。
