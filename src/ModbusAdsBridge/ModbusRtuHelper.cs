using NModbus;
using NModbus.Serial;
using Microsoft.Extensions.Logging;
using System.IO.Ports;

namespace ModbusAdsBridge;

public class ModbusRtuHelper : IDisposable
{
    private readonly ILogger<ModbusRtuHelper> _logger;
    private readonly SerialPortManager _serialPortManager;
    private IModbusMaster? _modbusMaster;
    private readonly object _lockObject = new();

    public ModbusRtuHelper(ILogger<ModbusRtuHelper> logger, SerialPortManager serialPortManager)
    {
        _logger = logger;
        _serialPortManager = serialPortManager;
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            var serialPort = await _serialPortManager.GetSerialPortAsync();
            if (serialPort == null)
            {
                _logger.LogError("Failed to get serial port for Modbus RTU");
                return false;
            }

            lock (_lockObject)
            {
                var factory = new ModbusFactory();
                _modbusMaster = factory.CreateRtuMaster(serialPort);
                _modbusMaster.Transport.Retries = 3;
                _modbusMaster.Transport.WaitToRetryMilliseconds = 250;
                _modbusMaster.Transport.ReadTimeout = 1000;
                _modbusMaster.Transport.WriteTimeout = 1000;
            }

            _logger.LogInformation("Modbus RTU master initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Modbus RTU master");
            return false;
        }
    }

    public async Task<ushort[]?> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort count)
    {
        if (_modbusMaster == null)
        {
            await InitializeAsync();
        }

        if (_modbusMaster == null)
        {
            throw new InvalidOperationException("Modbus master is not initialized");
        }

        try
        {
            lock (_lockObject)
            {
                return _modbusMaster.ReadHoldingRegisters(slaveId, startAddress, count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read holding registers from slave {SlaveId}, address {Address}, count {Count}", 
                slaveId, startAddress, count);
            throw;
        }
    }

    public async Task<ushort[]?> ReadInputRegistersAsync(byte slaveId, ushort startAddress, ushort count)
    {
        if (_modbusMaster == null)
        {
            await InitializeAsync();
        }

        if (_modbusMaster == null)
        {
            throw new InvalidOperationException("Modbus master is not initialized");
        }

        try
        {
            lock (_lockObject)
            {
                return _modbusMaster.ReadInputRegisters(slaveId, startAddress, count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read input registers from slave {SlaveId}, address {Address}, count {Count}", 
                slaveId, startAddress, count);
            throw;
        }
    }

    public async Task<bool[]?> ReadCoilsAsync(byte slaveId, ushort startAddress, ushort count)
    {
        if (_modbusMaster == null)
        {
            await InitializeAsync();
        }

        if (_modbusMaster == null)
        {
            throw new InvalidOperationException("Modbus master is not initialized");
        }

        try
        {
            lock (_lockObject)
            {
                return _modbusMaster.ReadCoils(slaveId, startAddress, count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read coils from slave {SlaveId}, address {Address}, count {Count}", 
                slaveId, startAddress, count);
            throw;
        }
    }

    public async Task<bool[]?> ReadInputsAsync(byte slaveId, ushort startAddress, ushort count)
    {
        if (_modbusMaster == null)
        {
            await InitializeAsync();
        }

        if (_modbusMaster == null)
        {
            throw new InvalidOperationException("Modbus master is not initialized");
        }

        try
        {
            lock (_lockObject)
            {
                return _modbusMaster.ReadInputs(slaveId, startAddress, count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read discrete inputs from slave {SlaveId}, address {Address}, count {Count}", 
                slaveId, startAddress, count);
            throw;
        }
    }

    public async Task WriteSingleRegisterAsync(byte slaveId, ushort registerAddress, ushort value)
    {
        if (_modbusMaster == null)
        {
            await InitializeAsync();
        }

        if (_modbusMaster == null)
        {
            throw new InvalidOperationException("Modbus master is not initialized");
        }

        try
        {
            lock (_lockObject)
            {
                _modbusMaster.WriteSingleRegister(slaveId, registerAddress, value);
            }
            
            _logger.LogDebug("Successfully wrote value {Value} to register {Address} on slave {SlaveId}", 
                value, registerAddress, slaveId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write register {Address} on slave {SlaveId}", registerAddress, slaveId);
            throw;
        }
    }

    public async Task WriteMultipleRegistersAsync(byte slaveId, ushort startAddress, ushort[] values)
    {
        if (_modbusMaster == null)
        {
            await InitializeAsync();
        }

        if (_modbusMaster == null)
        {
            throw new InvalidOperationException("Modbus master is not initialized");
        }

        try
        {
            lock (_lockObject)
            {
                _modbusMaster.WriteMultipleRegisters(slaveId, startAddress, values);
            }
            
            _logger.LogDebug("Successfully wrote {Count} values to registers starting at {Address} on slave {SlaveId}", 
                values.Length, startAddress, slaveId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write multiple registers starting at {Address} on slave {SlaveId}", 
                startAddress, slaveId);
            throw;
        }
    }

    public async Task WriteSingleCoilAsync(byte slaveId, ushort coilAddress, bool value)
    {
        if (_modbusMaster == null)
        {
            await InitializeAsync();
        }

        if (_modbusMaster == null)
        {
            throw new InvalidOperationException("Modbus master is not initialized");
        }

        try
        {
            lock (_lockObject)
            {
                _modbusMaster.WriteSingleCoil(slaveId, coilAddress, value);
            }
            
            _logger.LogDebug("Successfully wrote value {Value} to coil {Address} on slave {SlaveId}", 
                value, coilAddress, slaveId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write coil {Address} on slave {SlaveId}", coilAddress, slaveId);
            throw;
        }
    }

    public void Dispose()
    {
        lock (_lockObject)
        {
            _modbusMaster?.Dispose();
            _modbusMaster = null;
        }
        
        _logger.LogInformation("Modbus RTU helper disposed");
    }
}