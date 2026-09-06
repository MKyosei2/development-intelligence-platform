# Error Format（エラー形式）

API errorは次のJSON形式で返します。

```json
{
  "type": "validation_error",
  "title": "Invalid collector report",
  "status": 400,
  "traceId": "00-...",
  "errors": {}
}
```
