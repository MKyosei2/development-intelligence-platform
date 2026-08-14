# P1 Technical Evidence Architecture

P1 strengthens Project Truth; it does not replace Project Truth with QA.

```text
Python / C# / C++ deep specialized QA adapters
  -> Finding + stable fingerprint
  -> automatic reproduction (3 or 5 attempts)
  -> screen/audio/log/dump/environment artifacts
  -> PM explanation + technical payload
  -> EvidenceRecord

Local hardware calibration
  -> relative Target Hardware Profile
  -> CPU + process memory constraint
  -> optional GPU contention + thermal curve
  -> performance evidence with explicit confidence
  -> EvidenceRecord

EvidenceRecord + Claim
  -> Project Truth
  -> PM-first dashboard
```

## Language-specific QA

The deep `PythonQaEngine`, `CSharpQaEngine`, and `CppQaEngine` implementations remain, but they now implement the shared `IQaAdapter` contract. `QaAdapterRegistry` selects them by extensible `LanguageId`; other languages use generic or manifest adapters.

### Python
- `python -m compileall`
- Ruff when installed
- mypy when installed
- pytest when installed
- runtime execution with `PYTHONFAULTHANDLER=1`, `PYTHONASYNCIODEBUG=1`, deterministic hash seed
- Python traceback/source/exception parsing
- a separate `-X tracemalloc=5` probe for retained-memory growth after a successful Python runtime run
- asyncio lifecycle warnings such as un-awaited coroutines/pending tasks

### C#
- `dotnet restore`
- `dotnet build`
- `dotnet test`
- optional `dotnet format analyzers --verify-no-changes`
- runtime exception and source-frame parsing
- nullable/async compiler warning parsing
- timeout/hang evidence; `dotnet-stack` and `dotnet-dump` are collected before termination when available

### C++
- dedicated CMake ASan/UBSan build on clang/gcc
- MSVC `/W4 /fsanitize=address /Zi` on Windows
- CTest
- clang-tidy / Cppcheck when installed
- AddressSanitizer, UndefinedBehaviorSanitizer and LeakSanitizer parsing
- GCC/Clang/MSVC source-location parsing
- Windows crash watchdog using `DEBUG_ONLY_THIS_PROCESS` + `MiniDumpWriteDump`

## Reproduction evidence

Critical findings use five attempts; other reproducible runtime/test findings use three. A retry counts as reproduction only when the generated finding fingerprint matches the original finding. A different crash is not accepted as proof.

Each attempt can contain:

```text
reproduction/<finding-id>/attempt-001/
  environment.json
  stdout.log
  stderr.log
  reproduction.mp4
  system-audio.wav
  crash.dmp
  result.json
```

Microphone recording is not automatic. System loopback audio is the default recording source.

## Capture implementation

`collectors/capture-agent` is Windows-only and uses:
- DXGI Desktop Duplication for screen frames
- Media Foundation H.264 sink writer for MP4 video
- WASAPI loopback for system audio WAV evidence

The recorder starts before the target process and finalizes after a stop-file is written.

## Hardware model

P1 intentionally does not claim to emulate arbitrary physical CPUs/GPUs. A local calibration measures CPU, memory, storage, and optional Direct3D GPU synthetic scores. A target profile is created as a percentage of that measured baseline.

On Windows, simulation can apply:
- Job Object CPU hard cap
- process memory budget
- time-varying CPU thermal curve
- optional D3D11 GPU contention helper
- synthetic memory-bandwidth contention
- synthetic storage I/O contention

The resulting confidence is capped below real-device `Verified` evidence. The result JSON always records that the run is simulated and lists limitations.

## Minimum hardware estimate

P1 provides a simple boundary search over relative local capability (100/80/60/40 percent plus refinement). The result is explicitly named an estimate and cannot be presented as a certified minimum device specification without real-device evidence.

## Security boundary

P1 QA is process execution. Therefore API requests may only select a workspace relative to `Qa:WorkspaceRoot`, and runtime/simulation executables must also remain inside that workspace. Artifact download paths are similarly constrained to the QA run's artifact root.
