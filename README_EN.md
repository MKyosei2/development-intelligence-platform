# Development Intelligence Platform (DIP)

[日本語](./README.md) | **English**

DIP is a **Development Intelligence platform for game, media, and software development teams**. It brings Git, CI, QA, runtime, performance, and project data into a shared Project Context and treats them as evidence whose **source, revision, and freshness** can be traced.

**Portfolio focus:** Rather than presenting C# / .NET 8, PostgreSQL, Python, C++ / native QA, Unity / Unreal integration, and GitHub Actions as isolated demos, DIP connects them around a single theme: **evidence-grounded development infrastructure with exact-revision verification**.

![DIP portfolio demo](docs/09-portfolio/demo/dip-portfolio-demo.gif)

> This GIF is a sample-data preview. It is not used as evidence of Runtime Verification. For the current revision, GitHub Actions, benchmark results, QA Bug Zoo evidence, Project Truth, and final evidence artifacts take precedence.

## 60-Second Review

1. Start with the **Three Areas to Review First** section to understand the core of the project.
2. Read [`QA_EVIDENCE_CASE_STUDY.md`](docs/09-portfolio/QA_EVIDENCE_CASE_STUDY.md) to see how Critical QA evidence rejects an unsupported `Complete` claim.
3. Review **Verification Status** and **Final Exact-Revision Gate** to see how DIP separates Implemented, Tested, and Runtime Verified states.
4. See [`ENGINEERING_JOURNEY.md`](docs/09-portfolio/ENGINEERING_JOURNEY.md) for how the engineering approach evolved.

## Verified Final Portfolio Revision

The final portfolio completion gate passed for the exact source revision below:

```text
b9425d036c634a4fb1cba5ac9755df6d28b35071
```

| Final gate | Result |
|---|---|
| GitHub Actions | 11/11 Green |
| Docker Primary | PASS |
| Migration Apply / Rollback / Reapply | PASS |
| Integration E2E | PASS |
| GUI Playwright | PASS |
| Disaster Recovery | PASS |
| Benchmark / Soak | PASS |
| Governance | PASS |
| Dependency Audit | PASS |
| QA Bug Zoo Determinism | 10/10 PASS |
| Detection Recall | 100% |
| False Positive Rate | 0% |
| Reproduction Rate | 100% |
| Unstable Case Count | 0 |
| Project Truth Expected-Failure Scenario | PASS |
| Final Evidence Binding | PASS |
| Current Revision Readiness | PASS |

The final Project Truth scenario also converged correctly after asynchronous quality-gate processing and verified the intended outcome:

```text
Quality Gate = Failed
Unsupported Complete claim = Contradicted
Verified progress < Reported progress
Contradiction recorded
```

This is a **portfolio verification result**, not a claim that the system has been deployed to a production environment.

## Three Areas to Review First

### 1. Project Truth / Evidence

DIP separates **claims** from **evidence** and tracks source, revision, freshness, and contradictions.

The presence of a source alone is not enough to mark something as verified. DIP only treats a state as Confirmed when evidence exists that supports the **current revision**.

### 2. Game Development QA

**Evidence-Grounded Autonomous Debugging** follows this sequence:

```text
Hypothesis
    ↓
Candidate
    ↓
Isolated Verification
    ↓
Human Review
```

AI output itself is not treated as verification evidence.

Evidence from Universal QA, C++, runtime checks, CI, and engine integration is connected to QA findings, reproducibility, regressions, performance, and quality gates.

### 3. Verification / Release Operations

DIP binds Unit / Integration / E2E tests, GUI verification, security, benchmarks, migrations, backup / restore, and release readiness to an **exact source revision**.

It does not reinterpret any of the following as a success for the current revision:

- a historical PASS,
- a check that has not run,
- an environment-blocked check.

## Verification Status

The README itself is not considered verification evidence.

**The final portfolio revision is not described as `Current Revision PASS` until CI, benchmark, QA Bug Zoo, Project Truth, and final evidence are all bound to the same SHA.**

| Claim | Requirement for treating it as verified |
|---|---|
| Implemented | Corresponding source exists |
| Tested | Tests / workflows for the target revision succeed |
| Runtime Verified | Evidence exists from an actual runtime for the target revision |
| Performance Verified | Benchmark artifact exists for a clean working tree and exact SHA |
| Policy Verified | Policy evaluator and related evidence succeed for the same revision |
| Production Deployed | Claimed only when actual production deployment evidence exists |
| Final Portfolio Revision | Required CI, benchmark, QA Bug Zoo, Project Truth, and final evidence binding all belong to the same SHA |

The final Source of Truth is GitHub Actions, release/evidence artifacts, and `artifacts/final-evidence/`.

