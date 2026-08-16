# Verified baseline evidence

This directory stores **reviewer-facing copies of real GitHub Actions evidence**. It is a historical baseline, not proof for a newer revision.

| Evidence | GitHub Actions artifact | Workflow head SHA | Embedded measurement/runtime provenance | Artifact SHA-256 |
|---|---:|---|---|---|
| `benchmark-results.*` | `9208195351` | `8891f2096f957f638342db7fbf27024440cccd4d` | `621e900d08c048924646ffa672608b1ec7e8af21` | `08066ff1abc35c2d58bac25860f0ff9f783f6d340fb3f66b37c7c1c681a47a8d` |
| `qa-bug-zoo-results.*` | `9208238950` | `8891f2096f957f638342db7fbf27024440cccd4d` | no commit field inside the QA JSON; use the workflow head SHA | `939c91283e1f550874bd50d13ee16df283084c0b5e06fbf7a815efd63964c14a` |
| `WINDOWS_DOTNET_RUNTIME_PASS.json` | `9208190739` | `8891f2096f957f638342db7fbf27024440cccd4d` | `621e900d08c048924646ffa672608b1ec7e8af21` | `24ab6bf3dee638c1669ea24b3a6ce7949af8ac10ccfbf13162773c6b14925ea3` |

The benchmark and Windows runtime JSONs carry their own `commit` provenance (`621e900...`) even though the GitHub artifact containers were uploaded by workflows whose head was `8891f...`. DIP therefore uses the **more conservative embedded provenance for the measurement/runtime claim** and records both identifiers instead of pretending they are identical.

QA Bug Zoo has no commit field inside the result JSON, so its immutable artifact metadata/workflow head (`8891f...`) is the revision binding.

Representative measured results:

- Project list p95: **4.174 ms**
- Collector p95: **34.507 ms**
- 100 req/s: **0 failures / 1,500 requests**
- persisted 1,000,000-row Metric query p95: **363.796 ms**
- QA Bug Zoo: **19/19 detected**, **0/7 false positives**, **9/9 reproduced**
- Windows .NET: Release solution build + Domain/Application/Infrastructure test phases **PASS**

The Unity full-runtime pass is deliberately not asserted here. These files must never be relabeled as PASS evidence for a later commit.
