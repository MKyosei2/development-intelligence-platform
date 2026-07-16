# DIP Unity Collector

Unity Project内のTexture、Mesh、Material、Missing Componentを解析し、DIP共通Reportを出力します。

BatchMode例:

```powershell
Unity.exe -batchmode -quit `
  -projectPath C:\Project `
  -executeMethod Dip.UnityCollector.Editor.Cli.UnityCollectorCli.Run `
  -dipProjectId 00000000-0000-0000-0000-000000000001 `
  -dipCommitSha abcdef1234567890 `
  -dipBranch develop `
  -dipOutput C:\Reports\unity-report.json
```
