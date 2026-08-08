@echo off
setlocal
cd /d "%~dp0"
if not exist Fonts mkdir Fonts

echo.
echo Lade CDUHUB B612 Bitmapfont ...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$u='https://raw.githubusercontent.com/vradarserver/cduhub/main/library/cduhub/Resources/b612-font-21x31.json';" ^
  "$o=Join-Path (Get-Location) 'Fonts\b612-font-21x31.json';" ^
  "Invoke-WebRequest -UseBasicParsing -Uri $u -OutFile $o;" ^
  "Write-Host ('Gespeichert: ' + $o)"

if errorlevel 1 (
  echo.
  echo Download fehlgeschlagen.
  pause
  exit /b 1
)

echo.
echo Fertig.
pause
