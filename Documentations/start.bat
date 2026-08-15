@echo off
start /min cmd /k "docfx docfx.json --serve"
:loop
timeout /t 1 >nul
powershell -command "(New-Object System.Net.WebClient).DownloadString('http://localhost:8080/')"
if %errorlevel% equ 0 (
    start http://localhost:8080/
    exit
) else (
    goto loop
)