using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ModbusAdsBridge;

public class BridgeWorker : BackgroundService
{
    private readonly ILogger<BridgeWorker> _logger;
    private readonly BridgeOptions _options;
    private readonly ModbusRtuHelper _modbusHelper;
    private readonly AdsClientWrapper _adsClient;
    private readonly PersistentQueue _queue;

    public BridgeWorker(
        ILogger<BridgeWorker> logger,
        BridgeOptions options,
        ModbusRtuHelper modbusHelper,
        AdsClientWrapper adsClient,
        PersistentQueue queue)
    {
        _logger = logger;
        _options = options;
        _modbusHelper = modbusHelper;
        _adsClient = adsClient;
        _queue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bridge Worker started");

        // Initialize components
        await _queue.InitializeAsync();
        await _modbusHelper.InitializeAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Process Modbus to ADS data flow
                await ProcessModbusToAdsAsync(stoppingToken);

                // Process ADS to Modbus data flow (from queue)
                await ProcessAdsToModbusAsync(stoppingToken);

                await Task.Delay(_options.PollingIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bridge worker");
                await Task.Delay(5000, stoppingToken); // Wait before retrying
            }
        }

        _logger.LogInformation("Bridge Worker stopped");
    }

    private async Task ProcessModbusToAdsAsync(CancellationToken cancellationToken)
    {
        foreach (var device in _options.ModbusDevices)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await ProcessDeviceRegistersAsync(device, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process Modbus device {DeviceName} (Slave ID: {SlaveId})", 
                    device.Name, device.SlaveId);
            }
        }
    }

    private async Task ProcessDeviceRegistersAsync(ModbusDeviceConfig device, CancellationToken cancellationToken)
    {
        foreach (var register in device.Registers.Where(r => r.Enabled))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var value = await ReadModbusRegisterAsync(device.SlaveId, register);
                if (value != null)
                {
                    await WriteToAdsAsync(register.AdsSymbol, value, register.DataType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process register {RegisterType}:{Address} on device {DeviceName}", 
                    register.RegisterType, register.Address, device.Name);
            }
        }
    }

    private async Task<object?> ReadModbusRegisterAsync(byte slaveId, ModbusRegisterMapping register)
    {
        try
        {
            return register.RegisterType.ToUpperInvariant() switch
            {
                "HOLDINGREGISTER" => await ReadHoldingRegisterValueAsync(slaveId, register),
                "INPUTREGISTER" => await ReadInputRegisterValueAsync(slaveId, register),
                "COIL" => await ReadCoilValueAsync(slaveId, register),
                "DISCRETEINPUT" => await ReadDiscreteInputValueAsync(slaveId, register),
                _ => throw new ArgumentException($"Unknown register type: {register.RegisterType}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read Modbus register {RegisterType}:{Address} from slave {SlaveId}", 
                register.RegisterType, register.Address, slaveId);
            return null;
        }
    }

    private async Task<object?> ReadHoldingRegisterValueAsync(byte slaveId, ModbusRegisterMapping register)
    {
        var rawData = await _modbusHelper.ReadHoldingRegistersAsync(slaveId, register.Address, register.Count);
        if (rawData == null) return null;

        return ConvertModbusData(rawData, register.DataType);
    }

    private async Task<object?> ReadInputRegisterValueAsync(byte slaveId, ModbusRegisterMapping register)
    {
        var rawData = await _modbusHelper.ReadInputRegistersAsync(slaveId, register.Address, register.Count);
        if (rawData == null) return null;

        return ConvertModbusData(rawData, register.DataType);
    }

    private async Task<object?> ReadCoilValueAsync(byte slaveId, ModbusRegisterMapping register)
    {
        var rawData = await _modbusHelper.ReadCoilsAsync(slaveId, register.Address, register.Count);
        if (rawData == null) return null;

        return register.Count == 1 ? rawData[0] : rawData;
    }

    private async Task<object?> ReadDiscreteInputValueAsync(byte slaveId, ModbusRegisterMapping register)
    {
        var rawData = await _modbusHelper.ReadInputsAsync(slaveId, register.Address, register.Count);
        if (rawData == null) return null;

        return register.Count == 1 ? rawData[0] : rawData;
    }

    private static object? ConvertModbusData(ushort[] rawData, string dataType)
    {
        if (rawData.Length == 0) return null;

        return dataType.ToUpperInvariant() switch
        {
            "BOOL" => rawData[0] != 0,
            "UINT16" => rawData[0],
            "INT16" => (short)rawData[0],
            "UINT32" when rawData.Length >= 2 => (uint)((rawData[1] << 16) | rawData[0]),
            "INT32" when rawData.Length >= 2 => (int)((rawData[1] << 16) | rawData[0]),
            "FLOAT" when rawData.Length >= 2 => BitConverter.ToSingle(
                BitConverter.GetBytes((uint)((rawData[1] << 16) | rawData[0])), 0),
            _ => rawData[0]
        };
    }

    private async Task WriteToAdsAsync(string symbolName, object value, string dataType)
    {
        if (string.IsNullOrEmpty(symbolName)) return;

        if (!_adsClient.IsConnected)
        {
            await _adsClient.ConnectAsync();
        }

        if (!_adsClient.IsConnected)
        {
            _logger.LogWarning("Cannot connect to ADS, skipping write to symbol {SymbolName}", symbolName);
            return;
        }

        try
        {
            switch (dataType.ToUpperInvariant())
            {
                case "BOOL":
                    await _adsClient.WriteSymbolAsync(symbolName, Convert.ToBoolean(value));
                    break;
                case "UINT16":
                    await _adsClient.WriteSymbolAsync(symbolName, Convert.ToUInt16(value));
                    break;
                case "INT16":
                    await _adsClient.WriteSymbolAsync(symbolName, Convert.ToInt16(value));
                    break;
                case "UINT32":
                    await _adsClient.WriteSymbolAsync(symbolName, Convert.ToUInt32(value));
                    break;
                case "INT32":
                    await _adsClient.WriteSymbolAsync(symbolName, Convert.ToInt32(value));
                    break;
                case "FLOAT":
                    await _adsClient.WriteSymbolAsync(symbolName, Convert.ToSingle(value));
                    break;
                default:
                    _logger.LogWarning("Unsupported data type for ADS write: {DataType}", dataType);
                    break;
            }

            if (_options.EnableDebugLogging)
            {
                _logger.LogDebug("Successfully wrote {Value} to ADS symbol {SymbolName}", value, symbolName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write to ADS symbol {SymbolName}", symbolName);
        }
    }

    private async Task ProcessAdsToModbusAsync(CancellationToken cancellationToken)
    {
        // Process queued PLC data for writing to Modbus devices
        var message = await _queue.DequeueAsync<PlcDataMessage>("plc_data");
        if (message == null) return;

        try
        {
            await ProcessPlcDataMessageAsync(message.Data);
            await _queue.MarkAsProcessedAsync(message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process PLC data message {MessageId}", message.Id);
            
            if (message.RetryCount < _options.MaxRetries)
            {
                await _queue.IncrementRetryCountAsync(message.Id);
                await Task.Delay(_options.RetryDelayMs, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Max retries exceeded for message {MessageId}, discarding", message.Id);
                await _queue.MarkAsProcessedAsync(message.Id);
            }
        }
    }

    private async Task ProcessPlcDataMessageAsync(PlcDataMessage plcData)
    {
        if (string.IsNullOrEmpty(plcData.ModbusMapping))
            return;

        // Parse Modbus mapping (format: "SlaveId:RegisterType:Address")
        var parts = plcData.ModbusMapping.Split(':');
        if (parts.Length != 3) return;

        if (!byte.TryParse(parts[0], out var slaveId) ||
            !ushort.TryParse(parts[2], out var address))
        {
            _logger.LogWarning("Invalid Modbus mapping format: {ModbusMapping}", plcData.ModbusMapping);
            return;
        }

        var registerType = parts[1].ToUpperInvariant();

        try
        {
            await WriteToModbusAsync(slaveId, registerType, address, plcData.Value, plcData.DataType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write PLC data to Modbus device");
            throw;
        }
    }

    private async Task WriteToModbusAsync(byte slaveId, string registerType, ushort address, object value, string dataType)
    {
        switch (registerType)
        {
            case "HOLDINGREGISTER":
                await WriteHoldingRegisterAsync(slaveId, address, value, dataType);
                break;
            case "COIL":
                await _modbusHelper.WriteSingleCoilAsync(slaveId, address, Convert.ToBoolean(value));
                break;
            default:
                _logger.LogWarning("Unsupported Modbus register type for writing: {RegisterType}", registerType);
                break;
        }
    }

    private async Task WriteHoldingRegisterAsync(byte slaveId, ushort address, object value, string dataType)
    {
        switch (dataType.ToUpperInvariant())
        {
            case "BOOL":
                await _modbusHelper.WriteSingleRegisterAsync(slaveId, address, 
                    (ushort)(Convert.ToBoolean(value) ? 1 : 0));
                break;
            case "UINT16":
                await _modbusHelper.WriteSingleRegisterAsync(slaveId, address, Convert.ToUInt16(value));
                break;
            case "INT16":
                await _modbusHelper.WriteSingleRegisterAsync(slaveId, address, (ushort)Convert.ToInt16(value));
                break;
            case "UINT32":
            case "INT32":
            case "FLOAT":
                var bytes = dataType.ToUpperInvariant() switch
                {
                    "UINT32" => BitConverter.GetBytes(Convert.ToUInt32(value)),
                    "INT32" => BitConverter.GetBytes(Convert.ToInt32(value)),
                    "FLOAT" => BitConverter.GetBytes(Convert.ToSingle(value)),
                    _ => new byte[4]
                };
                var registers = new ushort[] {
                    BitConverter.ToUInt16(bytes, 0),
                    BitConverter.ToUInt16(bytes, 2)
                };
                await _modbusHelper.WriteMultipleRegistersAsync(slaveId, address, registers);
                break;
            default:
                _logger.LogWarning("Unsupported data type for Modbus write: {DataType}", dataType);
                break;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bridge Worker is stopping...");
        await base.StopAsync(cancellationToken);
    }
}