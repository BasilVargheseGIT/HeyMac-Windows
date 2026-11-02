#!/usr/bin/env pwsh
# Build.ps1 — builds the Hey Mac Windows port.
#
# Usage:
#   .\Build.ps1                    # Debug build
#   .\Build.ps1 -Configuration Release
#   .\Build.ps1 -Configuration Release -Run

param(
    [Parameter()][string]$Configuration = "Debug",
    [Parameter()][switch]$Run,
    [Parameter()][switch]$Restore
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$sln = Join-Path $root "HeyMac.Windows.sln"
$app = Join-Path $root "src\HeyMac.App\HeyMac.App.csproj"

if (-not (Test-Path $sln)) {
    throw "Solution not found at $sln. Run this script from the HeyMac.Windows folder."
}

Write-Host "Hey Mac Windows Port — Build ($Configuration)" -ForegroundColor Cyan

if ($Restore) {
    Write-Host "Restoring NuGet packages..." -ForegroundColor Gray
    dotnet restore $sln
}

Write-Host "Building $sln ..." -ForegroundColor Gray
dotnet build $sln -c $Configuration --no-restore:$Restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}

Write-Host "Build succeeded." -ForegroundColor Green

if ($Run) {
    $out = Join-Path $root "src\HeyMac.App\bin\$Configuration\net8.0-windows\HeyMac.App.dll"
    Write-Host "Running Hey Mac..." -ForegroundColor Cyan
    dotnet run --project $app -c $Configuration
}
