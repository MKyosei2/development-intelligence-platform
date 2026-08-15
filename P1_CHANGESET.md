# P1 cumulative changeset

## Core rule

P1 strengthens the P0 Project Truth / Evidence First architecture. It does not
turn DIP into a QA-centric product. Python/C#/C++ QA, reproduction, recordings
and hardware simulations generate evidence consumed by Project Truth.

## Implemented P1 areas

### Hardcoded language QA
- Python: compileall, Ruff, mypy, pytest, runtime traceback/source parsing,
  asyncio lifecycle warnings, separate tracemalloc retained-memory probe.
- C#: restore, build, compiler/nullability/async warnings, tests, analyzer
  verification, runtime exception/source parsing, timeout/hang evidence,
  optional `dotnet-stack` and `dotnet-dump` collection before termination.
- C++: CMake sanitizer build, MSVC `/W4 /fsanitize=address`, GCC/Clang
  ASan+UBSan, CTest, clang-tidy, Cppcheck, LeakSanitizer parsing, source
  location extraction and Windows native crash dump watchdog.
- No generic language adapter was introduced; all three engines are separate.

### Automatic reproduction and evidence artifacts
- Stable SHA-256 bug fingerprint based on language/category/exception/rule/
  source/function/stack signature.
- Critical findings: 5 attempts; other reproducible runtime/test/hang findings:
  3 attempts.
- Reproduction succeeds only when the same fingerprint reappears.
- Per-attempt environment snapshot, stdout, stderr, result JSON, screen video,
  system-audio WAV and dump when available.
- QA artifact read API with path traversal protection.

### Windows evidence helpers
- `capture-agent`: DXGI Desktop Duplication + Media Foundation H.264 MP4 +
  WASAPI loopback WAV.
- `crash-watchdog`: Windows debug loop + `MiniDumpWriteDump` on second-chance
  native exception.
- `hardware-probe`: D3D11 compute calibration.
- `hardware-stress-agent`: D3D11 synthetic GPU contention.

### Hardware-aware evidence
- Local CPU single/multi, memory bandwidth, storage and optional GPU calibration.
- Relative target hardware profiles; no claim of exact CPU/GPU emulation.
- Windows Job Object CPU hard cap and process memory budget.
- Time-varying thermal CPU curve.
- Synthetic GPU, memory-bandwidth and storage contention.
- FPS/frame-time/memory evaluation and explicit simulation confidence <= 85%.
- Result JSON always includes `simulated=true` and `realDevice=false`.
- Simple 100/80/60/40 + refinement minimum-capability boundary estimate.
- Infrastructure failures stop boundary conclusions instead of being mistaken
  for target-device failures.

### Project Truth / PM integration
- QA and performance results are converted into `EvidenceRecord` entries.
- Technical findings are translated into PM-readable impact/action text while
  retaining original technical payloads.
- PM dashboard shows recent hardcoded QA, reproduction counts and hardware
  simulations beneath Project Truth rather than above it.
- Detailed QA page exposes stack/source/log/video/audio/dump artifacts.

### Persistence / API / compatibility
- Added QA run, finding, reproduction, hardware profile, calibration and
  performance simulation persistence.
- Extended existing no-migrations PostgreSQL bootstrap idempotently.
- Kept prior Dashboard Quality Gate fields for existing E2E/API compatibility.
- Added QA run/artifact and hardware calibration/profile/simulation APIs.

### Bug Zoo / verification assets
Intentional examples cover Python runtime/syntax/async/memory growth; C# compile,
null-reference, async-warning and hang; C++ compile, UAF, buffer overflow and
memory leak.

See `docs/P1_IMPLEMENTATION_STATUS.md`,
`docs/02-architecture/p1-technical-evidence.md` and
`docs/08-testing/p1-test-plan.md`.


## File delta versus P0 overlay

- New full files: **92**
- Modified full files: **15**
- Removed files: **0**
- Packaging metadata (`P1_*`) is excluded from these counts.
