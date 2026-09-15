# DIP Unity Collector

Add this directory as an embedded/local Unity package. The package emits asset
imports and build results through the local DIP named pipe. If Unity Test
Framework is present and `UNITY_INCLUDE_TESTS` is defined, aggregate test
results are also emitted.

`DIP > Capture Screenshot` creates a PNG under `DIPCaptures/` and registers it
as screenshot evidence. `DipMediaCollector.RegisterVideo(path)` is the
extension point for Unity Recorder or a studio capture pipeline.

The package uses only the local named pipe; it contains no server credential.
