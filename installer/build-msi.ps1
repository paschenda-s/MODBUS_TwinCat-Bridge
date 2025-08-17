# Build MSI installer for TwinBridge Modbus RTU Bridge
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "dist"
)

$ErrorActionPreference = "Stop"

Write-Host "Building TwinBridge Modbus RTU Bridge MSI..." -ForegroundColor Green

# Ensure we're in the repository root
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

# Clean and create output directory
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Build the application
Write-Host "Building application..." -ForegroundColor Yellow
dotnet publish src/ModbusAdsBridge/ModbusAdsBridge.csproj -c $Configuration -o "$OutputDir/ModbusAdsBridge" --self-contained false

if ($LASTEXITCODE -ne 0) {
    throw "Failed to build application"
}

# Create temporary files directory
$TempDir = "$OutputDir/temp"
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

# Use Heat to harvest files
Write-Host "Harvesting files with Heat..." -ForegroundColor Yellow
$HeatArgs = @(
    "dir", "$OutputDir/ModbusAdsBridge"
    "-cg", "ProductComponents"
    "-gg", "-g1"
    "-sf", "-srd"
    "-dr", "BINDIR"
    "-out", "$TempDir/HarvestedFiles.wxs"
)

& heat @HeatArgs

if ($LASTEXITCODE -ne 0) {
    throw "Failed to harvest files with Heat"
}

# Copy main WXS file to temp directory
Copy-Item "installer/ModbusAdsBridge.wxs" "$TempDir/"

# Compile with Candle
Write-Host "Compiling WXS files..." -ForegroundColor Yellow
$CandleArgs = @(
    "$TempDir/ModbusAdsBridge.wxs"
    "$TempDir/HarvestedFiles.wxs"
    "-out", "$TempDir/"
)

& candle @CandleArgs

if ($LASTEXITCODE -ne 0) {
    throw "Failed to compile WXS files"
}

# Link with Light
Write-Host "Creating MSI package..." -ForegroundColor Yellow
$LightArgs = @(
    "$TempDir/ModbusAdsBridge.wixobj"
    "$TempDir/HarvestedFiles.wixobj"
    "-out", "$OutputDir/TwinBridge-ModbusRtuBridge-1.0.0.msi"
)

& light @LightArgs

if ($LASTEXITCODE -ne 0) {
    throw "Failed to create MSI package"
}

# Clean up temp files
Remove-Item $TempDir -Recurse -Force

Write-Host "MSI package created successfully: $OutputDir/TwinBridge-ModbusRtuBridge-1.0.0.msi" -ForegroundColor Green
Write-Host ""
Write-Host "To install the service:" -ForegroundColor Cyan
Write-Host "  msiexec /i TwinBridge-ModbusRtuBridge-1.0.0.msi" -ForegroundColor White
Write-Host ""
Write-Host "To uninstall the service:" -ForegroundColor Cyan
Write-Host "  msiexec /x TwinBridge-ModbusRtuBridge-1.0.0.msi" -ForegroundColor White