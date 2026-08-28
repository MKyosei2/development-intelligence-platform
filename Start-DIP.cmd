@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-DIP.ps1" %*
set "DIP_EXIT=%ERRORLEVEL%"

if not "%DIP_EXIT%"=="0" (
    echo.
    echo DIP could not start. The diagnostic output above identifies the failed service.
    pause
)

exit /b %DIP_EXIT%
