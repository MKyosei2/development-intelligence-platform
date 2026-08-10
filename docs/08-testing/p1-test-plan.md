# P1 Verification Plan

P1 is complete only when the vertical evidence path is verified, not merely when source files exist.

## Required automated checks
1. Build `DevelopmentIntelligencePlatform.sln` in Release.
2. Run Domain, Application, Infrastructure and Integration tests.
3. Run E2E tests with API/Worker/PostgreSQL/Redis active.
4. Verify the Python Bug Zoo runtime error is detected and automatically reproduced.
5. On Windows, build capture-agent, crash-watchdog, hardware-probe and hardware-stress-agent.
6. On an interactive Windows machine, run a capture smoke and verify MP4/WAV artifact finalization.
7. Verify a hardware calibration can create a relative target profile and that simulated results are marked as non-real-device evidence.

## Bug Zoo
`samples/qa-bug-zoo` contains intentionally broken Python, C# and C++ examples with `expected-result.json` contracts.

## Evidence rules
- A reproduction is successful only when its fingerprint matches the original finding.
- Simulation confidence must never be promoted to `Verified` only because it passed.
- Missing optional analyzers are infrastructure/tool-availability information, not a code defect.
- Microphone capture must not be automatically enabled.
