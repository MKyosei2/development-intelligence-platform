# P1 Technical Evidence アーキテクチャ

P1はProject Truthを強化するものであり、Project TruthをQAへ置き換えるものではない。

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

## 言語固有QA

深い解析を行う`PythonQaEngine`、`CSharpQaEngine`、`CppQaEngine`は維持しつつ、共通の`IQaAdapter` contractを実装する。`QaAdapterRegistry`は拡張可能な`LanguageId`から選択し、その他の言語ではgenericまたはmanifest adapterを使用する。

### Python
- `python -m compileall`
- Ruff（導入済みの場合）
- mypy（導入済みの場合）
- pytest（導入済みの場合）
- `PYTHONFAULTHANDLER=1`、`PYTHONASYNCIODEBUG=1`、deterministic hash seedを用いたruntime execution
- Python traceback/source/exception解析
- 正常なPython runtime run後に保持memory増加を調べる別の`-X tracemalloc=5` probe
- un-awaited coroutine/pending taskなどasyncio lifecycle warning

### C#
- `dotnet restore`
- `dotnet build`
- `dotnet test`
- 任意の`dotnet format analyzers --verify-no-changes`
- runtime exceptionとsource-frame解析
- nullable/async compiler warning解析
- timeout/hang Evidence。利用可能であれば終了前に`dotnet-stack`と`dotnet-dump`を収集

### C++
- clang/gccでの専用CMake ASan/UBSan build
- WindowsでのMSVC `/W4 /fsanitize=address /Zi`
- CTest
- clang-tidy / Cppcheck（導入済みの場合）
- AddressSanitizer、UndefinedBehaviorSanitizer、LeakSanitizer解析
- GCC/Clang/MSVC source-location解析
- `DEBUG_ONLY_THIS_PROCESS` + `MiniDumpWriteDump`を用いたWindows crash watchdog

## 再現Evidence

Critical findingは5回、それ以外の再現可能なruntime/test findingは3回試行する。Retryを再現成功として数えるのは、生成されたfinding fingerprintが元のfindingと一致した場合だけである。別のcrashは証拠として認めない。

各attemptには次のようなartifactを含められる。

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

Microphone録音は自動実行しない。既定の録音元はsystem loopback audioである。

## Capture実装

`collectors/capture-agent`はWindows専用で、次を使用する。
- DXGI Desktop Duplicationによるscreen frame取得
- Media Foundation H.264 sink writerによるMP4 video
- WASAPI loopbackによるsystem audio WAV Evidence

Recorderは対象processより前に開始し、stop-fileが書き込まれた後にfinalizeする。

## Hardware Model（ハードウェアモデル）

P1は任意の物理CPU/GPUを完全に再現できるとは主張しない。Local calibrationでCPU、memory、storage、任意のDirect3D GPU synthetic scoreを計測し、その実測baselineに対する割合としてtarget profileを作成する。

Windowsでは、次のsimulationを適用できる。
- Job Object CPU hard cap
- process memory budget
- 時間変化するCPU thermal curve
- 任意のD3D11 GPU contention helper
- synthetic memory-bandwidth contention
- synthetic storage I/O contention

得られるconfidenceは実機`Verified` Evidenceより低く制限する。Result JSONにはsimulation runであることと制約事項を常に記録する。

## 最小Hardware推定

P1はlocal capability比率（100/80/60/40%と追加refinement）に対する単純な境界探索を提供する。結果は明示的にestimateとして扱い、実機Evidenceなしに認証済みminimum device specificationとして提示してはならない。

## セキュリティ境界

P1 QAはprocess実行を伴う。そのためAPI requestで選択できるworkspaceは`Qa:WorkspaceRoot`からの相対pathに限定し、runtime/simulation executableもそのworkspace内に留める。Artifact download pathもQA runのartifact root内へ制限する。
