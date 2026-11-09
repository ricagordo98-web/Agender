@echo off
setlocal
set "URL=http://localhost:5018"
set "ROOT=%~dp0"
set "STARTED="

set "DOCKER=%ProgramFiles%\Docker\Docker\Docker Desktop.exe"
tasklist /FI "IMAGENAME eq Docker Desktop.exe" | find /I "Docker Desktop.exe" >nul || if exist "%DOCKER%" (echo A abrir o Docker para o WhatsApp... & start "" "%DOCKER%")


curl -s -o nul "%URL%/conta/entrar" && goto app

if not exist "%LOCALAPPDATA%\Agender" mkdir "%LOCALAPPDATA%\Agender"
set "ASPNETCORE_URLS=%URL%"
set "Agender__DatabasePath=%LOCALAPPDATA%\Agender\agender.db"
set "Agender__EnableTunnel=true"

echo A iniciar o servidor...
start "Agender Servidor" /D "%ROOT%Servidor" "%ROOT%Servidor\Agender.Web.exe"
set "STARTED=1"

for /l %%i in (1,1,60) do (
    curl -s -o nul "%URL%/conta/entrar" && goto app
    ping -n 2 127.0.0.1 >nul
)

echo O servidor nao arrancou em %URL%.
pause
goto end

:app
echo A abrir a aplicacao...
start "" /wait "%ROOT%App-Windows\Agender.exe"

:end
if defined STARTED taskkill /IM Agender.Web.exe /T /F >nul 2>&1
endlocal
