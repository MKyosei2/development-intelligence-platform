# Repository automation scripts

- `setup-development.ps1`: 必要ToolとPython仮想環境を準備
- `verify-environment.ps1`: Toolと必須Pathを検証
- `build-all.ps1`: C#、Python、C++をまとめてBuild
- `test-all.ps1`: .NET、Python、C++、任意でk6を実行
- `format-all.ps1`: `dotnet format`と`ruff`
- `generate-sample-data.ps1`: SampleとSHA-256 Manifest生成
- `package-release.ps1`: Release成果物をZIP化
- `validate-schemas.ps1`: Sample ReportをJSON Schema検証

Repository Rootから実行し、生成物は`artifacts/`または`reports/`へ出します。
