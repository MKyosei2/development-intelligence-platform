# Universal QA S-priority changeset — 2026-08-14

## Target

This source package integrates the requested S-priority QA work into the existing Development Intelligence Platform tree. The GitHub `main` revision observed while preparing the package was `6fdec48480daf0fe9f9aff6ad8942ee6d2365dca`. Because the connected private repository cannot be cloned into this execution container, the working tree was reconstructed from the most recent full repository ZIP available in the user's Library and the relevant post-ZIP `main` fixes were synchronized from GitHub before applying this changeset. This package must therefore be verified after it is placed in the user's real Git checkout; it is not presented as a byte-for-byte Git archive of that SHA.

## Implemented

| Requested S item | Source state |
|---|---|
| Universal QA Adapter architecture | Implemented: `IQaAdapter`, registry, deep adapters, generic adapter, manifest adapter, Unity-aware adapter |
| `QaLanguage enum` -> `LanguageId` | Implemented with legacy DB/JSON aliases and arbitrary string IDs |
| Build / Framework / Game Engine detection | Implemented for major build systems plus Unity / Unreal / Godot and common frameworks |
| SARIF / JSON / JUnit diagnostic normalization | Implemented; also handles NUnit-style Unity XML and TRX failures; text regex is fallback |
| Multi-language Bug Zoo | Expanded beyond Python/C#/C++ to JavaScript, Go, Java, Ruby, PHP, Bash and a custom manifest-only language |
| Windows / .NET / Unity formal runtime verification | Implemented as fail-closed exact-revision verification scripts; no PASS is generated without the required real environment |
| Benchmark placeholders -> measured workflow | Implemented benchmark runner for project-list p95, collector p95, 100 req/s error rate and 1M metric-query p95; documentation is only populated with values produced by the exact revision |

## Adapter behavior

- Python/C#/C++ keep specialized deep checks.
- Unity C# is routed to a Unity Editor BatchMode/EditMode adapter instead of assuming `dotnet` is authoritative.
- CMake C++ uses the sanitizer/deep adapter. Non-CMake C++ falls back to MSBuild/cppcheck/compile-database-aware checks when available.
- New or proprietary languages are declared in `.dip/qa.json` using executable + argument arrays; DIP does not build a shell command string.
- HLSL/GDScript are detected but require an explicit manifest command where entry points/profiles/project contracts cannot be inferred safely.

## Verification contract

Run on the actual Windows Git checkout:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-UniversalQa.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-PortfolioFormal.ps1 -UnityPath "C:\path\to\Unity.exe"
powershell -ExecutionPolicy Bypass -File .\scripts\Run-PortfolioBenchmarks.ps1 -UpdateDocumentation
```

`Verify-PortfolioFormal.ps1` requires a clean Git tree and binds the evidence to `git rev-parse HEAD`. It requires Windows, .NET, Docker and a real Unity Editor. The benchmark JSON also stores the exact source commit.

## Generation-environment limitation

The package-generation environment does not contain the .NET SDK, PowerShell or Unity Editor. Consequently this package does **not** claim that the modified C# solution, Windows capture paths, Unity runtime verification or exact-revision portfolio benchmarks passed here. See `artifacts/generation-static-validation/STATIC_VALIDATION_REPORT.md` for checks that were actually executed.
