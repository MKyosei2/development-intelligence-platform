# Formal Runtime Verification

Formal evidence for a revision is generated only by a fail-closed run on the target Windows machine:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-PortfolioFormal.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe"
```

The runner requires a clean Git working tree, Windows, .NET, Docker, a real Unity Editor, full solution verification, Universal QA Bug Zoo, Project Control/P2 checks and portfolio benchmarks. It records the exact source commit. Old P0/Unity/benchmark evidence is never promoted to a new revision.
