# DIP JSON Schema（スキーマ）

Collector、API、Test、Sample Dataの境界を固定します。

- `collector-report.schema.json`: 全Collector共通Report
- `pipeline-run.schema.json`: PipelineRun外部表現
- `metric.schema.json`: Metric外部表現
- `quality-issue.schema.json`: QualityIssue外部表現
- `quality-rules.schema.json`: Quality Rule設定

既存Fieldの意味変更は禁止し、破壊的変更ではSchema Major Versionを上げます。
