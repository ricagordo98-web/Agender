param(
    [int]$Port = 5018,
    [switch]$InstalarTarefa,
    [switch]$AbrirFirewall
)

$ErrorActionPreference = 'Stop'
$serverExe = Join-Path $PSScriptRoot 'Servidor\Agender.Web.exe'
$dataDir = Join-Path $env:LOCALAPPDATA 'Agender'

if ($AbrirFirewall) {
    New-NetFirewallRule -DisplayName "Agender ($Port)" -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -Profile Private | Out-Null
    Write-Host "Regra de firewall criada para a porta $Port (redes privadas)."
    return
}

if ($InstalarTarefa) {
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -WindowStyle Hidden -File `"$PSCommandPath`" -Port $Port"
    $trigger = New-ScheduledTaskTrigger -AtLogOn
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
    Register-ScheduledTask -TaskName 'Agender Servidor' -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null
    Write-Host "Tarefa 'Agender Servidor' criada: o servidor arranca sempre que iniciares sessão."
    return
}

New-Item -ItemType Directory -Force $dataDir | Out-Null
$env:ASPNETCORE_URLS = "http://0.0.0.0:$Port"
$env:Agender__DatabasePath = Join-Path $dataDir 'agender.db'

& $serverExe
