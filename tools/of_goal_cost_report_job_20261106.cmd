@echo off
setlocal
set "PY=C:\Users\pboru\AppData\Local\Programs\Python\Python312\python.exe"
set "ROOT=C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill"
"%PY%" -X utf8 "%ROOT%\tools\of_goal_cost_report_20261106.py" > "%ROOT%\docs\OPENFILL_GOAL_COST_JOB_LAST.txt" 2>&1
exit /b %ERRORLEVEL%
