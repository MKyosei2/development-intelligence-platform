# Universal QA アーキテクチャ

DIP QAは、閉じた言語enumではなく、拡張可能な`LanguageId`とadapter registryを使用する。

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

Built-in adapterは、対応toolchainが導入されている場合にPython、C#、C++、JavaScript/TypeScript、Go、Java/Kotlin、Rust、Ruby、PHP、Bash、PowerShell、Lua、Swift、Objective-C、C、SQL、GLSLを扱う。HLSLとGDScriptは自動検出するが、DIPがHLSLのentry point/profileやProject固有のGDScript実行契約を推測することはない。`dxc`/`fxc`、Godot headless check、独自toolは`.dip/qa.json`で構成する。`ManifestQaAdapter`により、DIP Domain codeを変更せず追加・独自language IDにも対応できる。

## 独自言語

`.dip/qa.json`の例:

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

Commandは実行ファイルとargument配列として起動し、manifestをshell command文字列へ連結しない。`{workspace}`、`{artifact}`、`{file}`は明示的な置換値として扱う。

## 検出

Repository detectionはsource extension、CMake、MSBuild/.NET、npm、Python packaging、Cargo、Go Modules、Maven、Gradle、Make、Mesonを認識する。Engine evidenceはUnity、Unreal Engine、Godotを認識し、framework hintでは一般的なJavaScript/Python frameworkを扱う。

## Diagnostics（診断）

SARIF、JUnit系XML、JSONを優先して正規化する。既存のcompiler/traceback parsingはtext fallbackとして維持する。そのため、すべてのadapterが既存のfingerprint、incident、reproduction、Project Truth、Release Readiness pipelineへ接続できる。
