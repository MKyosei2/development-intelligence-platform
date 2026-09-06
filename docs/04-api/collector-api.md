# Collector API（Collector向けAPI）

`POST /api/collectors/reports`

必須項目はschemaVersion、collectorType、collectorVersion、projectId、commitSha、branch、startedAt、finishedAt、status、metrics、issuesです。

同一Delivery IDは重複登録しません。
