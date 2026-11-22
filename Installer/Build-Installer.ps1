# ==============================================================================
# Build Script: Logitech T650 Tray setup .exe (Inno Setup 6)
# Publishes a self-contained win-x64 build and compiles Installer\T650Tray.iss.
# Usage: pwsh -File "Installer\Build-Installer.ps1" [-Version 2.0.0] [-Iscc <path to ISCC.exe>]
# Output: Dist\T650Tray-Setup-v<Version>.exe and Dist\SHA256SUMS.txt
# ==============================================================================

param(
    [string]$Version,
    [string]$Iscc
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
$bridgeProj = Join-Path $repoRoot "src\T650Bridge\T650Bridge.csproj"
$issPath = Join-Path $scriptDir "T650Tray.iss"
$publishDir = Join-Path $scriptDir "bin\publish"
$distDir = Join-Path $repoRoot "Dist"

function Find-Iscc {
    param([string]$Explicit)
    if ($Explicit) { return $Explicit }
    $onPath = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    return $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

# 1. Resolve version and compiler
if (-not $Version) {
    $csproj = Get-Content $bridgeProj -Raw
    $Version = if ($csproj -match '<Version>([\d\.]+)</Version>') { $Matches[1] } else { "0.0.0" }
}

$isccPath = Find-Iscc -Explicit $Iscc
if (-not $isccPath -or -not (Test-Path $isccPath)) {
    Write-Host "[Error] Inno Setup 6 compiler (ISCC.exe) not found. Install it with: winget install JRSoftware.InnoSetup" -ForegroundColor Red
    Exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Building Logitech T650 Tray setup v$Version" -ForegroundColor Cyan
Write-Host " ISCC: $isccPath" -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Cyan

# 2. Publish self-contained app
Write-Host "1/3 Publishing self-contained T650Bridge (win-x64)..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $bridgeProj -c Release -r win-x64 --self-contained true -p:Version=$Version -o $publishDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "[Error] dotnet publish failed!" -ForegroundColor Red
    Exit 1
}

# 3. Compile installer
Write-Host "2/3 Compiling installer with Inno Setup..." -ForegroundColor Cyan
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir -Force | Out-Null }
& $isccPath "/Qp" "/DAppVersion=$Version" "/DSourceDir=$publishDir" "/DOutputDir=$distDir" $issPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "[Error] ISCC failed!" -ForegroundColor Red
    Exit 1
}

# 4. Checksums
Write-Host "3/3 Writing SHA256SUMS.txt..." -ForegroundColor Cyan
$setupName = "T650Tray-Setup-v$Version.exe"
$setupPath = Join-Path $distDir $setupName
$hash = (Get-FileHash -Path $setupPath -Algorithm SHA256).Hash.ToLower()
"$hash  $setupName" | Set-Content -Path (Join-Path $distDir "SHA256SUMS.txt") -Encoding ascii

$item = Get-Item $setupPath
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " Setup built: $($item.FullName)" -ForegroundColor White
Write-Host " Size:        $([math]::Round($item.Length / 1MB, 1)) MB" -ForegroundColor Cyan
Write-Host " SHA-256:     $hash" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Green
