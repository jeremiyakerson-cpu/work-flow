@echo off
REM One-time setup: installs everything this tool needs. Double-click to run.
cd /d "%~dp0"
where py >nul 2>nul
if errorlevel 1 (
  echo Python is not installed. Get it from https://www.python.org/downloads/
  echo During install, tick "Add python.exe to PATH". Then run this file again.
  pause
  exit /b 1
)
if not exist .venv py -3 -m venv .venv
call .venv\Scripts\activate.bat
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
python -m playwright install chromium
if not exist config.yaml copy config.example.yaml config.yaml >nul
echo.
echo Setup done. Next: open config.yaml in Notepad and paste your Pinterest app ID and secret.
echo Then double-click "Pinterest Organizer.bat".
pause
