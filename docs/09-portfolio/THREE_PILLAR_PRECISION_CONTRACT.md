# Three-pillar precision contract

The final portfolio surface treats the following three capabilities as one evidence pipeline:

1. **Game Development QA** produces revision-bound observations and deterministic Bug Zoo evidence.
2. **Project Truth / Evidence** evaluates claims using only evidence applicable to the current observed revision.
3. **Verification / Release Operations** fails closed unless the required CI, benchmark, QA and Project Truth evidence all belong to the same final DIP revision.

## Project Truth invariants

- Historical evidence remains visible but cannot verify a newer revision.
- Evidence derived from the same `PipelineRunId` may inherit the revision carried by the revision-bound record from that run.
- Negative status phrases such as `未完了` and `not completed` are evaluated before positive completion tokens.
- A `Blocked` or `Failed` claim is not contradicted merely because the supporting evidence itself is a failure.
- Current Project blockers and adaptive risk ignore failures that belong only to a historical revision.
- The deterministic portfolio scenario intentionally injects a Critical QA finding and a 100% `Complete` claim. Verification succeeds only when the quality gate becomes `Failed`, the claim becomes `Contradicted`, verified progress is below reported progress, and the contradiction is recorded.

## QA invariants

- `metrics=[]` and `issues=[]` is not a passing quality gate.
- Collector issue-count summary metrics by themselves are not sufficient coverage for PASS.
- Required Bug Zoo languages cannot be silently reclassified from unavailable/infrastructure-blocked to PASS.
- Runtime reproduction evidence must state when a host security policy required a verifier-owned fallback probe.
- Unity / Unreal collector contracts may be tested without claiming an actual engine runtime execution.

## Final release invariants

`FINAL PORTFOLIO EVIDENCE: PASS` requires all of the following for one final DIP SHA:

- required GitHub Actions matrix: 11/11 Green;
- exact-revision portfolio benchmark: PASS from a clean tracked tree;
- QA Bug Zoo: PASS for the required languages, with no blocking infrastructure failures;
- deterministic QA -> DIP -> Project Truth scenario: PASS;
- Project Truth expected product outcome: quality gate `Failed` and unsupported completion claim `Contradicted`;
- final evidence binding confirms that CI, benchmark, QA Bug Zoo and Project Truth all reference the same DIP SHA.

Engine-specific runtime evidence is **optional**. When no real Git-backed Unity or Unreal target project is available, engine runtime remains `NOT_EVALUATED`; this does not block final portfolio publication and must not be presented as runtime-verified.

Runtime verification of any single subsystem is never equivalent to final release readiness.
