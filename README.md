# TwinBridge Modbus RTU Bridge

A .NET 8 Windows service that bridges communication between Modbus RTU devices and TwinCAT ADS (Automation Device Specification).

## Overview

TwinBridge provides seamless integration between industrial Modbus RTU devices and Beckhoff TwinCAT PLCs, enabling data exchange and protocol translation in industrial automation environments.

## Features

- **Modbus RTU Communication**: Supports serial communication with Modbus RTU devices
- **TwinCAT ADS Integration**: Direct communication with TwinCAT runtime via ADS protocol
- **Windows Service**: Runs as a system service with automatic startup
- **SQLite Logging**: Comprehensive data exchange logging and history
- **Configurable Polling**: Adjustable data polling intervals
- **Error Handling**: Robust error handling with automatic reconnection
- **MSI Installer**: Windows Installer package with service configuration

## Requirements

- Windows operating system
- .NET 8 Runtime
- TwinCAT runtime (for ADS communication)
- Serial port for Modbus RTU communication

## Installation

### Using MSI Installer

1. Run `TwinBridge-v1.0.0.msi` as Administrator
2. Follow the installation wizard
3. The service will be installed and started automatically

### Manual Installation

1. Extract the ZIP package to desired location
2. Configure `appsettings.json` (see Configuration section)
3. Install as Windows service:
   ```cmd
   sc create TwinBridge binPath="C:\Path\To\ModbusAdsBridge.exe" start=auto
   sc start TwinBridge
   ```

## Configuration

Edit `appsettings.json` to configure the bridge:

```json
{
  "TwinBridge": {
    "AmsNetId": "",                    // Leave empty for local ADS
    "AdsPort": 851,                    // TwinCAT ADS port
    "ModbusSettings": {
      "SerialPort": "COM1",            // Serial port for Modbus RTU
      "BaudRate": 9600,                // Baud rate
      "DataBits": 8,                   // Data bits
      "Parity": "None",                // Parity setting
      "StopBits": "One",               // Stop bits
      "SlaveId": 1                     // Modbus slave ID
    },
    "DatabasePath": "database.db",     // SQLite database path
    "PollingIntervalMs": 1000          // Polling interval in milliseconds
  }
}
```

### AMS NetID Configuration

- **Local ADS**: Leave `AmsNetId` empty or set to local machine's AMS NetID
- **Remote ADS**: Set to target TwinCAT system's AMS NetID (e.g., "192.168.1.100.1.1")

## Building from Source

### Prerequisites

- .NET 8 SDK
- WiX Toolset v3.11+ (for MSI generation)

### Build Commands

```powershell
# Build and package (creates ZIP and MSI)
.\package.ps1

# Build MSI only
.\build-msi.ps1

# Build project only
dotnet build src/ModbusAdsBridge/ModbusAdsBridge.csproj
```

## Service Management

```cmd
# Start service
sc start TwinBridge

# Stop service
sc stop TwinBridge

# Check service status
sc query TwinBridge

# Uninstall service
sc delete TwinBridge
```

## Logging

The service logs to:
- Windows Event Log (Application)
- Console (when running interactively)
- SQLite database (`database.db`)

## Database Schema

The SQLite database contains data exchange history:

```sql
CREATE TABLE DataExchange (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    ModbusData BLOB,
    AdsData BLOB,
    Status TEXT
);
```

## Development

### Project Structure

```
src/
├── ModbusAdsBridge/
│   ├── ModbusAdsBridge.csproj    # Main project file
│   ├── Program.cs                # Service entry point
│   ├── appsettings.json         # Configuration
│   └── Services/                # Service implementations
│       ├── ModbusBridgeService.cs
│       ├── IModbusService.cs
│       ├── IAdsService.cs
│       └── IDatabaseService.cs
installer/
├── Product.wxs                  # WiX installer definition
└── TwinBridgeInstaller.wixproj # WiX project file
tests/
└── ModbusAdsBridge.Tests/      # Unit tests
```

### Running Tests

```powershell
dotnet test
```

## License

[License information to be added]

## Support

For issues and support, please create an issue in the repository.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request