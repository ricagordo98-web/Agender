@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Publicar.ps1" %*
echo.
pause
