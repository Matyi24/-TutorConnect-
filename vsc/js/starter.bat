@echo off
cd /d "%~dp0"

echo Projekt package-ek letőltése...
echo.

where npm >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: npm was not found. Please install Node.js first:
    echo https://nodejs.org
    pause
    exit /b 1
)

call npm install express express-session better-sqlite3 argon2 mysql2 nodemon

echo.
echo Starting the app...
echo.
call npx nodemon main.js
echo Ha nem müködik töltsd le a Xampp-ot és inditsd el az Apache-t és Mysql-t (Zsamot be kell importálni)
pause