@echo off
setlocal

cd /d "%~dp0"

powershell.exe ^
  -NoProfile ^
  -ExecutionPolicy Bypass ^
  -File ".\scripts\Verify-All-Local.ps1" ^
  -Mode Full

set RESULT=%ERRORLEVEL%

echo.
if "%RESULT%"=="0" (
    echo FULL LOCAL CI PASSED
) else (
    echo FULL LOCAL CI FAILED
)

echo.
pause

exit /b %RESULT%