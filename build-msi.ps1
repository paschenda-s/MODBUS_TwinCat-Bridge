# TwinBridge MSI Builder Script
# Requires WiX Toolset v3.11+

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "dist"
)

Write-Host "Building TwinBridge MSI installer..." -ForegroundColor Green

try {
    # Ensure output directory exists
    if (!(Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    }

    # Verify WiX tools are available
    $candleExe = Get-Command "candle.exe" -ErrorAction SilentlyContinue
    $lightExe = Get-Command "light.exe" -ErrorAction SilentlyContinue
    $heatExe = Get-Command "heat.exe" -ErrorAction SilentlyContinue

    if (!$candleExe -or !$lightExe -or !$heatExe) {
        throw "WiX Toolset not found. Please install WiX Toolset v3.11+ and add to PATH"
    }

    # Build paths
    $projectPath = "src/ModbusAdsBridge"
    $installerPath = "installer"
    $binPath = "$projectPath/bin/$Configuration/net8.0"
    $wixObjPath = "$installerPath/obj"
    $wixBinPath = "$installerPath/bin"

    # Create WiX output directories
    New-Item -ItemType Directory -Path $wixObjPath -Force | Out-Null
    New-Item -ItemType Directory -Path $wixBinPath -Force | Out-Null

    # Harvest files using heat.exe
    Write-Host "Harvesting files with heat.exe..." -ForegroundColor Yellow
    $heatArgs = @(
        "dir", $binPath,
        "-cg", "ProductComponents",
        "-gg", "-scom", "-sreg", "-sfrag", "-srd",
        "-dr", "BinFolder",
        "-out", "$installerPath/Generated.wxs"
    )
    & heat.exe $heatArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Heat.exe failed with exit code $LASTEXITCODE"
    }

    # Compile WiX source files
    Write-Host "Compiling WiX source files..." -ForegroundColor Yellow
    $candleArgs = @(
        "$installerPath/Product.wxs",
        "$installerPath/Generated.wxs",
        "-ext", "WixUtilExtension",
        "-out", "$wixObjPath/",
        "-dModbusAdsBridge.TargetPath=$binPath/ModbusAdsBridge.exe"
    )
    & candle.exe $candleArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Candle.exe failed with exit code $LASTEXITCODE"
    }

    # Link MSI package
    Write-Host "Linking MSI package..." -ForegroundColor Yellow
    $msiPath = Join-Path $OutputPath "TwinBridge-v1.0.0.msi"
    $lightArgs = @(
        "$wixObjPath/Product.wixobj",
        "$wixObjPath/Generated.wixobj",
        "-ext", "WixUtilExtension",
        "-out", $msiPath
    )
    & light.exe $lightArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Light.exe failed with exit code $LASTEXITCODE"
    }

    Write-Host "MSI installer created successfully: $msiPath" -ForegroundColor Green

} catch {
    Write-Error "MSI build failed: $($_.Exception.Message)"
    exit 1
} finally {
    # Clean up intermediate files
    if (Test-Path "$installerPath/Generated.wxs") {
        Remove-Item "$installerPath/Generated.wxs" -Force
    }
    if (Test-Path "$installerPath/obj") {
        Remove-Item "$installerPath/obj" -Recurse -Force
    }
    if (Test-Path "$installerPath/bin") {
        Remove-Item "$installerPath/bin" -Recurse -Force
    }
}