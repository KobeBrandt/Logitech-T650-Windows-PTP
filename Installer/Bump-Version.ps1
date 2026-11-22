# ==============================================================================
# Helper Script: Bump the T650 Tray version & rebuild the setup .exe
# Usage: pwsh -File "Installer/Bump-Version.ps1" -NewVersion "2.0.1"
# The installer reads its version from the csproj, so only the csproj is changed.
# ==============================================================================

param(
    [Parameter(Mandatory = $true)]
    [string]$NewVersion
)

$ErrorActionPreference = "Stop"

# 1. Validate Semantic Version (e.g. 1.0.1 or 1.1.0)
if ($NewVersion -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "[Error] Version must follow Semantic Versioning 'MAJOR.MINOR.PATCH' (e.g. 1.1.0)!" -ForegroundColor Red
    Exit 1
}

$fourPartVersion = "$NewVersion.0"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Bumping T650 Tray Version -> $NewVersion ($fourPartVersion)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 2. Update T650Bridge.csproj
$csprojPath = Join-Path $repoRoot "src\T650Bridge\T650Bridge.csproj"
Write-Host "`n[1/2] Updating $csprojPath..." -ForegroundColor Yellow
$csprojContent = Get-Content $csprojPath -Raw
$csprojContent = [regex]::Replace($csprojContent, '<Version>[\d\.]+</Version>', "<Version>$NewVersion</Version>")
$csprojContent = [regex]::Replace($csprojContent, '<AssemblyVersion>[\d\.]+</AssemblyVersion>', "<AssemblyVersion>$fourPartVersion</AssemblyVersion>")
$csprojContent = [regex]::Replace($csprojContent, '<FileVersion>[\d\.]+</FileVersion>', "<FileVersion>$fourPartVersion</FileVersion>")
Set-Content -Path $csprojPath -Value $csprojContent -Encoding utf8 -NoNewline
Write-Host "  -> Updated to Version $NewVersion ($fourPartVersion)" -ForegroundColor Green

# 3. Rebuild the installer
Write-Host "`n[2/2] Rebuilding setup for version $NewVersion..." -ForegroundColor Yellow
$buildScript = Join-Path $scriptDir "Build-Installer.ps1"
& pwsh -File "$buildScript" -Version "$NewVersion"
if ($LASTEXITCODE -ne 0) { Exit $LASTEXITCODE }

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " Version bump to $NewVersion complete!" -ForegroundColor Green
Write-Host " Git release instructions:" -ForegroundColor Yellow
Write-Host "   git add -u" -ForegroundColor White
Write-Host "   git commit -m `"bump version to $NewVersion`"" -ForegroundColor White
Write-Host "   git tag -a v$NewVersion -m `"Release v$NewVersion`"" -ForegroundColor White
Write-Host "   git push origin main --tags" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green
