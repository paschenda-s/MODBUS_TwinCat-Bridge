using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data.SQLite;

namespace ModbusAdsBridge.Services;

public interface IDatabaseService
{
    Task InitializeAsync();
    Task LogDataExchangeAsync(byte[] modbusData, byte[] adsData);
}

public class DatabaseService : IDatabaseService
{
    private readonly ILogger<DatabaseService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public DatabaseService(ILogger<DatabaseService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        var dbPath = _configuration.GetSection("TwinBridge:DatabasePath")?.Value ?? "database.db";
        _connectionString = $"Data Source={dbPath};Version=3;";
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing SQLite database...");
        
        using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync();
        
        var createTableCmd = connection.CreateCommand();
        createTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS DataExchange (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                ModbusData BLOB,
                AdsData BLOB,
                Status TEXT
            );
            
            CREATE INDEX IF NOT EXISTS idx_timestamp ON DataExchange(Timestamp);
        ";
        
        await createTableCmd.ExecuteNonQueryAsync();
        _logger.LogInformation("Database initialized successfully");
    }

    public async Task LogDataExchangeAsync(byte[] modbusData, byte[] adsData)
    {
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();
            
            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO DataExchange (ModbusData, AdsData, Status)
                VALUES (@ModbusData, @AdsData, @Status)
            ";
            
            insertCmd.Parameters.AddWithValue("@ModbusData", modbusData);
            insertCmd.Parameters.AddWithValue("@AdsData", adsData);
            insertCmd.Parameters.AddWithValue("@Status", "Success");
            
            await insertCmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log data exchange to database");
        }
    }
}