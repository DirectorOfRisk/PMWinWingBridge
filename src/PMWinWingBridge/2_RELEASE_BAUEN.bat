@echo off
setlocal
cd /d "%~dp0"
title PMWinWingBridge v1.0.0 - Build Release

echo.
echo PMWinWingBridge v1.0.0
echo ======================
echo Building Windows x64 release...
echo.

if exist publish rmdir /s /q publish

dotnet publish PMWinWingBridge.csproj -c Release -r win-x64 --self-contained false -o publish
if errorlevel 1 goto :error

copy /y bridge-config.json publish\bridge-config.json >nul
copy /y CONFIG_BEARBEITEN.bat publish\CONFIG_BEARBEITEN.bat >nul
copy /y SETUP_DEVICES.bat publish\SETUP_DEVICES.bat >nul
copy /y FONT_HERUNTERLADEN.bat publish\FONT_HERUNTERLADEN.bat >nul

if exist Fonts xcopy /e /i /y Fonts publish\Fonts >nul

copy /y "..\..\README.md" publish\README.md >nul
copy /y "..\..\LICENSE" publish\LICENSE >nul
copy /y "..\..\CHANGELOG.md" publish\CHANGELOG.md >nul
copy /y "..\..\THIRD_PARTY_NOTICES.md" publish\THIRD_PARTY_NOTICES.md >nul
if not exist publish\licenses mkdir publish\licenses
xcopy /e /i /y "..\..\licenses" publish\licenses >nul

echo.
echo RELEASE BUILD COMPLETE
echo.
echo Output:
echo   %CD%\publish
echo.
echo Main executable:
echo   publish\PMWinWingBridge.exe
echo.
pause
exit /b 0

:error
echo.
echo RELEASE BUILD FAILED.
echo.
echo Make sure the .NET 8 SDK is installed and NuGet packages can be restored.
echo.
pause
exit /b 1
