# DIP Unreal Engine Collector

Unreal Project向けのdeterministicかつdependency-freeなCollectorです。既存のDIP `CollectorReport` Schemaを出力するため、Unreal専用のIngestion APIは必要ありません。

## 収集するEvidence

主に次の情報を収集します。

- `.uproject` metadata
- Asset / Map / Source / Config / Plugin数
- `Saved/Logs` 内のError / Warning
- 任意のUnreal Insights CSVから得られるframe time、GameThread、RenderThread、GPU、Memory columnの平均値

## 実行例

```bash
python collectors/unreal-collector/collector.py --root ./MyGame --project-id 00000000-0000-0000-0000-000000000001 --repository https://github.com/acme/game --commit-sha 0123456789abcdef --branch main --external-run-id unreal-ci-123 --output artifacts/unreal-report.json
```

生成されたJSONは、既存の `POST /api/v1/collectors/reports` Endpointへ送信します。

`.uproject` descriptorが存在しない場合、CollectorはPASSを推測しません。その状態をCriticalとして記録し、Report statusをFailedにする **fail-closed** 動作を採用しています。

追加Evidenceとして、任意の `--automation-report <index.json>`（または `Saved/Automation/Reports/**/index.json` の自動検出）、`Saved/Crashes` Artifact数、`--insights-csv` Runtime Metricを扱えます。

## Engine-native Automationモード

より深いUnreal検証を行う場合は、`--unreal-editor-cmd <path-to-UnrealEditor-Cmd>` を指定し、必要に応じて `--automation-filter <filter>` も追加します。

CollectorはUnrealをunattended modeで起動し、Engineの終了状態・結果Metadataを記録し、生成されたAutomation `index.json` を読み込みます。Engine commandまたはReportを利用できない場合はfail-closedで失敗扱いにします。

Unreal installationを意図的に用意しない環境では、dependency-freeのFile / Log収集Pathを引き続き利用できます。
