param(
    [string]$Output = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Agender-Release'),
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$windowsTarget = 'net10.0-windows10.0.19041.0'

function Invoke-Step([string]$title, [scriptblock]$action) {
    Write-Host "`n== $title" -ForegroundColor Cyan
    & $action
    if ($LASTEXITCODE -ne 0) {
        throw "Falhou: $title (código $LASTEXITCODE)"
    }
}

Set-Location $root

Get-Process -Name 'Agender', 'Agender.Web' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

if (-not $SkipTests) {
    Invoke-Step 'Testes' { dotnet test --nologo -v q }
}

Invoke-Step 'Servidor (Release)' { dotnet publish Agender.Web -c Release -o (Join-Path $Output 'Servidor') --nologo -v q }
Invoke-Step 'App Windows (Release)' { dotnet build Agender -c Release -f $windowsTarget --nologo -v q }

$appSource = Get-ChildItem (Join-Path $root "Agender\bin\Release\$windowsTarget") -Recurse -Filter 'Agender.exe' | Select-Object -First 1
if ($null -eq $appSource) {
    throw 'Não encontrei o Agender.exe compilado.'
}

Invoke-Step 'Copiar a app' { robocopy $appSource.DirectoryName (Join-Path $Output 'App-Windows') /MIR /NFL /NDL /NJH /NJS /NP | Out-Null; $global:LASTEXITCODE = [int]($LASTEXITCODE -ge 8) }

Copy-Item (Join-Path $PSScriptRoot 'Iniciar-Agender.ps1') $Output -Force
Copy-Item (Join-Path $PSScriptRoot 'Servidor-Partilhado.ps1') $Output -Force

$shortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Agender.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe')
$shortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$(Join-Path $Output 'Iniciar-Agender.ps1')`""
$shortcut.WorkingDirectory = $Output
$shortcut.IconLocation = Join-Path $Output 'App-Windows\Agender.exe'
$shortcut.Save()

Write-Host "`nConcluído. Atalho criado: $shortcutPath" -ForegroundColor Green
Write-Host "Pasta: $Output"
