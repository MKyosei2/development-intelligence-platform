@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Stop-DIP.ps1" %*
set "DIP_EXIT=%ERRORLEVEL%"

if not "%DIP_EXIT%"=="0" pause
exit /b %DIP_EXIT%
