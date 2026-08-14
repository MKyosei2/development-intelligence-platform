@echo off
setlocal
cd /d "%~dp0"
echo Running isolated GUI runtime verification...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Verify-GuiRuntime.ps1" %*
if errorlevel 1 (
    echo.
    echo GUI runtime verification failed. See artifacts\gui-runtime-verification\compose.log
    pause
    exit /b 1
)
echo.
echo GUI runtime verification passed.
pause
endlocal
