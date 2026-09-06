# k6 負荷テスト

## インストール

```powershell
winget install --id Grafana.k6 -e
```

## 実行

```powershell
$env:DIP_API_URL = "http://localhost:7001"
$env:DIP_PROJECT_ID = "replace-with-project-id"

k6 run tests/load/k6/dashboard-load.js
k6 run tests/load/k6/webhook-load.js
```
