@echo off
setlocal
cd /d "%~dp0"
echo.
echo === MarioTrickster: run Unity native EditMode tests (S254) ===
echo Unity must be CLOSED for this project and the license activated.
echo Results: Builds\UnityTests\editmode-results.xml   Log: Builds\UnityTests\editmode.log
echo.
set "UNITY=%UNITY_EXE%"
if "%UNITY%"=="" set "UNITY=C:\Program Files\Unity\Hub\Editor\2022.3.61f1\Editor\Unity.exe"
if not exist "%UNITY%" (
  echo STOPPED: Unity.exe not found at "%UNITY%". Set UNITY_EXE and run again.
  pause
  exit /b 2
)
if exist "Temp\UnityLockfile" (
  del "Temp\UnityLockfile" >nul 2>&1
  if exist "Temp\UnityLockfile" (
    echo STOPPED: this project is open in Unity. Use MarioTrickster - Tests - Test Hub inside Unity, or close Unity.
    pause
    exit /b 3
  )
)
if not exist "Builds\UnityTests" mkdir "Builds\UnityTests"
echo Running... this can take a few minutes.
"%UNITY%" -batchmode -projectPath "%CD%" -runTests -testPlatform EditMode -testResults "%CD%\Builds\UnityTests\editmode-results.xml" -logFile "Builds\UnityTests\editmode.log"
set "CODE=%ERRORLEVEL%"
echo.
if exist "Builds\UnityTests\editmode-results.xml" (
  findstr /R /C:"<test-run " "Builds\UnityTests\editmode-results.xml"
)
echo.
if "%CODE%"=="0" (echo ALL EDITMODE TESTS PASSED.) else (echo Exit code %CODE%: some tests failed or Unity could not start. Read the log above.)
pause
exit /b %CODE%
