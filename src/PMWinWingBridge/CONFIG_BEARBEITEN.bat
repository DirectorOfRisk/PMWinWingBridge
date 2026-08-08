@echo off
cd /d "%~dp0"
if not exist bridge-config.json (
  echo bridge-config.json was not found.
  pause
  exit /b 1
)
start "" notepad.exe bridge-config.json
