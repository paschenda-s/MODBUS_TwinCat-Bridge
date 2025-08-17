using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ModbusAdsBridge;

public class PersistentQueue : IDisposable
{
    private readonly ILogger<PersistentQueue> _logger;
    private readonly BridgeOptions _options;
    private SqliteConnection? _connection;
    private readonly object _lockObject = new();

    public PersistentQueue(ILogger<PersistentQueue> logger, BridgeOptions options)
    {
        _logger = logger;
        _options = options;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var connectionString = $"Data Source={_options.DatabasePath}";
            _connection = new SqliteConnection(connectionString);
            await _connection.OpenAsync();

            // Create queue table if it doesn't exist
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS message_queue (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp TEXT NOT NULL,
                    message_type TEXT NOT NULL,
                    message_data TEXT NOT NULL,
                    retry_count INTEGER DEFAULT 0,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                )";

            using var command = new SqliteCommand(createTableSql, _connection);
            await command.ExecuteNonQueryAsync();

            // Create index for performance
            var createIndexSql = @"
                CREATE INDEX IF NOT EXISTS idx_message_queue_timestamp 
                ON message_queue(timestamp)";

            using var indexCommand = new SqliteCommand(createIndexSql, _connection);
            await indexCommand.ExecuteNonQueryAsync();

            _logger.LogInformation("Persistent queue initialized with database: {DatabasePath}", _options.DatabasePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize persistent queue");
            throw;
        }
    }

    public async Task EnqueueAsync<T>(string messageType, T messageData)
    {
        if (_connection == null)
        {
            await InitializeAsync();
        }

        try
        {
            lock (_lockObject)
            {
                var json = JsonSerializer.Serialize(messageData);
                var timestamp = DateTime.UtcNow.ToString("O");

                var sql = @"
                    INSERT INTO message_queue (timestamp, message_type, message_data) 
                    VALUES (@timestamp, @messageType, @messageData)";

                using var command = new SqliteCommand(sql, _connection);
                command.Parameters.AddWithValue("@timestamp", timestamp);
                command.Parameters.AddWithValue("@messageType", messageType);
                command.Parameters.AddWithValue("@messageData", json);

                command.ExecuteNonQuery();
            }

            _logger.LogDebug("Enqueued message of type {MessageType}", messageType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue message of type {MessageType}", messageType);
            throw;
        }
    }

    public async Task<QueueMessage<T>?> DequeueAsync<T>(string messageType)
    {
        if (_connection == null)
        {
            await InitializeAsync();
        }

        try
        {
            lock (_lockObject)
            {
                var sql = @"
                    SELECT id, timestamp, message_data, retry_count 
                    FROM message_queue 
                    WHERE message_type = @messageType 
                    ORDER BY created_at ASC 
                    LIMIT 1";

                using var command = new SqliteCommand(sql, _connection);
                command.Parameters.AddWithValue("@messageType", messageType);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var id = reader.GetInt64(0);
                    var timestamp = reader.GetString(1);
                    var messageData = reader.GetString(2);
                    var retryCount = reader.GetInt32(3);

                    var data = JsonSerializer.Deserialize<T>(messageData);
                    if (data != null)
                    {
                        return new QueueMessage<T>(id, timestamp, data, retryCount);
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dequeue message of type {MessageType}", messageType);
            throw;
        }
    }

    public async Task MarkAsProcessedAsync(long messageId)
    {
        if (_connection == null)
        {
            await InitializeAsync();
        }

        try
        {
            lock (_lockObject)
            {
                var sql = "DELETE FROM message_queue WHERE id = @id";
                using var command = new SqliteCommand(sql, _connection);
                command.Parameters.AddWithValue("@id", messageId);
                command.ExecuteNonQuery();
            }

            _logger.LogDebug("Marked message {MessageId} as processed", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark message {MessageId} as processed", messageId);
            throw;
        }
    }

    public async Task IncrementRetryCountAsync(long messageId)
    {
        if (_connection == null)
        {
            await InitializeAsync();
        }

        try
        {
            lock (_lockObject)
            {
                var sql = "UPDATE message_queue SET retry_count = retry_count + 1 WHERE id = @id";
                using var command = new SqliteCommand(sql, _connection);
                command.Parameters.AddWithValue("@id", messageId);
                command.ExecuteNonQuery();
            }

            _logger.LogDebug("Incremented retry count for message {MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment retry count for message {MessageId}", messageId);
            throw;
        }
    }

    public async Task<int> GetQueueSizeAsync(string messageType)
    {
        if (_connection == null)
        {
            await InitializeAsync();
        }

        try
        {
            lock (_lockObject)
            {
                var sql = "SELECT COUNT(*) FROM message_queue WHERE message_type = @messageType";
                using var command = new SqliteCommand(sql, _connection);
                command.Parameters.AddWithValue("@messageType", messageType);
                var result = command.ExecuteScalar();
                return Convert.ToInt32(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get queue size for type {MessageType}", messageType);
            return 0;
        }
    }

    public async Task CleanupOldMessagesAsync(TimeSpan maxAge)
    {
        if (_connection == null)
        {
            await InitializeAsync();
        }

        try
        {
            var cutoffTime = DateTime.UtcNow.Subtract(maxAge);
            
            lock (_lockObject)
            {
                var sql = "DELETE FROM message_queue WHERE created_at < @cutoffTime";
                using var command = new SqliteCommand(sql, _connection);
                command.Parameters.AddWithValue("@cutoffTime", cutoffTime.ToString("O"));
                var deletedCount = command.ExecuteNonQuery();
                
                if (deletedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {DeletedCount} old messages older than {MaxAge}", 
                        deletedCount, maxAge);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old messages");
        }
    }

    public void Dispose()
    {
        lock (_lockObject)
        {
            _connection?.Dispose();
            _connection = null;
        }
        
        _logger.LogInformation("Persistent queue disposed");
    }
}

public record QueueMessage<T>(long Id, string Timestamp, T Data, int RetryCount);