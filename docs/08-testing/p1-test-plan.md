# P1 Verification Plan（P1検証計画）

P1はsource fileが存在するだけではcompleteではなく、vertical evidence pathまで検証できた場合にcompleteとする。

## 必須Automated Check
1. `DevelopmentIntelligencePlatform.sln`をReleaseでbuild。
2. Domain、Application、Infrastructure、Integration testを実行。
3. API/Worker/PostgreSQLを起動してE2E testを実行。
4. Python Bug Zoo runtime errorが検出され、自動reproductionされることを確認。
5. Windowsでcapture-agent、crash-watchdog、hardware-probe、hardware-stress-agentをbuild。
6. Interactive Windows環境でcapture smokeを実行し、MP4/WAV artifactがfinalizeされることを確認。
7. Hardware calibrationからrelative target profileを作成でき、simulated resultがnon-real-device Evidenceとして明示されることを確認。

## Bug Zoo（異常系テスト群）
`samples/qa-bug-zoo`には、`expected-result.json` contract付きの意図的に壊したPython、C#、C++ sampleを含む。

## Evidence Rule（証拠ルール）
- Reproduction成功とみなすのは、fingerprintが元findingと一致した場合だけ。
- Simulationがpassしても、それだけでconfidenceを`Verified`へ昇格させない。
- Optional analyzer欠損はinfrastructure/tool availability情報であり、code defectではない。
- Microphone captureを自動有効化しない。
