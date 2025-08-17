using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ModbusAdsBridge;

public class PlcIngestWorker : BackgroundService
{
    private readonly ILogger<PlcIngestWorker> _logger;
    private readonly BridgeOptions _options;
    private readonly AdsClientWrapper _adsClient;
    private readonly PersistentQueue _queue;

    public PlcIngestWorker(
        ILogger<PlcIngestWorker> logger, 
        BridgeOptions options, 
        AdsClientWrapper adsClient,
        PersistentQueue queue)
    {
        _logger = logger;
        _options = options;
        _adsClient = adsClient;
        _queue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PLC Ingest Worker started");

        // Initialize the queue
        await _queue.InitializeAsync();

        // Wait for initial connection
        await Task.Delay(5000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPlcDataIngestionAsync(stoppingToken);
                await Task.Delay(_options.PollingIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PLC ingest worker");
                await Task.Delay(5000, stoppingToken); // Wait before retrying
            }
        }

        _logger.LogInformation("PLC Ingest Worker stopped");
    }

    private async Task ProcessPlcDataIngestionAsync(CancellationToken cancellationToken)
    {
        if (!_adsClient.IsConnected)
        {
            if (!await _adsClient.ConnectAsync())
            {
                _logger.LogWarning("Cannot connect to ADS server, skipping PLC data ingestion");
                return;
            }
        }

        foreach (var symbolMapping in _options.AdsSymbols)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (symbolMapping.ReadOnly)
                continue; // Skip read-only symbols for ingestion

            try
            {
                await ProcessSymbolAsync(symbolMapping, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process ADS symbol {SymbolName}", symbolMapping.SymbolName);
            }
        }

        // Cleanup old messages periodically (once per hour)
        if (DateTime.UtcNow.Minute == 0)
        {
            await _queue.CleanupOldMessagesAsync(TimeSpan.FromDays(1));
        }
    }

    private async Task ProcessSymbolAsync(AdsSymbolMapping symbolMapping, CancellationToken cancellationToken)
    {
        try
        {
            object? value = symbolMapping.DataType.ToUpperInvariant() switch
            {
                "BOOL" => await _adsClient.ReadSymbolAsync<bool>(symbolMapping.SymbolName),
                "BYTE" => await _adsClient.ReadSymbolAsync<byte>(symbolMapping.SymbolName),
                "INT16" => await _adsClient.ReadSymbolAsync<short>(symbolMapping.SymbolName),
                "UINT16" => await _adsClient.ReadSymbolAsync<ushort>(symbolMapping.SymbolName),
                "INT32" => await _adsClient.ReadSymbolAsync<int>(symbolMapping.SymbolName),
                "UINT32" => await _adsClient.ReadSymbolAsync<uint>(symbolMapping.SymbolName),
                "FLOAT" => await _adsClient.ReadSymbolAsync<float>(symbolMapping.SymbolName),
                "DOUBLE" => await _adsClient.ReadSymbolAsync<double>(symbolMapping.SymbolName),
                "STRING" => await _adsClient.ReadSymbolAsync<string>(symbolMapping.SymbolName),
                _ => null
            };

            if (value != null)
            {
                var plcData = new PlcDataMessage
                {
                    SymbolName = symbolMapping.SymbolName,
                    Value = value,
                    DataType = symbolMapping.DataType,
                    ModbusMapping = symbolMapping.ModbusMapping,
                    Timestamp = DateTime.UtcNow
                };

                await _queue.EnqueueAsync("plc_data", plcData);

                if (_options.EnableDebugLogging)
                {
                    _logger.LogDebug("Ingested PLC data: {SymbolName} = {Value}", 
                        symbolMapping.SymbolName, value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read PLC symbol {SymbolName}", symbolMapping.SymbolName);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PLC Ingest Worker is stopping...");
        await base.StopAsync(cancellationToken);
    }
}

public class PlcDataMessage
{
    public string SymbolName { get; set; } = "";
    public object Value { get; set; } = new();
    public string DataType { get; set; } = "";
    public string ModbusMapping { get; set; } = "";
    public DateTime Timestamp { get; set; }
}