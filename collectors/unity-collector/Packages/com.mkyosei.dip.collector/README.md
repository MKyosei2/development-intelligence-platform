# DIP Unity Collector

Unity 2022.3+ projectsを対象に、DIPへ送る品質Evidenceを生成するUPM Collectorです。v0.2ではAsset QAに加えて、Build Settings、Scene/Reference、Animation、Runtime Performanceを同一Reportへ統合します。

## 収集する主なEvidence

- Texture / Mesh / Material: サイズ、三角形数、Material過多
- Build: enabled scene、missing/duplicate scene、Development Build状態
- Scene: GameObject数、disabled object、Missing Script、Missing serialized reference
- Animation: clip/curve/event数、zero-length clip、curve過多、duplicate event
- Runtime: avg/p50/p95/p99/max frame time、Mono/Total memory、GC collection回数

## BatchMode

```powershell
Unity.exe -batchmode -quit `
  -projectPath C:\Project `
  -executeMethod Dip.UnityCollector.Editor.Cli.UnityCollectorCli.Run `
  -dipProjectId 00000000-0000-0000-0000-000000000001 `
  -dipCommitSha abcdef1234567890 `
  -dipBranch develop `
  -dipOutput C:\Reports\unity-report.json
```

RuntimeのPerformanceProbeが生成したJSONも取り込む場合は `-dipRuntimeMetrics C:\Reports\dip-runtime-performance.json` を追加します。明示的に指定したRuntime evidenceが存在しない場合はWarningとして記録します。

## Runtime PerformanceProbe

`Dip.UnityCollector.Runtime.PerformanceProbe` をQA用SceneのGameObjectへ付与すると、warmup後のframe time / memory / GC統計を終了時にJSONへ保存します。`-dipRuntimeOutput` で出力先を指定できます。

## Tests

PackageにはEditor/RuntimeテストAssemblyを含みます。`scripts/Verify-UnityCollector.ps1` は一時Unity Projectを作成してUPM packageをfile参照し、EditMode testsとCollector BatchMode smokeを実行します。
