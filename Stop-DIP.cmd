@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Stop-Dip.ps1" %*

if errorlevel 1 (
    echo.
    echo DIP shutdown failed.
    pause
    exit /b 1
)

endlocal
