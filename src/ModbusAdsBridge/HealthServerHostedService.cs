using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ModbusAdsBridge;

public class HealthServerHostedService : BackgroundService
{
    private readonly ILogger<HealthServerHostedService> _logger;
    private readonly BridgeOptions _options;
    private readonly AdsClientWrapper _adsClient;
    private readonly SerialPortManager _serialPortManager;
    private readonly PersistentQueue _queue;
    private HttpListener? _httpListener;

    public HealthServerHostedService(
        ILogger<HealthServerHostedService> logger,
        BridgeOptions options,
        AdsClientWrapper adsClient,
        SerialPortManager serialPortManager,
        PersistentQueue queue)
    {
        _logger = logger;
        _options = options;
        _adsClient = adsClient;
        _serialPortManager = serialPortManager;
        _queue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{_options.HealthCheckPort}/");
            _httpListener.Start();
            
            _logger.LogInformation("Health check server started on port {Port}", _options.HealthCheckPort);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context), stoppingToken);
                }
                catch (ObjectDisposedException)
                {
                    // HttpListener was disposed, exit gracefully
                    break;
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995)
                {
                    // Operation was aborted, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in health check server");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start health check server");
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            switch (request.Url?.AbsolutePath.ToLowerInvariant())
            {
                case "/health":
                    await HandleHealthCheckAsync(response);
                    break;
                case "/status":
                    await HandleStatusCheckAsync(response);
                    break;
                case "/metrics":
                    await HandleMetricsAsync(response);
                    break;
                default:
                    response.StatusCode = 404;
                    await SendResponseAsync(response, "Not Found");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling health check request");
            try
            {
                context.Response.StatusCode = 500;
                await SendResponseAsync(context.Response, "Internal Server Error");
            }
            catch
            {
                // Ignore errors when trying to send error response
            }
        }
    }

    private async Task HandleHealthCheckAsync(HttpListenerResponse response)
    {
        var health = await GetHealthStatusAsync();
        var statusCode = health.IsHealthy ? 200 : 503;
        
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        
        var json = JsonSerializer.Serialize(health, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        
        await SendResponseAsync(response, json);
    }

    private async Task HandleStatusCheckAsync(HttpListenerResponse response)
    {
        var status = await GetDetailedStatusAsync();
        
        response.StatusCode = 200;
        response.ContentType = "application/json";
        
        var json = JsonSerializer.Serialize(status, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        
        await SendResponseAsync(response, json);
    }

    private async Task HandleMetricsAsync(HttpListenerResponse response)
    {
        var metrics = await GetMetricsAsync();
        
        response.StatusCode = 200;
        response.ContentType = "application/json";
        
        var json = JsonSerializer.Serialize(metrics, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        
        await SendResponseAsync(response, json);
    }

    private async Task<HealthStatus> GetHealthStatusAsync()
    {
        var checks = new List<HealthCheck>();

        // Check ADS connection
        try
        {
            var adsHealthy = _adsClient.IsConnected || await _adsClient.ConnectAsync();
            checks.Add(new HealthCheck("ADS", adsHealthy ? "Healthy" : "Unhealthy", 
                adsHealthy ? null : "Cannot connect to ADS server"));
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheck("ADS", "Unhealthy", ex.Message));
        }

        // Check Serial Port
        try
        {
            var serialHealthy = await _serialPortManager.TestConnectionAsync();
            checks.Add(new HealthCheck("SerialPort", serialHealthy ? "Healthy" : "Unhealthy",
                serialHealthy ? null : "Cannot open serial port"));
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheck("SerialPort", "Unhealthy", ex.Message));
        }

        // Check Database
        try
        {
            await _queue.InitializeAsync();
            var queueSize = await _queue.GetQueueSizeAsync("plc_data");
            checks.Add(new HealthCheck("Database", "Healthy", $"Queue size: {queueSize}"));
        }
        catch (Exception ex)
        {
            checks.Add(new HealthCheck("Database", "Unhealthy", ex.Message));
        }

        var overallHealthy = checks.All(c => c.Status == "Healthy");

        return new HealthStatus
        {
            Status = overallHealthy ? "Healthy" : "Unhealthy",
            IsHealthy = overallHealthy,
            Timestamp = DateTime.UtcNow,
            Checks = checks.ToArray()
        };
    }

    private async Task<ServiceStatus> GetDetailedStatusAsync()
    {
        var health = await GetHealthStatusAsync();
        
        return new ServiceStatus
        {
            ServiceName = "TwinBridge Modbus RTU Bridge",
            Version = "1.0.0",
            Status = health.Status,
            Uptime = DateTime.UtcNow.Subtract(Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss"),
            Configuration = new
            {
                AmsNetId = string.IsNullOrEmpty(_options.AmsNetId) ? "local" : _options.AmsNetId,
                AmsPort = _options.AmsPort,
                SerialPort = _options.SerialPortName,
                BaudRate = _options.BaudRate,
                PollingInterval = _options.PollingIntervalMs,
                HealthCheckPort = _options.HealthCheckPort
            },
            HealthChecks = health.Checks
        };
    }

    private async Task<ServiceMetrics> GetMetricsAsync()
    {
        var process = Process.GetCurrentProcess();
        var queueSize = 0;
        
        try
        {
            await _queue.InitializeAsync();
            queueSize = await _queue.GetQueueSizeAsync("plc_data");
        }
        catch
        {
            // Ignore errors when getting queue size
        }

        return new ServiceMetrics
        {
            Timestamp = DateTime.UtcNow,
            ProcessId = process.Id,
            WorkingSet = process.WorkingSet64,
            CpuTime = process.TotalProcessorTime.TotalMilliseconds,
            ThreadCount = process.Threads.Count,
            QueueSize = queueSize,
            ConfiguredDevices = _options.ModbusDevices.Length,
            ConfiguredSymbols = _options.AdsSymbols.Length
        };
    }

    private static async Task SendResponseAsync(HttpListenerResponse response, string content)
    {
        var buffer = Encoding.UTF8.GetBytes(content);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Health check server is stopping...");
        
        _httpListener?.Stop();
        _httpListener?.Close();
        
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _httpListener?.Close();
        base.Dispose();
    }
}

public class HealthStatus
{
    public string Status { get; set; } = "";
    public bool IsHealthy { get; set; }
    public DateTime Timestamp { get; set; }
    public HealthCheck[] Checks { get; set; } = Array.Empty<HealthCheck>();
}

public class HealthCheck
{
    public HealthCheck(string name, string status, string? message = null)
    {
        Name = name;
        Status = status;
        Message = message;
    }

    public string Name { get; set; }
    public string Status { get; set; }
    public string? Message { get; set; }
}

public class ServiceStatus
{
    public string ServiceName { get; set; } = "";
    public string Version { get; set; } = "";
    public string Status { get; set; } = "";
    public string Uptime { get; set; } = "";
    public object Configuration { get; set; } = new();
    public HealthCheck[] HealthChecks { get; set; } = Array.Empty<HealthCheck>();
}

public class ServiceMetrics
{
    public DateTime Timestamp { get; set; }
    public int ProcessId { get; set; }
    public long WorkingSet { get; set; }
    public double CpuTime { get; set; }
    public int ThreadCount { get; set; }
    public int QueueSize { get; set; }
    public int ConfiguredDevices { get; set; }
    public int ConfiguredSymbols { get; set; }
}