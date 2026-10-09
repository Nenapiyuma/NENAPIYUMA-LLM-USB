@echo off
setlocal
cd /d "%~dp0"
if exist "%~dp0NENAPIYUMA.exe" (
  start "" "%~dp0NENAPIYUMA.exe"
  exit /b 0
)
where py >nul 2>nul
if %errorlevel%==0 (
  py -3 "%~dp0app\main.py"
) else (
  where python >nul 2>nul
  if %errorlevel%==0 (
    python "%~dp0app\main.py"
  ) else (
    echo NENAPIYUMA.exe was not found and Python is not installed.
    echo Download the complete NENAPIYUMA USB Bundle from GitHub Actions.
    pause
    exit /b 1
  )
)
if errorlevel 1 (
  echo.
  echo Could not start NENAPIYUMA. See README_SI.md.
  pause
)
