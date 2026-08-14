# Universal QA Architecture

DIP QA now uses an extensible `LanguageId` and adapter registry instead of a closed language enum.

```text
Repository
  -> RepositoryTechnologyDetector
  -> QaTechnologyProfile (languages / build systems / frameworks / engines)
  -> QaAdapterRegistry
  -> IQaAdapter (built-in or .dip/qa.json manifest)
  -> ProcessExecutor (no shell)
  -> UniversalDiagnosticNormalizer (SARIF / JSON / JUnit / text fallback)
  -> QaFinding / fingerprint / incident / reproduction / Project Truth
```

Built-in adapters cover Python, C#, C++, JavaScript/TypeScript, Go, Java/Kotlin, Rust, Ruby, PHP, Bash, PowerShell, Lua, Swift, Objective-C, C, SQL and GLSL when their toolchains are installed. HLSL and GDScript are auto-detected, but DIP does not guess an HLSL entry point/profile or a project-specific GDScript execution contract; configure `dxc`/`fxc`, Godot headless checks, or proprietary tools in `.dip/qa.json`. `ManifestQaAdapter` also supports additional or proprietary language IDs without changing DIP Domain code.

## Custom language

`.dip/qa.json` example:

```json
{
  "languages": [{
    "id": "fortran",
    "extensions": [".f90"],
    "commands": [{
      "name": "gfortran-syntax",
      "executable": "gfortran",
      "arguments": ["-fsyntax-only", "{file}"],
      "perFile": true,
      "category": "compile",
      "severity": "critical",
      "reports": ["reports/*.sarif"]
    }]
  }]
}
```

Commands are executed as an executable plus an argument array; the manifest is never concatenated into a shell command. `{workspace}`, `{artifact}` and `{file}` are explicit substitutions.

## Detection

Repository detection recognizes source extensions, CMake, MSBuild/.NET, npm, Python packaging, Cargo, Go Modules, Maven, Gradle, Make and Meson. Engine evidence recognizes Unity, Unreal Engine and Godot. Framework hints include common JavaScript and Python frameworks.

## Diagnostics

SARIF, JUnit-like XML and JSON are normalized first. Existing compiler/traceback parsing remains as a textual fallback. All adapters therefore feed the existing fingerprint, incident, reproduction, Project Truth and Release Readiness pipelines.
