@echo off
setlocal

cd /d "%~dp0"

powershell.exe ^
  -NoProfile ^
  -ExecutionPolicy Bypass ^
  -File ".\scripts\Verify-All-Local.ps1" ^
  -Mode Quick

set RESULT=%ERRORLEVEL%

echo.
if "%RESULT%"=="0" (
    echo QUICK LOCAL CI PASSED
) else (
    echo QUICK LOCAL CI FAILED
)

echo.
pause

exit /b %RESULT%