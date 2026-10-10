@echo off
setlocal
cd /d "%~dp0"
echo.
echo === MarioTrickster Save to GitHub ===
echo This creates a checkpoint commit and pushes genspark_ai_developer.
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "docs\AI_CONTINUE_PACK\scripts\save_checkpoint.ps1" -Mode Save -Push
set "CODE=%ERRORLEVEL%"
echo.
if "%CODE%"=="0" (
  echo SUCCESS: Project checkpoint is saved to GitHub.
) else (
  echo STOPPED: Nothing was pushed. Read the message above and fix only the reported issue.
)
echo.
pause
exit /b %CODE%
