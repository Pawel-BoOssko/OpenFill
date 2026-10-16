@echo off
rem OpenFill - tester package launcher. Metadata: wersja 0.1, data 2026-10-16 09:25
rem Starts OpenFill and shows the MCP address to paste into Claude or ChatGPT (also copied to the clipboard).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0app\start-tester.ps1"
pause
