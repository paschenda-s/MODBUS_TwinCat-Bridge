# Root-level MSI build script - calls the installer script
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "dist"
)

Write-Host "Building TwinBridge MSI installer..." -ForegroundColor Green

# Ensure we're in the repository root
if (-not (Test-Path "package.ps1")) {
    Write-Error "Please run this script from the repository root directory"
    exit 1
}

# Call the installer build script
& "installer/build-msi.ps1" -Configuration $Configuration -OutputDir $OutputDir