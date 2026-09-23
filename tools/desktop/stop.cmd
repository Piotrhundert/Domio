@echo off
taskkill /IM Domio.Web.exe /F >nul 2>&1

if %errorlevel%==0 (
    echo Domio zostalo zatrzymane.
) else (
    echo Domio nie jest uruchomione.
)