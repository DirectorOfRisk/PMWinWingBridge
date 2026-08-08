@echo off
setlocal
cd /d "%~dp0"
if not exist publish\PMWinWingBridge.exe (
  echo The release has not been built yet.
  echo Run 2_RELEASE_BAUEN.bat first.
  pause
  exit /b 1
)
cd publish
PMWinWingBridge.exe
