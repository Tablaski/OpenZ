@echo off
setlocal
title OpenZ - Release build
pushd "%~dp0"
echo.
echo  OpenZ - Windows release build
echo  Close the Unity editor for this project before continuing.
echo  Keep this window open until the build finishes.
echo  Run this launcher only once; a second window cannot build the same project.
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build-Duel.ps1" -OutputDirectory "%~dp0builds\OpenZ-Release"
if errorlevel 1 goto failed
echo.
echo  SUCCESS! Your game is in builds\OpenZ-Release\OpenZ-Duel.exe
echo  To share the game, copy the ENTIRE OpenZ-Release folder.
explorer.exe "%~dp0builds\OpenZ-Release"
goto done
:failed
echo.
echo  BUILD FAILED. Read the error above and this log:
echo  tests\Multiplayer\.cli\Unity-build-OpenZ-Release.log
:done
echo.
pause
popd
endlocal
