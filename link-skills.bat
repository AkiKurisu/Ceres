@echo off
setlocal

rem Link the Ceres plugin's skills into selected local coding agents.

set "SCRIPT_DIR=%~dp0"
echo Select where to link Ceres skills.
powershell -ExecutionPolicy Bypass -File "%SCRIPT_DIR%scripts\link-skills.ps1"

if errorlevel 1 (
    echo.
    echo Linking failed.
    pause
    exit /b 1
)

echo.
echo Ceres skill linking complete.
pause
