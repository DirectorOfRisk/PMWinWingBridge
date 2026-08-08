@echo off
setlocal
cd /d "%~dp0"
title PM-WinWing Bridge Multi-CDU v1.0.0
echo.
echo PM-WinWing Bridge Multi-CDU v1.0.0
echo ====================================
echo.
dotnet run --project PMWinWingBridge.csproj
echo.
echo Bridge beendet.
pause
