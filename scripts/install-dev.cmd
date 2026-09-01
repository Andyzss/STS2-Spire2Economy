@echo off
setlocal
title SpireEconomy Development Installer

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-dev.ps1"
set "installer_exit_code=%ERRORLEVEL%"

echo.
if not "%installer_exit_code%"=="0" (
    echo SpireEconomy installation failed. Review the error above.
) else (
    echo SpireEconomy installation completed. You may now start the game.
)

echo.
pause
exit /b %installer_exit_code%
