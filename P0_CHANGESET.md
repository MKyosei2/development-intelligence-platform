# Project Truth P0 changeset

This changeset implements the PM / production-management Project Truth core.
QA remains one Evidence Provider and is not promoted to the platform's central
concept.

## New core domain

- `EvidenceRecord`
- `ProjectClaim`
- `ClaimEvidenceLink`
- `ProjectTruthSnapshot`
- `AuditLog`
- Evidence / Claim / Truth / Risk enums

## New application logic

- `EvidenceConfidenceEvaluator`
- `ClaimVerificationEvaluator`
- `ProgressEvaluator`
- `ProjectRiskEvaluator`
- `ProjectBlockerEvaluator`
- `ProjectTruthEvaluator`
- `ProjectTruthService`
- `ProjectClaimService`

## Existing ingestion extended

- Collector reports create Evidence in addition to their existing Pipeline /
  Metric / Quality flow.
- Signed GitHub webhook events create GitHub Evidence.
- Quality Gate remains available and is represented as one Evidence source.

## Persistence

New EF entities, configurations, repositories and DbSets are included.
Because the current repository has no migration baseline, existing databases in
no-migrations mode are extended idempotently by `P0SchemaBootstrapper`.

## PM-first UI

The project list and project dashboard now prioritize:

1. Project Truth state
2. Evidence-backed progress
3. Reported progress
4. Risk / blockers / mismatches
5. Top actions
6. Report vs Reality
7. Evidence Timeline
8. Technical evidence

The dashboard also allows a project status Claim to be submitted. A Claim is
verified against Evidence; DIP never scores the reporter's honesty.

## Tests

Added Domain/Application tests and two live Project Truth E2E scenarios:

- successful objective evidence verifies a completion Claim;
- Critical runtime evidence contradicts a completion Claim.

## Verification status

Source-level static validation is included in `P0_STATIC_VALIDATION.txt`.
The artifact-generation environment does not contain the .NET SDK, so build,
unit, integration and E2E execution must be performed in the normal DIP Windows
or CI environment before declaring P0 verified.
