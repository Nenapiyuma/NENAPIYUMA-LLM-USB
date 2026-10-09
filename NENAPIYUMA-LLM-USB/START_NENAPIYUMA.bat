@echo off
setlocal
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (
  py -3 app\main.py
) else (
  python app\main.py
)
if errorlevel 1 (
  echo.
  echo Could not start NENAPIYUMA. Install Python 3.10+ or see README_SI.md.
  pause
)
