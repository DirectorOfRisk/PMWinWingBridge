@echo off
setlocal
cd /d "%~dp0"
title PM-WinWing Bridge - Device Setup
echo.
echo PM-WinWing Bridge - Captain / First Officer / Observer Setup
echo ============================================================
echo.
dotnet run --project PMWinWingBridge.csproj -- --setup
echo.
pause
