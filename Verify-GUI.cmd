@echo off
setlocal
cd /d "%~dp0"
echo Running isolated GUI Phase 1 + Playwright verification...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Verify-GuiPhase1.ps1" %*
if errorlevel 1 (
    echo.
    echo GUI Phase 1 verification failed. See artifacts\gui-phase1-e2e\compose.log and Playwright artifacts.
    pause
    exit /b 1
)
echo.
echo GUI Phase 1 verification passed.
pause
endlocal
