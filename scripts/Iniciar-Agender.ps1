$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework

$url = 'http://localhost:5018'
$serverExe = Join-Path $PSScriptRoot 'Servidor\Agender.Web.exe'
$appExe = Join-Path $PSScriptRoot 'App-Windows\Agender.exe'
$dataDir = Join-Path $env:LOCALAPPDATA 'Agender'

function Test-Server {
    try {
        Invoke-WebRequest "$url/conta/entrar" -UseBasicParsing -TimeoutSec 2 | Out-Null
        return $true
    }
    catch {
        return [bool]$_.Exception.Response
    }
}

$serverProcess = $null

try {
    if (-not (Test-Server)) {
        New-Item -ItemType Directory -Force $dataDir | Out-Null
        $env:ASPNETCORE_URLS = $url
        $env:Agender__DatabasePath = Join-Path $dataDir 'agender.db'
        $env:Agender__EnableTunnel = 'true'

        $serverProcess = Start-Process $serverExe -WorkingDirectory (Split-Path $serverExe) -WindowStyle Normal -PassThru

        for ($i = 0; $i -lt 60 -and -not (Test-Server); $i++) {
            Start-Sleep -Milliseconds 500
        }

        if (-not (Test-Server)) {
            throw "O servidor não arrancou em $url."
        }
    }

    Start-Process $appExe -Wait
}
catch {
    [System.Windows.MessageBox]::Show($_.Exception.Message, 'Agender', 'OK', 'Error') | Out-Null
}
finally {
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        & taskkill /PID $serverProcess.Id /T /F | Out-Null
    }
}
