# DIP Unreal Engine Collector

Deterministic, dependency-free collector for Unreal projects. It emits the existing DIP `CollectorReport` schema so no Unreal-specific ingestion API is required.

Collected evidence includes `.uproject` metadata, assets/maps/source/config/plugin counts, Saved/Logs errors and warnings, and optional Unreal Insights CSV averages for frame time, GameThread, RenderThread, GPU and memory columns.

Example:

```bash
python collectors/unreal-collector/collector.py --root ./MyGame --project-id 00000000-0000-0000-0000-000000000001 --repository https://github.com/acme/game --commit-sha 0123456789abcdef --branch main --external-run-id unreal-ci-123 --output artifacts/unreal-report.json
```

The produced JSON is submitted to the existing `POST /api/v1/collectors/reports` endpoint. The collector never guesses a PASS when the `.uproject` descriptor is missing; that condition is emitted as Critical and the report status is Failed.

Additional evidence: optional `--automation-report <index.json>` (or automatic `Saved/Automation/Reports/**/index.json` discovery), Saved/Crashes artifact counting, and `--insights-csv` runtime metrics.
