# Schema

## projects
Project情報、Repository、Release Date、Health Score。

## pipeline_runs
Commit、Collector Type、Status、開始終了時刻。

## metrics
Current、Baseline、Difference、DifferencePercent、Unit。

## quality_issues
Category、Severity、Title、Evidence、Status。

重要Indexは`pipeline_run_id + name`、`project_id + severity + status`です。