## QA Evidence Case Study

The central final-portfolio scenario does **not** pretend to be a real production defect.

It is a **deterministic verification fixture** that intentionally injects a Critical QA finding.

```text
Intentional Critical QA finding
        ↓
DIP Collector / API ingestion
        ↓
Quality Gate = Failed
        ↓
Claim: Complete / 100%
        ↓
Project Truth
        ↓
Claim = Contradicted
Verified progress < Reported progress
Contradiction recorded
```

The verifier passes **not** because the product quality gate passes, but because DIP correctly rejects an unsupported completion claim while Critical evidence exists.

- Implementation and design: [`QA_EVIDENCE_CASE_STUDY.md`](docs/09-portfolio/QA_EVIDENCE_CASE_STUDY.md)
- 2–3 minute demo flow: [`QA_EVIDENCE_DEMO.md`](docs/09-portfolio/QA_EVIDENCE_DEMO.md)

## Engineering Principles

- **Implemented ≠ Tested ≠ Runtime Verified**
- **Separate claims from evidence**
- **Do not use AI output as the source of truth**
- **Historical PASS ≠ Current Revision PASS**
- **Production / publication gates fail closed**
- **Infrastructure failure is neither product failure nor PASS**

### C++ QA Determinism / Timeout Evidence Boundary

For C++ QA, DIP separates:

- **CTest** as test-orchestration evidence, and
- the **sanitizer-instrumented direct runtime** as the path for determining memory-safety findings.

A CTest infrastructure timeout is not promoted to a product `Critical/hang`, but it is also never silently treated as a PASS.

DIP records stdout/stderr, timeout state, exit code, CPU usage, peak memory, sandbox information, sanitizer environment, and the pre-kill process tree in `execution-diagnostics.json`.

The following C++ Bug Zoo cases are evaluated with 10-run determinism verification on the same SHA:

```text
cpp/clean
memory_leak
buffer_overflow
double_free
use_after_free
```

The result is only treated as Current Revision evidence when all of the following hold:

```text
10/10 PASS
Recall = 100%
False Positive Rate = 0%
Reproduction Rate = 100%
UnstableCaseCount = 0
```

```powershell
$sha = (git rev-parse HEAD).Trim()
.\scripts\DIP_QA_BUG_ZOO_DETERMINISM.ps1 -ExpectedSha $sha -Runs 10
```

## CI Execution Policy

To avoid consuming hosted-runner quota on every normal push, DIP uses a three-level policy:

```text
1. work/* / feat/* / fix/* / chore/* -> automatic hosted Actions: 0
2. workflow_dispatch                  -> Fast / Targeted / Full
3. main + .portfolio-ci-trigger       -> required final-submission / release workflows
```

Manual CI examples:

```powershell
.\scripts\Invoke-ManualCI.ps1 -Preset Fast -Ref work/example
.\scripts\Invoke-ManualCI.ps1 -Preset Targeted -Target DotNet,Python,Native -Ref work/example
.\scripts\Invoke-ManualCI.ps1 -Preset Full -Ref work/example
```

See [`CI_EXECUTION_POLICY.md`](docs/09-portfolio/CI_EXECUTION_POLICY.md) for details.

## Final Exact-Revision Gate

For the final source revision on `main`, all 11 required workflows must be Green:

1. Commit Message
2. .NET CI
3. Python CI
4. Native Collector CI
5. Integration and E2E Tests
6. GUI E2E
7. Native Package Build
8. Security Scan
9. Universal QA Benchmark
10. Unreal Collector Contract CI
11. Portfolio Publication Readiness

`missing`, `cancelled`, `skipped`, and `infrastructure-blocked` states are never reinterpreted as PASS.

Artifact-upload quota is kept separate from product verification. Auxiliary uploads may be best-effort only **after** the verification itself has succeeded.

```powershell
$sha = (git rev-parse HEAD).Trim()
python tools/verify_final_ci_matrix.py --sha $sha --wait-minutes 60
.\scripts\Complete-PortfolioRelease.ps1 -Version 1.0.0
```

## Benchmark / Soak

A performance result is valid as Current Revision evidence only when it was generated from a clean working tree and the exact source SHA being claimed.

```powershell
$sha = (git rev-parse HEAD).Trim()
.\scripts\Run-PortfolioBenchmarks.ps1 -ExpectedSha $sha
```

Canonical artifacts:

```text
artifacts/portfolio-benchmark/portfolio-benchmark-results.json
artifacts/portfolio-benchmark/portfolio-benchmark-results.sha256
```

The final completion gate keeps the memory-growth threshold unchanged at:

```text
Memory growth <= 256 MB
```

## Exact-Revision Evidence Package

