-- TwinBridge Modbus RTU Bridge Database Schema
-- SQLite database for persistent message queuing and logging

-- Message queue table for PLC-to-Modbus operations
CREATE TABLE IF NOT EXISTS message_queue (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp TEXT NOT NULL,
    message_type TEXT NOT NULL,
    message_data TEXT NOT NULL,
    retry_count INTEGER DEFAULT 0,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Index for performance on timestamp-based queries
CREATE INDEX IF NOT EXISTS idx_message_queue_timestamp 
ON message_queue(timestamp);

-- Index for performance on message type queries
CREATE INDEX IF NOT EXISTS idx_message_queue_type 
ON message_queue(message_type);

-- Index for cleanup operations
CREATE INDEX IF NOT EXISTS idx_message_queue_created_at 
ON message_queue(created_at);

-- Optional: Historical data logging table
CREATE TABLE IF NOT EXISTS data_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    source_type TEXT NOT NULL, -- 'modbus' or 'ads'
    device_id TEXT,
    symbol_name TEXT,
    register_address INTEGER,
    data_type TEXT,
    value TEXT,
    success BOOLEAN DEFAULT 1,
    error_message TEXT
);

-- Index for historical data queries
CREATE INDEX IF NOT EXISTS idx_data_history_timestamp 
ON data_history(timestamp);

CREATE INDEX IF NOT EXISTS idx_data_history_source 
ON data_history(source_type, device_id);

-- Optional: Configuration audit table
CREATE TABLE IF NOT EXISTS config_changes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    change_type TEXT NOT NULL, -- 'startup', 'config_reload', etc.
    description TEXT,
    config_snapshot TEXT -- JSON snapshot of configuration
);

-- Cleanup procedure (to be run periodically)
-- Remove old message queue entries (older than 24 hours)
-- DELETE FROM message_queue WHERE created_at < datetime('now', '-1 day');

-- Remove old history entries (older than 30 days)
-- DELETE FROM data_history WHERE timestamp < datetime('now', '-30 days');

-- Remove old config audit entries (older than 90 days)
-- DELETE FROM config_changes WHERE timestamp < datetime('now', '-90 days');

-- Views for monitoring
CREATE VIEW IF NOT EXISTS queue_summary AS
SELECT 
    message_type,
    COUNT(*) as message_count,
    AVG(retry_count) as avg_retries,
    MAX(retry_count) as max_retries,
    MIN(created_at) as oldest_message,
    MAX(created_at) as newest_message
FROM message_queue
GROUP BY message_type;

CREATE VIEW IF NOT EXISTS recent_activity AS
SELECT 
    timestamp,
    source_type,
    device_id,
    symbol_name,
    value,
    success,
    error_message
FROM data_history
WHERE timestamp > datetime('now', '-1 hour')
ORDER BY timestamp DESC
LIMIT 100;