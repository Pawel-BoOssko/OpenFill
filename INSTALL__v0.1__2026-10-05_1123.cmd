@echo off
rem OpenFill - install / update. Metadata: wersja 0.1, data 2026-10-05 11:23
rem Double-click: installs missing components, builds the app, creates shortcuts and starts OpenFill.
rem The install script has its version in its name, so we look it up by pattern (the newest one wins).
set "OF_SCRIPT="
for /f "delims=" %%f in ('dir /b /o:n "%~dp0tools\install*.ps1" 2^>nul') do set "OF_SCRIPT=%~dp0tools\%%f"
if not defined OF_SCRIPT (
  echo tools\install*.ps1 not found
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%OF_SCRIPT%" %*
if errorlevel 1 pause
