@echo off
setlocal
set "ROOT=C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\tools\of_goalmetric_safe_release_20261106.ps1" > "%ROOT%\docs\OPENFILL_GOALMETRIC_RELEASE_LAST.txt" 2>&1
exit /b %ERRORLEVEL%
