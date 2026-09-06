# Local開発

DIPはNative-firstなlocal runtimeを使用する。.NET 8、Python 3.12+、PostgreSQL、PostgreSQL client toolをinstallする。`dip` database/userを一度作成し、`POSTGRES_*` variable（`.env.example`参照）を設定して次を実行する。

```powershell
.\scripts\Start-DIP.ps1
```

LauncherはPython venvを作成し、Analysis/API/Worker/Webをhost上で直接起動する。PID/log stateは`.dip/runtime`に保存する。停止は`scripts/Stop-DIP.ps1 -KeepData`。Dockerとexternal queue daemonは不要。
