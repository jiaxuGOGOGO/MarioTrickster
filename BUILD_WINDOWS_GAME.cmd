@echo off
setlocal
cd /d "%~dp0"
echo.
echo === MarioTrickster: build the Windows game (S254) ===
echo World used: Assets\Resources\WorldPark\MyWorld.worldpark.json if it exists,
echo             otherwise the main world Borderland.worldpark.json.
echo Output:     Builds\Windows\^<world id^>\MarioTrickster.exe  (log: Builds\build-windows.log)
echo.
echo Unity must be CLOSED for this project (or use the "Build Windows" button inside Unity),
echo and the Unity license must be activated (Unity Hub - Preferences - Licenses - Add - Personal).
echo.
set "UNITY=%UNITY_EXE%"
if "%UNITY%"=="" set "UNITY=C:\Program Files\Unity\Hub\Editor\2022.3.61f1\Editor\Unity.exe"
if not exist "%UNITY%" (
  echo STOPPED: Unity.exe not found at "%UNITY%".
  echo Set UNITY_EXE to your Unity 2022.3.61f1 Editor\Unity.exe and run again.
  pause
  exit /b 2
)
if exist "Temp\UnityLockfile" (
  del "Temp\UnityLockfile" >nul 2>&1
  if exist "Temp\UnityLockfile" (
    echo STOPPED: this project is open in Unity right now.
    echo Use MarioTrickster - World Workshop - Build Windows inside Unity, or close Unity and run again.
    pause
    exit /b 3
  )
)
if not exist "Builds" mkdir "Builds"
echo Building... the first build can take several minutes.
"%UNITY%" -batchmode -quit -projectPath "%CD%" -buildTarget Win64 -executeMethod WorldParkBuilder.BuildWindowsBatch -logFile "Builds\build-windows.log" %*
set "CODE=%ERRORLEVEL%"
echo.
findstr /C:"[WorldPark]" "Builds\build-windows.log"
echo.
if "%CODE%"=="0" (
  echo DONE. Opening Builds\Windows ...
  start "" explorer "%CD%\Builds\Windows"
) else (
  echo FAILED with code %CODE%. Read Builds\build-windows.log ^(search for "error" or "[WorldPark]"^).
)
pause
exit /b %CODE%
