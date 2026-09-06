# P2 実験用Fixture

このSampleは、Local検証のために意図的に異常状態を作るFixtureです。本番Codeとして使用するものではありません。

- `python/soak_memory_growth/app.py`: Memoryを保持し続け、FPSが低下するTelemetryを出力します。
- `python/fuzz_target/app.py`: 既知のByte patternに対してnon-zeroを返し、Fuzz Evidenceを検証できるようにします。

実行はRepository内のQA workspaceに限定してください。P2 execution APIは、設定されたworkspace root外のPathを意図的に拒否します。
