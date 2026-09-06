# 検証済みBaseline Evidence

このdirectoryには**実際のGitHub Actions Evidenceをreviewer向けに複製したもの**を保存する。Historical baselineであり、新しいrevisionの証明ではない。

| Evidence | GitHub Actions artifact | Workflow head SHA | Embedded measurement/runtime provenance | Artifact SHA-256 |
|---|---:|---|---|---|
| `benchmark-results.*` | `9208195351` | `8891f2096f957f638342db7fbf27024440cccd4d` | `621e900d08c048924646ffa672608b1ec7e8af21` | `08066ff1abc35c2d58bac25860f0ff9f783f6d340fb3f66b37c7c1c681a47a8d` |
| `qa-bug-zoo-results.*` | `9208238950` | `8891f2096f957f638342db7fbf27024440cccd4d` | QA JSON内部にcommit fieldなし。workflow head SHAを使用 | `939c91283e1f550874bd50d13ee16df283084c0b5e06fbf7a815efd63964c14a` |
| `WINDOWS_DOTNET_RUNTIME_PASS.json` | `9208190739` | `8891f2096f957f638342db7fbf27024440cccd4d` | `621e900d08c048924646ffa672608b1ec7e8af21` | `24ab6bf3dee638c1669ea24b3a6ce7949af8ac10ccfbf13162773c6b14925ea3` |

Benchmark/Windows runtime JSONは、GitHub artifact containerをuploadしたworkflow headが`8891f...`である一方、内部の`commit` provenanceとして`621e900...`を持つ。そのためDIPはmeasurement/runtime claimに対して**より保守的なembedded provenanceを採用**し、同一だと装わず両方のidentifierを記録する。

QA Bug Zoo result JSONにはcommit fieldがないため、immutable artifact metadata/workflow head（`8891f...`）をrevision bindingとして扱う。

代表的な実測結果:

- Project list p95: **4.174 ms**
- Collector p95: **34.507 ms**
- 100 req/s: **0 failures / 1,500 requests**
- Persisted 1,000,000-row Metric query p95: **363.796 ms**
- QA Bug Zoo: **19/19 detected**, **0/7 false positives**, **9/9 reproduced**
- Windows .NET: Release solution build + Domain/Application/Infrastructure test phases **PASS**

Unity full-runtime PASSはここでは主張しない。これらのfileを後続commitのPASS Evidenceとしてre-labelしてはならない。
