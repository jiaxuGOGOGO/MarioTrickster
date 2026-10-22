@echo off
setlocal
cd /d "%~dp0"
echo.
echo === MarioTrickster Unity Launch ===
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "docs\AI_CONTINUE_PACK\scripts\bootstrap_windows.ps1"
set "CODE=%ERRORLEVEL%"
if not "%CODE%"=="0" (
  echo.
  echo STOPPED: Fix the missing requirement shown above before opening Unity.
  pause
  exit /b %CODE%
)
start "MarioTrickster Unity" "C:\Program Files\Unity\Hub\Editor\2022.3.61f1\Editor\Unity.exe" -projectPath "%CD%"
echo Unity is starting for this Git working copy.
timeout /t 3 >nul
exit /b 0
