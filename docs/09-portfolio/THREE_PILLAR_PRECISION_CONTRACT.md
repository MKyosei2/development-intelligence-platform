# Three-pillar precision contract

The final portfolio surface treats the following three capabilities as one evidence pipeline:

1. **Game Development QA** produces revision-bound observations.
2. **Project Truth / Evidence** evaluates claims using only evidence applicable to the current observed revision.
3. **Verification / Release Operations** fails closed unless CI, performance, engine runtime, engine quality, and Project Truth all agree on the final revision.

## Project Truth invariants

- Historical evidence remains visible but cannot verify a newer revision.
- Evidence derived from the same `PipelineRunId` may inherit the revision carried by the revision-bound record from that run.
- Negative status phrases such as `未完了` and `not completed` are evaluated before positive completion tokens.
- A `Blocked` or `Failed` claim is not contradicted merely because the supporting evidence itself is a failure.
- Current Project blockers and adaptive risk ignore failures that belong only to a historical revision.

## QA invariants

- `metrics=[]` and `issues=[]` is not a passing quality gate.
- Collector issue-count summary metrics by themselves are not sufficient coverage for PASS.
- Unity scene findings use stable rule identifiers so display text can change without changing issue identity.

## Final release invariants

`FINAL PORTFOLIO EVIDENCE: PASS` requires all of the following for one final DIP SHA:

- required GitHub Actions matrix: 11/11 Green;
- exact-revision benchmark: PASS;
- real Unity runtime: verified;
- Unity quality gate: PASS;
- real Unreal runtime: verified;
- Unreal quality gate: PASS;
- real-project Project Truth evidence present;
- Project Truth quality gate: `Passed`;
- real-project completion claim: `Confirmed`;
- Project Truth contradiction count: `0`;
- Project Truth target repository/commit matches the real Unity evidence.

Runtime verification alone is never equivalent to release readiness.
