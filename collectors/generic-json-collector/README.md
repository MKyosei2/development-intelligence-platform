# Generic JSON Collector

すでにJSONを出力できるCI、Web Service、独自ToolをDIPへ接続するための最小Collectorです。

```powershell
dotnet run --project collectors/generic-json-collector -- --validate report.json
```

## 終了コード

- `0`: 正常
- `1`: 引数不正
- `2`: Schemaまたは業務検証Error
- `3`: 読み込み・解析Error
