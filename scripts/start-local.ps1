# Starts the whole demo without Docker: Azurite, API (LocalDB), Blazor staff app, Angular dashboard.
# Each part opens in its own PowerShell window so the logs stay readable. Close the windows to stop.
#
#   Dashboard   http://localhost:4200
#   Staff input http://localhost:5090
#   API docs    http://localhost:5080/scalar

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# Use a per-user .NET 10 SDK if it was installed with dotnet-install (no admin rights needed).
$userDotnet = Join-Path $env:USERPROFILE '.dotnet'
$dotnetSetup = if (Test-Path (Join-Path $userDotnet 'dotnet.exe')) {
    "`$env:DOTNET_ROOT='$userDotnet'; `$env:PATH='$userDotnet;' + `$env:PATH;"
} else { '' }

sqllocaldb start MSSQLLocalDB | Out-Null

$azuriteData = Join-Path $root '.azurite'
New-Item -ItemType Directory -Force $azuriteData | Out-Null

function Start-Part([string]$title, [string]$command) {
    Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$Host.UI.RawUI.WindowTitle='$title'; $command"
}

Start-Part 'Azurite' "npx -y azurite@3.37.0 --silent --skipApiVersionCheck --location '$azuriteData'"
Start-Part 'API :5080' "$dotnetSetup Set-Location '$root'; dotnet run --project src/AthleteDay.Api"
Start-Part 'Staff :5090' "$dotnetSetup Set-Location '$root'; dotnet run --project src/AthleteDay.Staff"
Start-Part 'Dashboard :4200' "Set-Location '$root/web/dashboard'; if (-not (Test-Path node_modules)) { npm ci }; npx ng serve --port 4200"

Write-Host 'Starting... give it ~20 seconds, then open http://localhost:4200 and http://localhost:5090'
