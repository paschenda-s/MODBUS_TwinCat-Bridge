using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace ModbusAdsBridge;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "TwinBridge";
            })
            .ConfigureServices((context, services) =>
            {
                // Register configuration
                var bridgeOptions = new BridgeOptions();
                context.Configuration.GetSection("Bridge").Bind(bridgeOptions);
                services.AddSingleton(bridgeOptions);

                // Register core services
                services.AddSingleton<AdsClientWrapper>();
                services.AddSingleton<ModbusRtuHelper>();
                services.AddSingleton<SerialPortManager>();
                services.AddSingleton<PersistentQueue>();

                // Register hosted services
                services.AddHostedService<PlcIngestWorker>();
                services.AddHostedService<BridgeWorker>();
                services.AddHostedService<HealthServerHostedService>();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddEventLog();
                
                if (context.HostingEnvironment.IsDevelopment())
                {
                    logging.SetMinimumLevel(LogLevel.Debug);
                }
                else
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                }
            });

        var host = builder.Build();

        try
        {
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogCritical(ex, "Application terminated unexpectedly");
            throw;
        }
    }
}