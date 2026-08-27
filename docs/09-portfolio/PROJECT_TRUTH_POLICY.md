# Project Truth Policy

## Purpose

`ProjectTruthEvaluator` and `ProjectRiskEvaluator` are deterministic decision-support components. Their job is to make the relationship between reported progress and observed evidence explicit and explainable.

The evaluators previously embedded several numeric thresholds and penalties directly in code. Those values are now centralized in `ProjectTruthPolicy` so that the decision rule is visible, reviewable, testable, and replaceable without editing evaluator logic.

## Default policy

The default policy intentionally preserves historical behavior:

| Setting | Default | Meaning |
|---|---:|---|
| Critical blocker health penalty | 25 | Health-score penalty per critical blocker |
| Blocker health penalty | 12 | Health-score penalty per blocker |
| Contradiction health penalty | 10 | Health-score penalty per contradicted claim |
| Unverified claim penalty | 3 | Per-claim health penalty, capped separately |
| Max unverified-claim penalty | 12 | Prevents unbounded accumulation |
| Progress-gap penalty factor | 0.25 | Health points lost per reported-vs-verified percentage point |
| Max progress-gap penalty | 20 | Caps progress-gap health impact |
| High-risk contradiction threshold | 2 | Two or more contradictions become High risk |
| Medium-risk progress gap | 20% | Large reported-vs-verified gap becomes Medium risk |
| Near-completion reported progress | 80% | Unverified claims are more important near reported completion |
| Low-confidence risk boundary | Low | Low/Unverified evidence produces at least Medium risk |

These are **compatibility defaults**, not a claim that 20%, 25 points, or any other value is universally optimal.

## Why deterministic policy instead of ML?

For the current portfolio scope, deterministic policy has important advantages:

- every decision can be traced to evidence and a visible rule;
- behavior is reproducible across runs;
- there is not enough unbiased labeled production data to justify a learned model;
- reviewers can inspect boundary behavior directly in unit tests;
- operators can tune policy without retraining or introducing opaque confidence claims.

A learned model could later rank or recommend risk, but should not replace the evidence/provenance boundary unless training data, drift monitoring, and validation are available.

## Calibration procedure for real production data

A production deployment should not simply accept the default values forever. A defensible calibration process is:

1. Collect historical release/project snapshots with immutable Evidence and the actual later outcome.
2. Define labels independently of the policy being tuned, for example `safe`, `review-required`, `release-blocking`.
3. Split by project and time so near-duplicate revisions do not leak between training/calibration and validation sets.
4. Evaluate candidate thresholds/penalties with a cost function that weights false negatives more heavily when a missed blocker is more expensive than an unnecessary review.
5. Report confusion matrix, precision/recall, false-positive rate, and boundary sensitivity rather than only aggregate accuracy.
6. Select a policy, assign it a version, and record the dataset/evidence revision used for calibration.
7. Re-evaluate after workflow/tooling changes or meaningful evidence-distribution drift.

## Testing boundary

`ProjectTruthPolicyTests` verifies that:

- the default policy preserves the existing 20% progress-gap boundary;
- a different threshold changes behavior without modifying evaluator code;
- contradiction thresholds are configurable;
- invalid policy values fail fast.

These tests prove implementation behavior. They do **not** prove that the default policy is empirically optimal. That claim would require the calibration process above and a real labeled dataset.

## Operational rule

Policy changes should be reviewed like code changes:

- describe the reason;
- attach calibration/validation evidence when available;
- identify expected false-positive/false-negative impact;
- preserve the previous policy version for reproducibility;
- never rewrite historical Evidence to make a new policy appear retrospectively correct.
