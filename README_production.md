# TwinBridge Modbus RTU Bridge

A Windows service that bridges communication between Modbus RTU devices and TwinCAT ADS, enabling seamless data exchange in industrial automation systems.

## Overview

TwinBridge provides bidirectional communication between:
- **Modbus RTU devices** (via RS-485/RS-232 serial communication)
- **TwinCAT PLC systems** (via ADS protocol)

## Features

- **Bidirectional Data Flow**: Read from Modbus devices to PLC, write from PLC to Modbus devices
- **Multiple Device Support**: Configure multiple Modbus slaves with different register mappings
- **Persistent Queue**: SQLite-based message queue ensures data integrity during network interruptions
- **Health Monitoring**: Built-in HTTP health check endpoints for monitoring service status
- **Windows Service**: Runs as a background service with automatic startup
- **Configurable Polling**: Adjustable polling intervals and retry logic
- **Debug Logging**: Comprehensive logging for troubleshooting

## System Requirements

- Windows 10/Server 2016 or later
- .NET 6.0 Runtime
- Serial port (RS-232/RS-485) for Modbus RTU communication
- TwinCAT runtime (for ADS communication)

## Installation

### Using MSI Installer (Recommended)
1. Run `TwinBridge-ModbusRtuBridge-1.0.0.msi` as Administrator
2. The service will be installed and started automatically
3. Configure settings in `appsettings.json` located in the installation directory

### Manual Installation
1. Extract the ZIP file to `C:\Program Files\TwinBridge\`
2. Edit `appsettings.json` for your configuration
3. Install the service using PowerShell as Administrator:
   ```powershell
   sc.exe create "TwinBridge" binPath="C:\Program Files\TwinBridge\ModbusAdsBridge.exe" start=auto
   sc.exe start "TwinBridge"
   ```

## Configuration

Edit `appsettings.json` to configure the bridge:

### ADS Settings
- `AmsNetId`: Leave empty for local ADS, or specify remote ADS server (e.g., "192.168.1.100.1.1")
- `AmsPort`: ADS port (default: 851)

### Serial Port Settings
- `SerialPortName`: COM port for Modbus RTU (e.g., "COM1")
- `BaudRate`: Communication speed (default: 9600)
- `DataBits`: Data bits (default: 8)
- `Parity`: Parity setting ("None", "Odd", "Even")
- `StopBits`: Stop bits ("One", "Two")

### Modbus Devices
Configure each Modbus slave device:
```json
{
  "SlaveId": 1,
  "Name": "Device1",
  "Registers": [
    {
      "RegisterType": "HoldingRegister",
      "Address": 0,
      "Count": 1,
      "DataType": "UInt16",
      "AdsSymbol": "MAIN.Device1_Register0",
      "Enabled": true
    }
  ]
}
```

### ADS Symbol Mappings
Configure PLC variables to write to Modbus:
```json
{
  "SymbolName": "MAIN.OutputToModbus",
  "DataType": "UInt16",
  "ReadOnly": false,
  "ModbusMapping": "1:HoldingRegister:100"
}
```

## Supported Data Types

- **Bool**: Boolean values
- **UInt16**: 16-bit unsigned integer
- **Int16**: 16-bit signed integer
- **UInt32**: 32-bit unsigned integer (uses 2 Modbus registers)
- **Int32**: 32-bit signed integer (uses 2 Modbus registers)
- **Float**: 32-bit floating point (uses 2 Modbus registers)

## Register Types

- **HoldingRegister**: Read/Write registers (Function codes 3, 6, 16)
- **InputRegister**: Read-only registers (Function code 4)
- **Coil**: Read/Write single bits (Function codes 1, 5, 15)
- **DiscreteInput**: Read-only single bits (Function code 2)

## Monitoring

### Health Check Endpoints
Access these URLs to monitor service health:

- `http://localhost:8080/health` - Basic health status
- `http://localhost:8080/status` - Detailed service status
- `http://localhost:8080/metrics` - Performance metrics

### Windows Event Log
The service logs to the Windows Application Event Log. Filter by source "TwinBridge" to view service-related events.

## Troubleshooting

### Common Issues

1. **Serial Port Access Denied**
   - Ensure no other application is using the COM port
   - Check user permissions for the service account

2. **ADS Connection Failed**
   - Verify TwinCAT runtime is running
   - Check AMS NetID configuration (leave empty for local)
   - Ensure Windows Firewall allows ADS communication (port 48898)

3. **Modbus Communication Timeout**
   - Verify serial port settings (baud rate, parity, stop bits)
   - Check physical connections and termination resistors
   - Adjust ReadTimeout and WriteTimeout values

4. **Service Won't Start**
   - Check Windows Event Log for error details
   - Verify .NET 6.0 Runtime is installed
   - Ensure configuration file is valid JSON

### Debug Logging
Enable debug logging by setting `EnableDebugLogging: true` in `appsettings.json`. This provides detailed information about:
- Modbus register reads/writes
- ADS symbol operations
- Queue processing
- Connection status changes

## Administration

Use the included `admin-cli.ps1` script for common administrative tasks:

```powershell
# Check service status
.\admin-cli.ps1 -Status

# Restart service
.\admin-cli.ps1 -Restart

# View recent logs
.\admin-cli.ps1 -Logs

# Test connections
.\admin-cli.ps1 -Test
```

## Database

The service uses SQLite for persistent message queuing. The database file (`bridge_data.db`) is created automatically and contains:
- Message queue for PLC-to-Modbus operations
- Retry tracking for failed operations
- Automatic cleanup of old messages

## Performance Considerations

- **Polling Interval**: Balance between responsiveness and system load
- **Device Count**: More devices increase polling time
- **Register Count**: Batch multiple registers in single requests when possible
- **Network Latency**: Consider ADS network delays for remote PLCs

## Security

- Service runs as LocalSystem by default
- No external network listeners except health check port (8080)
- All configuration in local files
- ADS communication uses TwinCAT's built-in security

## Support

For technical support:
1. Check Windows Event Log for error messages
2. Enable debug logging for detailed diagnostics
3. Verify configuration against this documentation
4. Test individual components (serial port, ADS connection)

## Version Information

- **Version**: 1.0.0
- **Target Framework**: .NET 6.0
- **Dependencies**: TwinCAT.Ads, NModbus4, Microsoft.Data.Sqlite
- **License**: Review license terms in installer