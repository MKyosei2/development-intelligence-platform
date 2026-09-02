# DIP Native GUI + Local Intelligence patch

Target inspected revision: `5f19912d01c4ba1e840fad8730abff61f0bcd2ce`.

This archive contains only replacement/new files. Extract it at the repository root so the paths merge into the existing tree.

## What changes

- External OpenAI-compatible runtime configuration is removed from API/env/container configuration.
- `DeterministicLocalDebugAgent` replaces the external fix/debug provider. It uses deterministic evidence signatures and performs no network call.
- `EvidenceGroundedRootCauseService` becomes deterministic-only.
- A capability registry is exposed at `/api/v1/capabilities`.
- `Dip.Client` provides a browser-independent client for the DIP API.
- `Dip.Desktop` provides a Windows-native WPF capability explorer with project selection. HTTP/API failures are displayed inside the GUI instead of terminating the shell.
- `scripts/Start-DIP-Desktop.ps1` starts the existing backend when necessary, builds the desktop shell, then starts `DevelopmentIntelligencePlatform.exe`.
- The existing `Dip.Web` project remains in place as a migration/reference UI. The desktop project is kept in a separate solution so existing cross-platform CI for `DevelopmentIntelligencePlatform.sln` is not forced to build WPF.

## Files that must be deleted separately

Run the supplied `REMOVE_DIP_EXTERNAL_AI.ps1 -Apply` after extracting this archive. It deletes only the three external-AI implementation/configuration source files listed in that script.

## Recommended local verification

1. `dotnet build DevelopmentIntelligencePlatform.sln -c Release`
2. `dotnet test tests/Dip.Infrastructure.Tests/Dip.Infrastructure.Tests.csproj -c Release`
3. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-DIP-Desktop.ps1`

The patch intentionally does not push, commit, reset, clean, rebase or switch Git branches.
