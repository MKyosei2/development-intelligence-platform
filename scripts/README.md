# Repository automation scripts

## One-click local GUI

- `../Start-DIP.cmd`: 推奨。Docker Desktopの検出/起動、空きポート選択、Compose build/up、API/GUI health check、必要ならデモProject作成、ブラウザ起動まで自動化
- `Start-Dip.ps1`: `Start-DIP.cmd` のPowerShell本体
- `../Stop-DIP.cmd`: DIPを停止。Docker volumeは保持
- `Stop-Dip.ps1`: 停止処理本体

通常起動:

```powershell
.\Start-DIP.cmd
```

データを完全初期化して起動:

```powershell
.\Start-DIP.cmd -ResetData
```

ブラウザを自動で開かない:

```powershell
.\Start-DIP.cmd -NoBrowser
```

既存imageを使ってbuildを省略:

```powershell
.\Start-DIP.cmd -SkipBuild
```

GUI用デモProjectを自動作成しない:

```powershell
.\Start-DIP.cmd -SeedDemoProject:$false
```

停止:

```powershell
.\Stop-DIP.cmd
```

永続データも削除して停止:

```powershell
.\Stop-DIP.cmd -ResetData
```

起動時に5080/5081/5432/6379/8100が使用中なら、ランチャーは別の空きポートを自動選択します。実際に選んだURL/ポートは`artifacts/runtime/dip-runtime.json`に保存されます。

Docker Desktopが未起動なら自動起動し、Docker自体が未導入なら`winget`が利用可能な環境ではDocker Desktopの導入も試みます。Docker Desktop初回導入でWSL2/仮想化の有効化やWindows再起動がOSから要求された場合だけは、再起動後にもう一度`Start-DIP.cmd`を実行してください。

失敗時のCompose状態とログは`artifacts/runtime/startup-diagnostics.txt`に保存されます。

## Other scripts

- `setup-development.ps1`: 必要ToolとPython仮想環境を準備
- `verify-environment.ps1`: Toolと必須Pathを検証
- `build-all.ps1`: C#、Python、C++をまとめてBuild
- `test-all.ps1`: .NET、Python、C++、任意でk6を実行
- `format-all.ps1`: `dotnet format`と`ruff`
- `generate-sample-data.ps1`: SampleとSHA-256 Manifest生成
- `package-release.ps1`: Release成果物をZIP化
- `validate-schemas.ps1`: Sample ReportをJSON Schema検証
- `Verify-Solution.ps1`: P0 MVPの最終検証

Repository Rootから実行し、生成物は`artifacts/`または`reports/`へ出します。
