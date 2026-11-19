@echo off
cd /d "%~dp0"
if not exist .venv (
  echo Run setup.bat first.
  pause
  exit /b 1
)
call .venv\Scripts\activate.bat
:menu
cls
echo  ===============================
echo     Pinterest Organizer
echo  ===============================
echo   1. Open Pinterest
echo   2. Connect to my Pinterest account (first time only)
echo   3. Sort my pins - PREVIEW (changes nothing)
echo   4. Sort my pins - DO IT
echo   5. Find pins I might like (pin picker)
echo   6. Quit
echo.
set /p choice=Choose 1-6: 
if "%choice%"=="1" python main.py open
if "%choice%"=="2" python main.py login
if "%choice%"=="3" python main.py sort
if "%choice%"=="4" python main.py sort --apply
if "%choice%"=="5" python main.py discover
if "%choice%"=="6" exit /b 0
echo.
pause
goto menu
