@echo off
setlocal
cd /d "%~dp0"
echo.
echo === MarioTrickster Checkpoint Status ===
echo This checks Git, pending changes, Unity meta files, and the latest test report.
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "docs\AI_CONTINUE_PACK\scripts\save_checkpoint.ps1" -Mode Status
set "CODE=%ERRORLEVEL%"
echo.
pause
exit /b %CODE%
