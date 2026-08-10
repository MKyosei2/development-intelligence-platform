@echo off
setlocal
cd /d "%~dp0"

echo Starting Development Intelligence Platform...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-Dip.ps1" %*

if errorlevel 1 (
    echo.
    echo DIP startup failed. Review the message above.
    echo Diagnostics are written under artifacts\runtime when available.
    pause
    exit /b 1
)

endlocal
