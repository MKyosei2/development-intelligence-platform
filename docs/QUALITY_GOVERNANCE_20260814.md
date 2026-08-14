# QA Governance / Reliability Expansion — 2026-08-14

This expansion turns QA output into durable management intelligence. It is integrated into the existing Domain / Application / Infrastructure / API / Worker / Web architecture; it does not create a second QA product beside DIP.

## Processing flow

```text
QA Adapter / Test / Runtime / Benchmark
  -> Universal Diagnostic Normalizer (dip.finding.v2)
  -> QaRun + QaFinding + immutable artifacts
  -> Quality Governance
       -> Finding lifecycle + time-bounded waiver
       -> Test reliability / flaky classification
       -> Regression baseline (last-known-good / first-bad)
       -> Ownership rule -> CODEOWNERS fallback
       -> QA policy evaluation (GO / REVIEW REQUIRED / NO-GO)
       -> Evidence provenance + freshness/confidence
       -> QA self-health (product failure != QA infrastructure failure)
       -> Quality trend / technical debt
       -> QA wall/CPU/artifact cost budget
  -> Release Readiness / Project Intelligence dashboard
```

## Requested feature mapping

| Priority | Feature | Implementation |
|---|---|---|
| S | Flaky Test Detection / Test Reliability | `QaTestReliability`; repeated-run/reproduction observations are classified as Unknown / StablePass / StableFail / Flaky / EnvironmentFailure. |
| S | Finding Lifecycle / Suppression / Waiver | `QaFindingState`, `QaWaiver`; guarded state transitions, expiring/revocable waivers, no permanent silent ignore. |
| S | Regression Baseline Management | `QaRegressionBaseline`; baseline commit/value, last-known-good and first-bad tracking; manual rebaseline API. |
| S | Ownership / Automatic Triage | DIP path rules first, repository `CODEOWNERS` fallback, Team/Workstream assignment. |
| S | QA Policy / Release Gate Engine | Versioned `QaPolicyRules` evaluates critical/warning/flaky/infrastructure/runtime/CPU/artifact budgets and emits GO / REVIEW REQUIRED / NO-GO. |
| S | Execution Sandbox / Security Boundary | no shell execution, workspace/symlink boundary, trusted PATH-tool allowlist, secret-stripping environment, output/timeout/memory limits, process-tree kill, Windows Job Object CPU/memory limits, optional fail-closed external network-isolation proof. |
| S | Evidence Provenance / Reproducibility | Exact commit, runtime environment fingerprint, adapter/tool/version/sandbox data, normalizer/policy version, input/evidence hashes. |
| S | QA System Self-Health | Product findings are separated from unavailable/degraded QA adapters/tools/sandbox state. |
| A | Root Cause / Change Correlation | Governance correlation adds finding ownership + observation commit + last-known-good / first-bad candidates and complements the existing evidence-grounded root-cause service. |
| A | Quality Trend / Technical Debt | Per-run trend snapshots, debt score, direction and delta surfaced in Project Intelligence. |
| A | Smart Test Selection | Path-to-test impact rules select relevant tests ahead of the full suite. |
| A | Diagnostic Schema Versioning | `dip.finding.v2`, normalizer version and source-format/source-version are persisted while raw artifacts remain immutable. |
| A | Artifact Retention Policy | Project/kind retention rules are applied by `ArtifactService`; existing retention worker performs expiry cleanup. Critical incident artifacts can be preserved. |
| A | Data Freshness / Confidence | Fresh / Aging / Stale plus confidence %, reduced when QA self-health is degraded/unavailable. Release readiness requires exact-candidate provenance. |
| B | QA Cost / Runtime Budget | Wall duration, tool duration, actual observed CPU time and artifact bytes are persisted and checked against policy budgets. |
| B | Test Recommendation | Changed-path coverage recommendations, flaky-history recommendations and explicit coverage-gap recommendation. |
| B | Automatic Bug Minimization | Safe argument-level delta debugging inside the QA sandbox; source files are not rewritten automatically. |
| B | Cross-project Benchmark Comparison | Latest successful project runs for an identical benchmark definition/metric/aggregation are compared with environment fingerprints and an explicit comparability warning. |

## Persistence

Migration `202608140006_AddQaGovernance` adds governance tables and extends `qa_findings` with diagnostic schema metadata. The previous `202608130005_AddProjectForecasts` migration and legacy schema mappings remain part of the formal migration chain.

## Management API

The authenticated API is under `/api/v1/qa-governance`. It exposes overview, lifecycle transitions, waivers, reliability, baselines, ownership, policies, test-impact selection, retention, health, trends, root-cause correlation, freshness, cost, minimization and cross-project benchmark comparison. `GET /api/v1/qa-governance/diagnostic-schema` is informational and does not expose project data.

## Dashboard

`ProjectIntelligence.razor` now includes a QA Governance / Release Gate card showing gate verdict, QA self-health, open findings, flaky tests, regressions, active waivers, evidence freshness/confidence and technical-debt trend. The dashboard intentionally shows management decisions before low-level diagnostic logs.

## Verification rule

Source implementation is not runtime certification. Run on the exact target revision:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-QaGovernance.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-PortfolioFormal.ps1 -UnityPath "<Unity.exe>"
```

`Verify-PortfolioFormal.ps1` now fails if QA Governance verification fails. A PASS artifact belongs only to the commit recorded in that artifact; old verification evidence must not be promoted to a newer revision.
