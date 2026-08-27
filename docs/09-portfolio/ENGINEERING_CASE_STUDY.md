# Engineering Case Study

This document is the short technical reading path for reviewers who want to understand **why DIP is an engineering project**, not only a feature list.

## 1. Problem framing

Game and software production data is fragmented by nature. Git history, CI, tests, engine/runtime events, performance measurements, incidents, and project-management reports all have different schemas, clocks, failure modes, and trust levels. A dashboard can aggregate those sources visually while still making unsafe decisions if it silently treats every source as equally authoritative.

DIP therefore treats **provenance and verification state as first-class data**. A Claim is what somebody or some system reports. Evidence is an observable result tied to a source/revision/time. The platform evaluates their relationship rather than assigning a score to a person.

## 2. Engineering decision: provider-neutral Evidence

The core boundary is not "GitHub data" or "Unity data". It is an Evidence contract that external providers normalize into.

Why:

- GitHub, Unity, Unreal, Maya, native runtimes, and CI can be added without teaching the Project Truth domain every provider-specific schema.
- Provider failures remain distinguishable from product failures.
- Evidence retains revision/provenance instead of becoming an untraceable dashboard number.

Trade-off:

- Normalization adds schema and adapter complexity.
- A provider-neutral model must avoid becoming a lowest-common-denominator abstraction.

Mitigation:

- Keep provider-specific payload/artifacts available while promoting only stable, decision-relevant fields into common Evidence.
- Fail closed on ambiguous cross-source matching instead of silently merging identities.

## 3. Engineering decision: PostgreSQL is truth; Redis is coordination

DIP separates durable state from delivery mechanics.

- PostgreSQL: durable project/evidence/job state.
- Redis: coordination and delivery acceleration.
- Worker: asynchronous ingestion/analysis execution.
- Python service: isolated analysis capability.

The design avoids making queue delivery itself the authoritative business state. Jobs can therefore be reasoned about in terms of persisted state, lease/heartbeat/retry/recovery, rather than "the message disappeared so the work must be done".

## 4. Engineering decision: exact-revision evidence

A common portfolio and CI mistake is to show a historical green badge or benchmark and imply that the current source has the same state. DIP deliberately rejects that assumption.

The verification rule is:

> A newer revision does not inherit an older revision's PASS.

Historical benchmark and QA evidence remains useful as a baseline, but current source must pass its required workflows/runtime checks before it is described as verified. This is also why the README labels sample-data screenshots separately from runtime evidence.

## 5. Engineering decision: explicit Project Truth policy

Project Truth uses deterministic heuristics because explainability is more important here than pretending that a small portfolio dataset justifies an ML model. Previous evaluator constants are now centralized in `ProjectTruthPolicy` and injected into the evaluators.

Benefits:

- no scattered magic numbers;
- defaults preserve backward compatibility;
- thresholds can be versioned and calibrated without changing evaluator code;
- tests can exercise boundary behavior directly;
- future deployments can bind policy changes to evidence and review.

Important limitation:

The default values are conservative historical defaults, **not statistically proven optimum values**. The correct production process is to calibrate them on labeled historical decisions and keep a hold-out validation set. See `PROJECT_TRUTH_POLICY.md`.

## 6. What the measured baseline demonstrates

Historical evidence currently records, among other values:

- Project list p95: 4.174 ms;
- Collector p95: 34.507 ms;
- 100 req/s error rate: 0.0000% over 1,500 requests;
- 1M Metric query p95: 363.796 ms;
- QA Bug Zoo recall: 19/19;
- clean-control false positives: 0/7;
- reproducibility: 9/9.

Those numbers demonstrate that measurement infrastructure exists and has produced concrete results for a specific verified commit. They do not claim that every later commit has identical results.

## 7. Failure model

The system is designed around explicit failure boundaries:

- ingestion should be idempotent when provider IDs/revisions exist;
- infrastructure failure must not be relabeled as product failure;
- unavailable CI runners are not a PASS;
- AI/Fix Agent suggestions remain advisory until isolated verification succeeds;
- engine/device claims remain unverified without the corresponding runtime.

This distinction is intentionally visible to reviewers because production engineering is primarily about behavior under partial failure, not only the happy path.

## 8. Portfolio positioning

The strongest fit of DIP is:

- game development infrastructure / production engineering;
- backend/platform engineering;
- QA automation and developer productivity;
- CI/CD, release engineering, and observability;
- Technical Artist / Technical Director tooling where engine and DCC evidence matter.

It is not presented as a gameplay or rendering showcase. For those roles, DIP should be paired with a project that directly demonstrates gameplay, graphics, or simulation code.
