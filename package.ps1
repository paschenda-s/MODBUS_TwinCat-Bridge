# Package TwinBridge Modbus RTU Bridge for distribution
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "dist",
    [switch]$SkipMSI = $false
)

$ErrorActionPreference = "Stop"

Write-Host "Packaging TwinBridge Modbus RTU Bridge..." -ForegroundColor Green

# Ensure we're in the repository root
if (Test-Path "package.ps1") {
    $RepoRoot = Get-Location
} else {
    Write-Error "Please run this script from the repository root directory"
    exit 1
}

# Clean and create output directory
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Build the application
Write-Host "Building application for packaging..." -ForegroundColor Yellow
dotnet publish src/ModbusAdsBridge/ModbusAdsBridge.csproj -c $Configuration -o "$OutputDir/TwinBridge" --self-contained false

if ($LASTEXITCODE -ne 0) {
    throw "Failed to build application"
}

# Copy additional files
Write-Host "Copying additional files..." -ForegroundColor Yellow

# Copy documentation
Copy-Item "README_production.md" "$OutputDir/TwinBridge/README.md"

# Copy database schema
Copy-Item "db_schema.sql" "$OutputDir/TwinBridge/"

# Copy admin CLI script
Copy-Item "admin-cli.ps1" "$OutputDir/TwinBridge/"

# Create ZIP package
Write-Host "Creating ZIP package..." -ForegroundColor Yellow
$ZipPath = "$OutputDir/TwinBridge-ModbusRtuBridge-1.0.0.zip"

# Use .NET compression if available, otherwise use PowerShell 5+ Compress-Archive
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory("$OutputDir/TwinBridge", $ZipPath)
    Write-Host "ZIP package created using .NET compression" -ForegroundColor Gray
} catch {
    if (Get-Command Compress-Archive -ErrorAction SilentlyContinue) {
        Compress-Archive -Path "$OutputDir/TwinBridge/*" -DestinationPath $ZipPath -Force
        Write-Host "ZIP package created using PowerShell compression" -ForegroundColor Gray
    } else {
        Write-Warning "Could not create ZIP package - compression not available"
    }
}

# Build MSI if not skipped and WiX is available
if (-not $SkipMSI) {
    Write-Host "Building MSI installer..." -ForegroundColor Yellow
    
    # Check if WiX tools are available
    $HeatExists = Get-Command heat -ErrorAction SilentlyContinue
    $CandleExists = Get-Command candle -ErrorAction SilentlyContinue
    $LightExists = Get-Command light -ErrorAction SilentlyContinue
    
    if ($HeatExists -and $CandleExists -and $LightExists) {
        try {
            & "$RepoRoot/installer/build-msi.ps1" -Configuration $Configuration -OutputDir $OutputDir
            Write-Host "MSI installer created successfully" -ForegroundColor Green
        } catch {
            Write-Warning "Failed to create MSI installer: $($_.Exception.Message)"
            Write-Host "Continuing with ZIP package only..." -ForegroundColor Yellow
        }
    } else {
        Write-Warning "WiX Toolset not found in PATH. Skipping MSI creation."
        Write-Host "To build MSI, install WiX Toolset and ensure heat.exe, candle.exe, and light.exe are in PATH" -ForegroundColor Yellow
    }
} else {
    Write-Host "MSI creation skipped" -ForegroundColor Yellow
}

# Display results
Write-Host ""
Write-Host "Packaging completed!" -ForegroundColor Green
Write-Host "Output directory: $OutputDir" -ForegroundColor Cyan

Get-ChildItem $OutputDir -File | ForEach-Object {
    $size = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  $($_.Name) ($size MB)" -ForegroundColor White
}

Write-Host ""
Write-Host "Installation Instructions:" -ForegroundColor Cyan
Write-Host "1. Extract the ZIP file to your desired location" -ForegroundColor White
Write-Host "2. Review and edit appsettings.json for your environment" -ForegroundColor White
Write-Host "3. Install as Windows Service using sc.exe or use the MSI installer" -ForegroundColor White
Write-Host "4. Check the health endpoint at http://localhost:8080/health" -ForegroundColor White

Write-Host ""
Write-Host "Requirements:" -ForegroundColor Cyan
Write-Host "- .NET 6.0 Runtime or SDK" -ForegroundColor White
Write-Host "- Windows 10/Server 2016 or later" -ForegroundColor White
Write-Host "- TwinCAT ADS client libraries (if using remote ADS)" -ForegroundColor White