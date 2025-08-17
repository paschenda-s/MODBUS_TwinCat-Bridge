using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace ModbusAdsBridge.Services;

public class ModbusBridgeService : BackgroundService
{
    private readonly ILogger<ModbusBridgeService> _logger;
    private readonly IModbusService _modbusService;
    private readonly IAdsService _adsService;
    private readonly IDatabaseService _databaseService;
    private readonly IConfiguration _configuration;

    public ModbusBridgeService(
        ILogger<ModbusBridgeService> logger,
        IModbusService modbusService,
        IAdsService adsService,
        IDatabaseService databaseService,
        IConfiguration configuration)
    {
        _logger = logger;
        _modbusService = modbusService;
        _adsService = adsService;
        _databaseService = databaseService;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TwinBridge Modbus RTU Bridge service starting...");

        try
        {
            await _databaseService.InitializeAsync();
            await _modbusService.ConnectAsync();
            await _adsService.ConnectAsync();

            var pollingIntervalSection = _configuration.GetSection("TwinBridge:PollingIntervalMs");
            var pollingInterval = int.TryParse(pollingIntervalSection?.Value, out var interval) ? interval : 1000;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDataExchangeAsync();
                    await Task.Delay(pollingInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during data exchange cycle");
                    await Task.Delay(5000, stoppingToken); // Wait 5 seconds before retry
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in TwinBridge service");
            throw;
        }
        finally
        {
            _logger.LogInformation("TwinBridge Modbus RTU Bridge service stopping...");
            await _modbusService.DisconnectAsync();
            await _adsService.DisconnectAsync();
        }
    }

    private async Task ProcessDataExchangeAsync()
    {
        // Read data from Modbus
        var modbusData = await _modbusService.ReadDataAsync();
        
        // Write data to ADS
        await _adsService.WriteDataAsync(modbusData);
        
        // Read data from ADS
        var adsData = await _adsService.ReadDataAsync();
        
        // Write data to Modbus
        await _modbusService.WriteDataAsync(adsData);
        
        // Log to database
        await _databaseService.LogDataExchangeAsync(modbusData, adsData);
    }
}