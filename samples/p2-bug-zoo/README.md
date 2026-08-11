# P2 experiment fixtures

These samples are intentionally abnormal fixtures for local validation. They are not production code.

- `python/soak_memory_growth/app.py`: retains memory and emits decreasing FPS telemetry.
- `python/fuzz_target/app.py`: returns non-zero for a known byte pattern so fuzz evidence can be validated.

Run only in the repository QA workspace. P2 execution APIs intentionally reject paths outside the configured workspace root.
