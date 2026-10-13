@echo off
setlocal
cd /d "%~dp0"

where npm >nul 2>nul
if errorlevel 1 (
    echo npm was not found. Install Node.js from https://nodejs.org first.
    pause
    exit /b 1
)

if not exist package.json (
    echo No package.json found - creating one...
    call npm init -y
)

echo.
echo Installing project dependencies...
call npm install argon2@0.45.1 express@5.2.1 express-session@1.19.0 multer@2.4.0 mysql2@3.24.5 ws@8.22.0 better-sqlite3
if errorlevel 1 goto :fail

echo.
echo Installing dev dependencies...
call npm install --save-dev nodemon
if errorlevel 1 goto :fail

echo.
echo All packages installed successfully.
pause
exit /b 0

:fail
echo.
echo Something went wrong during installation. See the errors above.
pause
exit /b 1