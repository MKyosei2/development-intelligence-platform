# Generic JSON Collector

既にJSONを出力できるCI、Webサービス、独自ツールをDIPへ接続するための最小Collectorです。

```powershell
dotnet run --project collectors/generic-json-collector -- --validate report.json
```

終了コード:

- `0`: 正常
- `1`: 引数不正
- `2`: Schemaまたは業務検証エラー
- `3`: 読み込み・解析エラー
