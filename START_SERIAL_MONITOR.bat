@echo off
setlocal
title PMWinWingBridge Serial Monitor
cd /d "%~dp0"
echo PMWinWingBridge - Serial Monitor
echo =================================
echo.
dotnet run --project "src\PMWinWingBridge.SerialMonitor\PMWinWingBridge.SerialMonitor.csproj"
echo.
if errorlevel 1 (
  echo Das Programm wurde mit einem Fehler beendet.
)
pause
