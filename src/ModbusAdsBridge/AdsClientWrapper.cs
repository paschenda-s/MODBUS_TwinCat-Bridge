using Microsoft.Extensions.Logging;
using System.Reflection;

namespace ModbusAdsBridge;

public class AdsClientWrapper : IDisposable
{
    private readonly ILogger<AdsClientWrapper> _logger;
    private readonly BridgeOptions _options;
    private object? _adsClient;
    private bool _isConnected;
    private readonly object _lockObject = new();
    private Type? _adsClientType;
    private Type? _amsNetIdType;

    public AdsClientWrapper(ILogger<AdsClientWrapper> logger, BridgeOptions options)
    {
        _logger = logger;
        _options = options;
        InitializeAdsTypes();
    }

    private void InitializeAdsTypes()
    {
        try
        {
            // Try to load TwinCAT.Ads assembly dynamically
            var adsAssembly = Assembly.LoadFrom("TwinCAT.Ads.dll");
            _adsClientType = adsAssembly.GetType("TwinCAT.Ads.AdsClient");
            _amsNetIdType = adsAssembly.GetType("TwinCAT.Ads.AmsNetId");
            
            if (_adsClientType != null && _amsNetIdType != null)
            {
                _logger.LogInformation("TwinCAT.Ads assembly loaded successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TwinCAT.Ads assembly not found. ADS functionality will be disabled.");
        }
    }

    public Task<bool> ConnectAsync()
    {
        lock (_lockObject)
        {
            if (_isConnected)
                return Task.FromResult(true);

            if (_adsClientType == null)
            {
                _logger.LogError("TwinCAT.Ads not available. Cannot connect to ADS server.");
                return Task.FromResult(false);
            }

            try
            {
                _adsClient = Activator.CreateInstance(_adsClientType);
                
                if (string.IsNullOrEmpty(_options.AmsNetId))
                {
                    // Connect to local ADS router
                    var connectMethod = _adsClientType.GetMethod("Connect", new[] { typeof(int) });
                    connectMethod?.Invoke(_adsClient, new object[] { _options.AmsPort });
                }
                else
                {
                    // Connect to remote ADS server
                    var amsNetIdConstructor = _amsNetIdType?.GetConstructor(new[] { typeof(string) });
                    var amsNetId = amsNetIdConstructor?.Invoke(new object[] { _options.AmsNetId });
                    
                    var connectMethod = _adsClientType.GetMethod("Connect", new[] { _amsNetIdType!, typeof(int) });
                    connectMethod?.Invoke(_adsClient, new object[] { amsNetId!, _options.AmsPort });
                }

                _isConnected = true;
                _logger.LogInformation("Successfully connected to ADS server. NetId: {NetId}, Port: {Port}",
                    string.IsNullOrEmpty(_options.AmsNetId) ? "local" : _options.AmsNetId, _options.AmsPort);
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to ADS server");
                _isConnected = false;
                DisposeAdsClient();
                return Task.FromResult(false);
            }
        }
    }

    public void Disconnect()
    {
        lock (_lockObject)
        {
            if (_adsClient != null)
            {
                try
                {
                    var disconnectMethod = _adsClientType?.GetMethod("Disconnect", Type.EmptyTypes);
                    disconnectMethod?.Invoke(_adsClient, null);
                    _logger.LogInformation("Disconnected from ADS server");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during ADS disconnect");
                }
                finally
                {
                    DisposeAdsClient();
                    _isConnected = false;
                }
            }
        }
    }

    public async Task<T?> ReadSymbolAsync<T>(string symbolName)
    {
        if (!_isConnected || _adsClient == null)
        {
            await ConnectAsync();
        }

        if (!_isConnected || _adsClient == null)
        {
            throw new InvalidOperationException("ADS client is not connected");
        }

        try
        {
            // Create variable handle
            var createHandleMethod = _adsClientType?.GetMethod("CreateVariableHandle", new[] { typeof(string) });
            var handle = createHandleMethod?.Invoke(_adsClient, new object[] { symbolName });
            
            // Read value
            var readMethod = _adsClientType?.GetMethods()
                .FirstOrDefault(m => m.Name == "Read" && m.IsGenericMethod && m.GetParameters().Length == 1);
            var genericReadMethod = readMethod?.MakeGenericMethod(typeof(T));
            var result = (T?)genericReadMethod?.Invoke(_adsClient, new object[] { handle! });
            
            // Delete handle
            var deleteHandleMethod = _adsClientType?.GetMethod("DeleteVariableHandle");
            deleteHandleMethod?.Invoke(_adsClient, new object[] { handle! });
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read ADS symbol: {SymbolName}", symbolName);
            throw;
        }
    }

    public async Task WriteSymbolAsync<T>(string symbolName, T value)
    {
        if (!_isConnected || _adsClient == null)
        {
            await ConnectAsync();
        }

        if (!_isConnected || _adsClient == null)
        {
            throw new InvalidOperationException("ADS client is not connected");
        }

        try
        {
            // Create variable handle
            var createHandleMethod = _adsClientType?.GetMethod("CreateVariableHandle", new[] { typeof(string) });
            var handle = createHandleMethod?.Invoke(_adsClient, new object[] { symbolName });
            
            // Write value
            var writeMethod = _adsClientType?.GetMethods()
                .FirstOrDefault(m => m.Name == "Write" && m.IsGenericMethod && m.GetParameters().Length == 2);
            var genericWriteMethod = writeMethod?.MakeGenericMethod(typeof(T));
            genericWriteMethod?.Invoke(_adsClient, new object[] { handle!, value! });
            
            // Delete handle
            var deleteHandleMethod = _adsClientType?.GetMethod("DeleteVariableHandle");
            deleteHandleMethod?.Invoke(_adsClient, new object[] { handle! });
            
            _logger.LogDebug("Successfully wrote value {Value} to ADS symbol: {SymbolName}", value, symbolName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write ADS symbol: {SymbolName}", symbolName);
            throw;
        }
    }

    public bool IsConnected => _isConnected;

    private void DisposeAdsClient()
    {
        if (_adsClient is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _adsClient = null;
    }

    public void Dispose()
    {
        Disconnect();
    }
}