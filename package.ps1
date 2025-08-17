# TwinBridge Modbus RTU Bridge Packaging Script
# Requires .NET 6 SDK and WiX Toolset v3.11+

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "dist"
)

Write-Host "Starting TwinBridge packaging process..." -ForegroundColor Green

# Create output directory
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

try {
    # Build the main project
    Write-Host "Building ModbusAdsBridge project..." -ForegroundColor Yellow
    dotnet build src/ModbusAdsBridge/ModbusAdsBridge.csproj -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build ModbusAdsBridge project"
    }

    # Publish the project
    Write-Host "Publishing ModbusAdsBridge project..." -ForegroundColor Yellow
    dotnet publish src/ModbusAdsBridge/ModbusAdsBridge.csproj -c $Configuration -o "publish/ModbusAdsBridge" --self-contained false
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to publish ModbusAdsBridge project"
    }

    # Create ZIP package
    Write-Host "Creating ZIP package..." -ForegroundColor Yellow
    $zipPath = Join-Path $OutputPath "TwinBridge-v1.0.0.zip"
    Compress-Archive -Path "publish/ModbusAdsBridge/*" -DestinationPath $zipPath -Force
    Write-Host "ZIP package created: $zipPath" -ForegroundColor Green

    # Build MSI installer if WiX is available
    Write-Host "Checking for WiX Toolset..." -ForegroundColor Yellow
    $wixPath = Get-Command "candle.exe" -ErrorAction SilentlyContinue
    if ($wixPath) {
        Write-Host "Building MSI installer..." -ForegroundColor Yellow
        & ./build-msi.ps1 -Configuration $Configuration -OutputPath $OutputPath
        if ($LASTEXITCODE -eq 0) {
            Write-Host "MSI installer built successfully" -ForegroundColor Green
        } else {
            Write-Warning "MSI build failed, but ZIP package is available"
        }
    } else {
        Write-Warning "WiX Toolset not found. Only ZIP package will be created."
        Write-Host "To build MSI installer, install WiX Toolset v3.11+ and add to PATH" -ForegroundColor Cyan
    }

    Write-Host "Packaging completed successfully!" -ForegroundColor Green
    Write-Host "Output directory: $OutputPath" -ForegroundColor Cyan
    Get-ChildItem $OutputPath | ForEach-Object {
        Write-Host "  - $($_.Name) ($([math]::Round($_.Length / 1MB, 2)) MB)" -ForegroundColor White
    }

} catch {
    Write-Error "Packaging failed: $($_.Exception.Message)"
    exit 1
} finally {
    # Clean up publish directory
    if (Test-Path "publish") {
        Remove-Item "publish" -Recurse -Force
    }
}