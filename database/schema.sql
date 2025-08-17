-- TwinBridge SQLite Database Schema
-- Version: 1.0.0

-- Main data exchange log table
CREATE TABLE IF NOT EXISTS DataExchange (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    ModbusData BLOB,
    AdsData BLOB,
    Status TEXT NOT NULL DEFAULT 'Success',
    ErrorMessage TEXT,
    Direction TEXT CHECK(Direction IN ('ModbusToAds', 'AdsToModbus', 'Bidirectional')) DEFAULT 'Bidirectional'
);

-- Configuration table for runtime settings
CREATE TABLE IF NOT EXISTS Configuration (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Key TEXT UNIQUE NOT NULL,
    Value TEXT,
    Description TEXT,
    LastModified DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Device status tracking
CREATE TABLE IF NOT EXISTS DeviceStatus (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    DeviceType TEXT NOT NULL CHECK(DeviceType IN ('Modbus', 'ADS')),
    DeviceName TEXT NOT NULL,
    Status TEXT NOT NULL CHECK(Status IN ('Connected', 'Disconnected', 'Error')),
    LastSeen DATETIME DEFAULT CURRENT_TIMESTAMP,
    ErrorCount INTEGER DEFAULT 0,
    UNIQUE(DeviceType, DeviceName)
);

-- Performance metrics
CREATE TABLE IF NOT EXISTS PerformanceMetrics (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    Operation TEXT NOT NULL,
    DurationMs INTEGER NOT NULL,
    Success BOOLEAN NOT NULL DEFAULT 1,
    Details TEXT
);

-- Indexes for better query performance
CREATE INDEX IF NOT EXISTS idx_dataexchange_timestamp ON DataExchange(Timestamp);
CREATE INDEX IF NOT EXISTS idx_dataexchange_status ON DataExchange(Status);
CREATE INDEX IF NOT EXISTS idx_devicestatus_type_name ON DeviceStatus(DeviceType, DeviceName);
CREATE INDEX IF NOT EXISTS idx_devicestatus_lastseen ON DeviceStatus(LastSeen);
CREATE INDEX IF NOT EXISTS idx_performance_timestamp ON PerformanceMetrics(Timestamp);
CREATE INDEX IF NOT EXISTS idx_performance_operation ON PerformanceMetrics(Operation);

-- Insert default configuration values
INSERT OR IGNORE INTO Configuration (Key, Value, Description) VALUES
('SchemaVersion', '1.0.0', 'Database schema version'),
('MaxLogRetentionDays', '30', 'Maximum number of days to retain log data'),
('PerformanceMetricsEnabled', 'true', 'Enable performance metrics collection'),
('AutoCleanupEnabled', 'true', 'Enable automatic cleanup of old data');

-- View for recent data exchange activity
CREATE VIEW IF NOT EXISTS RecentDataExchange AS
SELECT 
    Id,
    Timestamp,
    length(ModbusData) as ModbusDataSize,
    length(AdsData) as AdsDataSize,
    Status,
    ErrorMessage,
    Direction
FROM DataExchange
WHERE Timestamp >= datetime('now', '-1 day')
ORDER BY Timestamp DESC;

-- View for device health summary
CREATE VIEW IF NOT EXISTS DeviceHealthSummary AS
SELECT 
    DeviceType,
    DeviceName,
    Status,
    LastSeen,
    ErrorCount,
    CASE 
        WHEN Status = 'Connected' AND LastSeen >= datetime('now', '-5 minutes') THEN 'Healthy'
        WHEN Status = 'Connected' AND LastSeen < datetime('now', '-5 minutes') THEN 'Stale'
        WHEN Status = 'Disconnected' THEN 'Offline'
        ELSE 'Error'
    END as HealthStatus
FROM DeviceStatus;

-- Trigger to update LastModified on Configuration changes
CREATE TRIGGER IF NOT EXISTS tr_configuration_lastmodified
    AFTER UPDATE ON Configuration
    FOR EACH ROW
BEGIN
    UPDATE Configuration 
    SET LastModified = CURRENT_TIMESTAMP 
    WHERE Id = NEW.Id;
END;

-- Trigger for automatic cleanup of old data (if enabled)
CREATE TRIGGER IF NOT EXISTS tr_auto_cleanup
    AFTER INSERT ON DataExchange
    FOR EACH ROW
    WHEN (SELECT Value FROM Configuration WHERE Key = 'AutoCleanupEnabled') = 'true'
BEGIN
    DELETE FROM DataExchange 
    WHERE Timestamp < datetime('now', '-' || (
        SELECT Value FROM Configuration WHERE Key = 'MaxLogRetentionDays'
    ) || ' days');
    
    DELETE FROM PerformanceMetrics 
    WHERE Timestamp < datetime('now', '-' || (
        SELECT Value FROM Configuration WHERE Key = 'MaxLogRetentionDays'
    ) || ' days');
END;