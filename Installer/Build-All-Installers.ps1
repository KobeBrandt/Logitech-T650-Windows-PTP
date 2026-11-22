# ==============================================================================
# Build Script: Build the Logitech T650 Tray App Installer
# Compiles the Bridge tray app MSI and writes SHA256SUMS.txt
# ==============================================================================

param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$bridgeScript = Join-Path $scriptDir "T650Bridge\Build-Bridge-Installer.ps1"
$distDir = Join-Path $scriptDir "..\Dist"
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir -Force | Out-Null }
$distDir = Resolve-Path $distDir

$versionStr = if ($Version) { " v$Version" } else { "" }
Write-Host "==========================================================" -ForegroundColor Magenta
Write-Host " Building Logitech T650 Tray App MSI Installer$versionStr" -ForegroundColor Magenta
Write-Host "==========================================================" -ForegroundColor Magenta

$passArgs = @()
if ($Version) {
    $passArgs = @("-Version", $Version)
}

# 1. Build Bridge Tray App Installer
Write-Host "`n>>> Building Bridge Tray App Installer..." -ForegroundColor Cyan
& pwsh -File "$bridgeScript" @passArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "[Error] Bridge installer build failed!" -ForegroundColor Red
    Exit 1
}

# 2. Generate Unified SHA256SUMS.txt
Write-Host "`n>>> Generating unified SHA256SUMS.txt..." -ForegroundColor Cyan
$shaSumsPath = Join-Path $distDir "SHA256SUMS.txt"
$shaLines = @()
Get-ChildItem -Path "$distDir\*.msi" | Sort-Object Name | ForEach-Object {
    $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash.ToLower()
    $shaLines += "$hash  $($_.Name)"
}
$shaLines | Set-Content -Path $shaSumsPath -Encoding ascii
Write-Host " SHA256SUMS: $shaSumsPath" -ForegroundColor Gray

# 3. Final Summary
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " Installer Built Successfully!" -ForegroundColor Green
Write-Host " Destination Folder: $distDir" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green

Get-ChildItem -Path "$distDir\*.msi" | Select-Object Name, @{Name="Size (KB)"; Expression={[math]::Round($_.Length / 1KB, 1)}}, LastWriteTime | Format-Table -AutoSize
