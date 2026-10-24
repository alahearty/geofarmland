#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Stop-WithError {
    param([string]$Message)
    Write-Host ""
    Write-Host "ERROR: $Message" -ForegroundColor Red
    exit 1
}

function Require-Command {
    param([string]$Name, [string]$Hint)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Stop-WithError "$Name was not found. $Hint"
    }
}

function Start-AppWindow {
    param(
        [string]$Title,
        [string]$WorkingDirectory,
        [string]$Command
    )

    $safeTitle = $Title.Replace("'", "''")
    $safeDirectory = $WorkingDirectory.Replace("'", "''")
    $script = @'
$Host.UI.RawUI.WindowTitle = '__TITLE__'
Set-Location -LiteralPath '__DIRECTORY__'
__COMMAND__
'@
    $script = $script.Replace('__TITLE__', $safeTitle).Replace('__DIRECTORY__', $safeDirectory).Replace('__COMMAND__', $Command)
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
    Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoExit',
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-EncodedCommand', $encoded
    ) | Out-Null
}

Write-Host "GeoFarmland launcher" -ForegroundColor Green
Write-Host "Repository: $repoRoot"

Require-Command 'dotnet' 'Install the .NET 9 SDK and reopen this window.'
Require-Command 'node' 'Install Node.js 18 or newer and reopen this window.'
Require-Command 'npm' 'Install npm and reopen this window.'

$clientDirectory = Join-Path $repoRoot 'src\geofarmland.client'
if (-not (Test-Path (Join-Path $clientDirectory 'node_modules'))) {
    Write-Step 'Installing frontend dependencies'
    Push-Location $clientDirectory
    try {
        & npm install
        if ($LASTEXITCODE -ne 0) { Stop-WithError 'npm install failed.' }
    }
    finally {
        Pop-Location
    }
}

Write-Step 'Starting the ASP.NET Core backend on http://localhost:58036'
Start-AppWindow -Title 'GeoFarmland backend (http://localhost:58036)' -WorkingDirectory $repoRoot -Command @'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project 'src\geofarmland.Server' --no-launch-profile --urls 'http://localhost:58036'
'@

Write-Step 'Starting the Vue frontend on https://localhost:64210'
Start-AppWindow -Title 'GeoFarmland frontend (https://localhost:64210)' -WorkingDirectory $clientDirectory -Command @'
$env:ASPNETCORE_URLS = 'http://localhost:58036'
npm run dev
'@

Write-Host ""
Write-Host "Backend:  http://localhost:58036" -ForegroundColor Yellow
Write-Host "Frontend: https://localhost:64210" -ForegroundColor Yellow
Write-Host "PostgreSQL must be running. The backend applies migrations automatically."
Write-Host "The frontend uses a local development certificate; accept the browser warning if it appears."

Start-Sleep -Seconds 8
Start-Process 'https://localhost:64210'
