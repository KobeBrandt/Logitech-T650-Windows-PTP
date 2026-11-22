# ==============================================================================
# Build Script: Logitech T650 Bridge Tray App MSI Installer
# Uses WiX Toolset v3.14 to compile and package:
#   T650Bridge-Setup-v<Version>.msi
# ==============================================================================

param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = Resolve-Path (Join-Path $scriptDir "..\..")
$bridgeProj = Join-Path $repoRoot "T650-Windows-PTP-Driver\T650Bridge\T650Bridge.csproj"
$objDir = Join-Path $scriptDir "obj"
$publishDir = Join-Path $scriptDir "bin\publish"
$distDir = Join-Path $repoRoot "Dist"
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir -Force | Out-Null }
$distDir = Resolve-Path $distDir

# Auto-detect version if not specified
if (-not $Version) {
    $csprojContent = Get-Content $bridgeProj -Raw
    if ($csprojContent -match '<Version>([\d\.]+)</Version>') {
        $Version = $Matches[1]
    } else {
        $Version = "1.0.0"
    }
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Building Logitech T650 Bridge Tray App Installer v$Version" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Locate WiX Toolset Binaries
$wixBin = "C:\Program Files (x86)\WiX Toolset v3.14\bin"
$candle = Join-Path $wixBin "candle.exe"
$light = Join-Path $wixBin "light.exe"

if (-not (Test-Path $candle) -or -not (Test-Path $light)) {
    Write-Host "[Error] WiX Toolset v3.14 binaries not found at '$wixBin'!" -ForegroundColor Red
    Exit 1
}

$sourceIco = Join-Path $repoRoot "T650-Windows-PTP-Driver\T650Bridge\T650.ico"
$licenseRtf = Join-Path $scriptDir "..\License.rtf"
if (-not (Test-Path $objDir)) { New-Item -ItemType Directory -Path $objDir -Force | Out-Null }

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

# 2. Publish .NET binary
Write-Host "1/4 Publishing T650Bridge..." -ForegroundColor Cyan
dotnet publish $bridgeProj -c Release -r win-x64 --self-contained false -o $publishDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "[Error] dotnet publish failed!" -ForegroundColor Red
    Exit 1
}

if (Test-Path $sourceIco) {
    Copy-Item $sourceIco -Destination $publishDir -Force
}

# 3. Compile WiX Source
Write-Host "2/4 Compiling WiX with candle.exe..." -ForegroundColor Cyan
$wxsPath = Join-Path $scriptDir "T650Bridge.wxs"
$wixObj = Join-Path $objDir "T650Bridge.wixobj"

& $candle -nologo -arch x64 -dSourceDir="$publishDir" -dLicenseRtf="$licenseRtf" -out "$wixObj" -ext WixUIExtension -ext WixUtilExtension "$wxsPath"
if ($LASTEXITCODE -ne 0) {
    Write-Host "[Error] candle.exe failed!" -ForegroundColor Red
    Exit 1
}

# 4. Link MSI
Write-Host "3/4 Linking MSI with light.exe..." -ForegroundColor Cyan
$msiFileName = "T650Bridge-Setup-v$Version.msi"
$msiOut = Join-Path $distDir $msiFileName

& $light -nologo -out "$msiOut" -ext WixUIExtension -ext WixUtilExtension -sice:ICE69 -sice:ICE91 "$wixObj"
if ($LASTEXITCODE -ne 0) {
    Write-Host "[Error] light.exe linking failed!" -ForegroundColor Red
    Exit 1
}

# Convenience copy without version
Copy-Item -Path $msiOut -Destination (Join-Path $distDir "T650Bridge-Setup.msi") -Force

# 5. Compute SHA-256 Checksum
Write-Host "4/4 Calculating SHA-256 Checksum..." -ForegroundColor Cyan
$hashHex = (Get-FileHash -Path $msiOut -Algorithm SHA256).Hash.ToLower()
$shaFile = "$msiOut.sha256"
"$hashHex  $msiFileName" | Set-Content -Path $shaFile -Encoding ascii

$msiItem = Get-Item $msiOut
Write-Host "==========================================================" -ForegroundColor Green
Write-Host " Tray App Installer Built Successfully!" -ForegroundColor Green
Write-Host " Output File: $($msiItem.FullName)" -ForegroundColor White
Write-Host " Size:        $([math]::Round($msiItem.Length / 1KB, 2)) KB" -ForegroundColor Cyan
Write-Host " SHA-256:     $hashHex" -ForegroundColor Yellow
Write-Host " Hash File:   $shaFile" -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Green
