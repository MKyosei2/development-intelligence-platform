# P0 統合テストケース

1. Projectを作成する
2. Collector ReportをPOSTする
3. `201 Created` を確認する
4. PipelineRun / Metric / QualityIssueが保存されることを確認する
5. 同じExternalRunIdとAttemptNumberで再送する
6. `200 OK` を確認する
7. PipelineRun件数が増えないことを確認する
8. 同一FingerprintのIssueが統合されることを確認する
