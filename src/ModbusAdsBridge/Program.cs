using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModbusAdsBridge.Services;

namespace ModbusAdsBridge;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "TwinBridge";
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<ModbusBridgeService>();
                services.AddSingleton<IModbusService, ModbusService>();
                services.AddSingleton<IAdsService, AdsService>();
                services.AddSingleton<IDatabaseService, DatabaseService>();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddEventLog();
            });
}