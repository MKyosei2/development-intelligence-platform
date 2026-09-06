# API概要

## Core Project API（主要Project API）

```text
GET  /api/projects
POST /api/projects
```

## Collector / Evidence取り込み

```text
POST /api/v1/collector-reports
POST /api/v1/webhooks/github
```

CollectorとGitHub resultは既存pathから永続化し、Project Truth evaluation向け`EvidenceRecord`へも変換する。

## Project Truth / PM Dashboard（PMダッシュボード）

```text
GET  /api/v1/dashboard/projects
GET  /api/v1/dashboard/projects/{projectId}

GET  /api/v1/projects/{projectId}/claims
POST /api/v1/projects/{projectId}/claims
```

Dashboard APIはTruth Status、reported/evidence-backed progress、Evidence Confidence、Risk、Blocker、Report vs Reality、Evidence Timelineなど、PM-firstな状態を返す。既存Pipeline、Quality Gate、Metric、Quality Issue fieldもtechnical evidenceとして利用できる。

ClaimはProject memberの報告であり、machine Evidenceとは別に保存し、`Confirmed`、`PartiallyConfirmed`、`Unverified`、`Contradicted`、`Stale`で評価する。これらはstatementだけを評価する状態であり、reporterのtrust scoreではない。

## Python Analysis Service（Python分析Service）

独立FastAPI analysis serviceは既存analysis endpointを提供し続ける。これはanalysis/evidence providerであり、Project Truth evaluatorを置き換えない。

## Project Truth P1

```text
POST /api/v1/qa/runs
GET  /api/v1/qa/runs/{runId}
GET  /api/v1/qa/projects/{projectId}/crash-clusters
GET  /api/v1/qa/runs/{runId}/artifacts
GET  /api/v1/qa/runs/{runId}/artifacts/{path}

POST /api/v1/hardware/projects/{projectId}/calibrate
POST /api/v1/hardware/profiles/relative
GET  /api/v1/hardware/profiles
POST /api/v1/hardware/simulate
POST /api/v1/hardware/estimate-minimum
```

QAとsimulationはtechnical resultを`EvidenceRecord`へ書き込む。Project Truth evaluatorがProject levelの最終decision layerであり続ける。Runtime/simulation executable pathはconfigured QA workspace内へ制限する。

## P2 Decision Intelligence（意思決定支援）

```text
POST /api/v1/p2/community-hardware
GET  /api/v1/p2/community-hardware/confidence
POST /api/v1/p2/community-hardware/profile
POST /api/v1/p2/portability
POST /api/v1/p2/adaptive-qa-plan
POST /api/v1/p2/reliability/flaky-test
POST /api/v1/p2/reliability/runtime
POST /api/v1/p2/reliability/soak
POST /api/v1/p2/regression/bisect
POST /api/v1/p2/regression/origin
POST /api/v1/p2/mutation
POST /api/v1/p2/fuzz
POST /api/v1/p2/test-impact
POST /api/v1/p2/debug/symbolicate
POST /api/v1/p2/game/regression
POST /api/v1/p2/media/regression
POST /api/v1/p2/root-cause
GET  /api/v1/p2/projects/{projectId}/similar-incidents
GET  /api/v1/p2/projects/{projectId}/freshness
GET  /api/v1/p2/projects/{projectId}/conflicts
GET  /api/v1/p2/projects/{projectId}/schedule-risk
GET  /api/v1/p2/projects/{projectId}/completion-forecast
GET  /api/v1/p2/projects/{projectId}/stagnation
POST /api/v1/p2/dependency-risk
GET  /api/v1/p2/projects/{projectId}/daily-change
GET  /api/v1/p2/projects/{projectId}/health-change
GET  /api/v1/p2/projects/{projectId}/historical-risk
POST /api/v1/p2/decision-brief
GET  /api/v1/p2/projects/{projectId}/decision-traces
GET  /api/v1/p2/projects/{projectId}/intelligence
```

Execution endpointは`Qa:WorkspaceRoot`内のworkspace-relative targetだけを受け付ける。Community Hardware uploadには明示的consentが必要。Prediction/AI endpointはdecision-support contextを生成するが、それ単独でmeasured product failureを作成できない。
