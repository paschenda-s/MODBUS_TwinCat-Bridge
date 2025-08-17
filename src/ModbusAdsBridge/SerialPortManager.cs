using System.IO.Ports;
using Microsoft.Extensions.Logging;

namespace ModbusAdsBridge;

public class SerialPortManager : IDisposable
{
    private readonly ILogger<SerialPortManager> _logger;
    private readonly BridgeOptions _options;
    private SerialPort? _serialPort;
    private readonly object _lockObject = new();

    public SerialPortManager(ILogger<SerialPortManager> logger, BridgeOptions options)
    {
        _logger = logger;
        _options = options;
    }

    public Task<SerialPort?> GetSerialPortAsync()
    {
        lock (_lockObject)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                return Task.FromResult<SerialPort?>(_serialPort);
            }

            return Task.FromResult(InitializeSerialPort());
        }
    }

    private SerialPort? InitializeSerialPort()
    {
        try
        {
            // Dispose existing port if any
            _serialPort?.Dispose();

            _serialPort = new SerialPort(_options.SerialPortName)
            {
                BaudRate = _options.BaudRate,
                DataBits = _options.DataBits,
                Parity = ParseParity(_options.Parity),
                StopBits = ParseStopBits(_options.StopBits),
                ReadTimeout = _options.ReadTimeout,
                WriteTimeout = _options.WriteTimeout,
                Handshake = Handshake.None,
                RtsEnable = false,
                DtrEnable = false
            };

            _serialPort.Open();
            
            _logger.LogInformation("Serial port {PortName} opened successfully. Config: {BaudRate} baud, {DataBits}-{Parity}-{StopBits}",
                _options.SerialPortName, _options.BaudRate, _options.DataBits, _options.Parity, _options.StopBits);

            return _serialPort;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize serial port {PortName}", _options.SerialPortName);
            _serialPort?.Dispose();
            _serialPort = null;
            return null;
        }
    }

    private static Parity ParseParity(string parity)
    {
        return parity.ToUpperInvariant() switch
        {
            "NONE" => Parity.None,
            "ODD" => Parity.Odd,
            "EVEN" => Parity.Even,
            "MARK" => Parity.Mark,
            "SPACE" => Parity.Space,
            _ => Parity.None
        };
    }

    private static StopBits ParseStopBits(string stopBits)
    {
        return stopBits.ToUpperInvariant() switch
        {
            "NONE" => StopBits.None,
            "ONE" => StopBits.One,
            "TWO" => StopBits.Two,
            "ONEPOINTFIVE" => StopBits.OnePointFive,
            _ => StopBits.One
        };
    }

    public void ClosePort()
    {
        lock (_lockObject)
        {
            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                        _logger.LogInformation("Serial port {PortName} closed", _options.SerialPortName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing serial port {PortName}", _options.SerialPortName);
                }
                finally
                {
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }
        }
    }

    public bool IsPortOpen
    {
        get
        {
            lock (_lockObject)
            {
                return _serialPort?.IsOpen ?? false;
            }
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var port = await GetSerialPortAsync();
            return port?.IsOpen ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Serial port connection test failed");
            return false;
        }
    }

    public void Dispose()
    {
        ClosePort();
    }
}