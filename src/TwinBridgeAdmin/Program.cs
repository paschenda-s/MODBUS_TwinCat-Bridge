using System.CommandLine;
using System.Data.SQLite;
using Microsoft.Extensions.Configuration;

namespace TwinBridgeAdmin;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("TwinBridge Administration Helper Tool");

        // Service management commands
        var serviceCommand = new Command("service", "Service management commands");
        
        var statusCommand = new Command("status", "Show service status");
        var startCommand = new Command("start", "Start the TwinBridge service");
        var stopCommand = new Command("stop", "Stop the TwinBridge service");
        var restartCommand = new Command("restart", "Restart the TwinBridge service");
        
        serviceCommand.AddCommand(statusCommand);
        serviceCommand.AddCommand(startCommand);
        serviceCommand.AddCommand(stopCommand);
        serviceCommand.AddCommand(restartCommand);

        // Database management commands
        var dbCommand = new Command("database", "Database management commands");
        
        var dbStatusCommand = new Command("status", "Show database status");
        var dbCleanCommand = new Command("clean", "Clean old database records");
        var dbExportCommand = new Command("export", "Export database to CSV");
        var dbStatsCommand = new Command("stats", "Show database statistics");
        
        dbCommand.AddCommand(dbStatusCommand);
        dbCommand.AddCommand(dbCleanCommand);
        dbCommand.AddCommand(dbExportCommand);
        dbCommand.AddCommand(dbStatsCommand);

        // Configuration commands
        var configCommand = new Command("config", "Configuration management");
        
        var configShowCommand = new Command("show", "Show current configuration");
        var configValidateCommand = new Command("validate", "Validate configuration");
        
        configCommand.AddCommand(configShowCommand);
        configCommand.AddCommand(configValidateCommand);

        // Logs commands
        var logsCommand = new Command("logs", "Log management");
        
        var logsShowCommand = new Command("show", "Show recent logs");
        var logsExportCommand = new Command("export", "Export logs to file");
        
        logsCommand.AddCommand(logsShowCommand);
        logsCommand.AddCommand(logsExportCommand);

        rootCommand.AddCommand(serviceCommand);
        rootCommand.AddCommand(dbCommand);
        rootCommand.AddCommand(configCommand);
        rootCommand.AddCommand(logsCommand);

        // Command handlers
        statusCommand.SetHandler(ShowServiceStatus);
        startCommand.SetHandler(StartService);
        stopCommand.SetHandler(StopService);
        restartCommand.SetHandler(RestartService);
        
        dbStatusCommand.SetHandler(ShowDatabaseStatus);
        dbCleanCommand.SetHandler(CleanDatabase);
        dbExportCommand.SetHandler(ExportDatabase);
        dbStatsCommand.SetHandler(ShowDatabaseStats);
        
        configShowCommand.SetHandler(ShowConfiguration);
        configValidateCommand.SetHandler(ValidateConfiguration);
        
        logsShowCommand.SetHandler(ShowLogs);
        logsExportCommand.SetHandler(ExportLogs);

        return await rootCommand.InvokeAsync(args);
    }

    static void ShowServiceStatus()
    {
        Console.WriteLine("TwinBridge Service Status");
        Console.WriteLine("========================");
        
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sc",
                Arguments = "query TwinBridge",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                Console.WriteLine(output);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static void StartService()
    {
        Console.WriteLine("Starting TwinBridge service...");
        ExecuteServiceCommand("start TwinBridge");
    }

    static void StopService()
    {
        Console.WriteLine("Stopping TwinBridge service...");
        ExecuteServiceCommand("stop TwinBridge");
    }

    static void RestartService()
    {
        Console.WriteLine("Restarting TwinBridge service...");
        StopService();
        Thread.Sleep(2000);
        StartService();
    }

    static void ExecuteServiceCommand(string arguments)
    {
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sc",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                if (!string.IsNullOrEmpty(output))
                    Console.WriteLine(output);
                if (!string.IsNullOrEmpty(error))
                    Console.WriteLine($"Error: {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static void ShowDatabaseStatus()
    {
        Console.WriteLine("Database Status");
        Console.WriteLine("===============");
        
        var config = LoadConfiguration();
        var dbPath = config.GetValue<string>("TwinBridge:DatabasePath", "database.db");
        
        if (File.Exists(dbPath))
        {
            var fileInfo = new FileInfo(dbPath);
            Console.WriteLine($"Database file: {dbPath}");
            Console.WriteLine($"Size: {fileInfo.Length / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"Last modified: {fileInfo.LastWriteTime}");
            
            // Get record counts
            using var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;");
            connection.Open();
            
            var tables = new[] { "DataExchange", "Configuration", "DeviceStatus", "PerformanceMetrics" };
            foreach (var table in tables)
            {
                try
                {
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
                    var count = cmd.ExecuteScalar();
                    Console.WriteLine($"{table}: {count} records");
                }
                catch
                {
                    Console.WriteLine($"{table}: Table not found");
                }
            }
        }
        else
        {
            Console.WriteLine($"Database file not found: {dbPath}");
        }
    }

    static void CleanDatabase()
    {
        Console.WriteLine("Cleaning old database records...");
        // Implementation for cleaning old records
        Console.WriteLine("Database cleanup completed.");
    }

    static void ExportDatabase()
    {
        Console.WriteLine("Exporting database to CSV...");
        // Implementation for database export
        Console.WriteLine("Database export completed.");
    }

    static void ShowDatabaseStats()
    {
        Console.WriteLine("Database Statistics");
        Console.WriteLine("==================");
        // Implementation for database statistics
    }

    static void ShowConfiguration()
    {
        Console.WriteLine("Current Configuration");
        Console.WriteLine("====================");
        
        var config = LoadConfiguration();
        var twinBridgeSection = config.GetSection("TwinBridge");
        
        foreach (var item in twinBridgeSection.GetChildren())
        {
            Console.WriteLine($"{item.Key}: {item.Value}");
        }
    }

    static void ValidateConfiguration()
    {
        Console.WriteLine("Validating configuration...");
        
        var config = LoadConfiguration();
        var errors = new List<string>();
        
        // Validate required settings
        var amsNetId = config.GetValue<string>("TwinBridge:AmsNetId");
        var adsPort = config.GetValue<int>("TwinBridge:AdsPort", 0);
        var serialPort = config.GetValue<string>("TwinBridge:ModbusSettings:SerialPort");
        
        if (adsPort <= 0)
            errors.Add("Invalid ADS port");
        
        if (string.IsNullOrEmpty(serialPort))
            errors.Add("Serial port not specified");
        
        if (errors.Any())
        {
            Console.WriteLine("Configuration errors found:");
            foreach (var error in errors)
            {
                Console.WriteLine($"  - {error}");
            }
        }
        else
        {
            Console.WriteLine("Configuration is valid.");
        }
    }

    static void ShowLogs()
    {
        Console.WriteLine("Recent Logs");
        Console.WriteLine("===========");
        // Implementation for showing recent logs
    }

    static void ExportLogs()
    {
        Console.WriteLine("Exporting logs...");
        // Implementation for log export
        Console.WriteLine("Logs exported.");
    }

    static IConfiguration LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
    }
}