After final evidence has been collected, DIP packages the CI matrix, benchmark, QA Bug Zoo, 10-run determinism results, Project Truth, Docker Primary evidence, and governance evidence into one ZIP archive.

Packaging itself is fail-closed and requires:

```text
HEAD == origin/main == evidence SHA
tracked working tree == clean
all mandatory artifacts == PASS
```

```powershell
$sha = (git rev-parse HEAD).Trim()
.\scripts\Package-PortfolioEvidence.ps1 -ExpectedSha $sha
```

The ZIP contains:

```text
EVIDENCE_PACKAGE_MANIFEST.json
SHA256SUMS.txt
```

and the ZIP itself receives a `.sha256` sidecar.

## Engine Integration Verification Boundary

Unity / Unreal collectors and integration source are part of the project.

However, when no real target project is available, engine-specific runtime verification is **not** made a mandatory condition for final portfolio publication.

```text
Unity engine runtime  = NOT_EVALUATED
Unreal engine runtime = NOT_EVALUATED
```

Optional engine-specific verification is only run when a real target project is available.

The GitHub-hosted Unreal workflow validates contract / unit behavior. It is **not** presented as a substitute for actual Unreal Editor runtime verification.

## Native-First Architecture

```text
Browser
  └─ Dip.Web (.NET / Blazor) :5081
       └─ Dip.Api (.NET)     :5080
            ├─ PostgreSQL       durable truth + durable queue
            ├─ Python Analysis  :8100
            └─ filesystem       artifacts / workspaces

Dip.Worker (.NET Worker)     :9091 metrics
  ├─ PostgreSQL-backed jobs / queue
  ├─ native QA processes
  └─ engine / native / toolchain integrations
```

PostgreSQL acts as the durable Source of Truth and the queue / coordination boundary.

.NET and Python services can run directly on the host. Docker is used for verification and supported deployment workflows, but it is not a mandatory dependency for the formal **Native-first runtime** architecture.

See [`ARCHITECTURE_OVERVIEW.md`](docs/09-portfolio/ARCHITECTURE_OVERVIEW.md) for details.

## Evidence and Verification Boundaries

| State | Meaning |
|---|---|
| Implemented | Source exists |
| Tested | The relevant test succeeds |
| Runtime Verified | The target revision was verified in a real runtime |
| Infrastructure Blocked | Verification could not complete because of runner / tool / environment constraints |
| NOT_EVALUATED | Explicitly outside the scope of the current claim |

Related documents:

- Historical baseline: [`docs/09-portfolio/evidence/verified-baseline/`](docs/09-portfolio/evidence/verified-baseline/)
- Exact-revision policy: [`docs/09-portfolio/evidence/latest-revision.md`](docs/09-portfolio/evidence/latest-revision.md)
- Benchmark rule: [`docs/09-portfolio/benchmark-results.md`](docs/09-portfolio/benchmark-results.md)
- Publication checklist: [`docs/09-portfolio/PUBLICATION_CHECKLIST.md`](docs/09-portfolio/PUBLICATION_CHECKLIST.md)

## Primary Technology Areas

- **C# / .NET 8** — Domain / Application / API / Worker / Blazor
- **PostgreSQL** — durable truth / queue / migration / disaster recovery
- **Python 3.12+** — analysis / QA / evidence tooling
- **C++ / native QA / collectors**
- **Unity / Unreal / DCC / toolchain integration**
- **GitHub Actions / Playwright**

## Local Runtime

The normal local environment uses:

- .NET 8
- PostgreSQL 16-compatible server and client tools
- Python 3.12+
- Git

Additional requirements:

- Node.js 22+ for browser E2E
- CMake + compiler toolchain for C++ QA
- Unity / Unreal only for optional engine-specific verification

`Start-DIP.ps1` checks PostgreSQL and starts Python Analysis, API, Worker, and Web.

`Stop-DIP.ps1` stops DIP processes.

## Recommended Reading Order for Reviewers

1. This README
2. [`QA_EVIDENCE_CASE_STUDY.md`](docs/09-portfolio/QA_EVIDENCE_CASE_STUDY.md)
3. [`ENGINEERING_JOURNEY.md`](docs/09-portfolio/ENGINEERING_JOURNEY.md)
4. [`GAME_DEVELOPMENT_SCENARIO.md`](docs/09-portfolio/GAME_DEVELOPMENT_SCENARIO.md)
5. [`reviewer-brief.md`](docs/09-portfolio/reviewer-brief.md)
6. Architecture / Evidence / CI artifacts

DIP is not primarily a gameplay or rendering showcase.

It is a portfolio project focused on **Development Infrastructure, QA, Developer Productivity, Production Engineering, and evidence-grounded decision support for game development**.
