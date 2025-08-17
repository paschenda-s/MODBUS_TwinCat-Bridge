namespace ModbusAdsBridge;

public class BridgeOptions
{
    public string AmsNetId { get; set; } = "";
    public int AmsPort { get; set; } = 851;
    public string SerialPortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public int ReadTimeout { get; set; } = 1000;
    public int WriteTimeout { get; set; } = 1000;
    public int PollingIntervalMs { get; set; } = 1000;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 500;
    public string DatabasePath { get; set; } = "bridge_data.db";
    public int HealthCheckPort { get; set; } = 8080;
    public bool EnableDebugLogging { get; set; } = false;
    
    // Modbus device configurations
    public ModbusDeviceConfig[] ModbusDevices { get; set; } = Array.Empty<ModbusDeviceConfig>();
    
    // ADS symbol mappings
    public AdsSymbolMapping[] AdsSymbols { get; set; } = Array.Empty<AdsSymbolMapping>();
}

public class ModbusDeviceConfig
{
    public byte SlaveId { get; set; }
    public string Name { get; set; } = "";
    public ModbusRegisterMapping[] Registers { get; set; } = Array.Empty<ModbusRegisterMapping>();
}

public class ModbusRegisterMapping
{
    public string RegisterType { get; set; } = ""; // "HoldingRegister", "InputRegister", "Coil", "DiscreteInput"
    public ushort Address { get; set; }
    public ushort Count { get; set; } = 1;
    public string DataType { get; set; } = "UInt16"; // "UInt16", "Int16", "UInt32", "Int32", "Float", "Bool"
    public string AdsSymbol { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public class AdsSymbolMapping
{
    public string SymbolName { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool ReadOnly { get; set; } = false;
    public string ModbusMapping { get; set; } = "";
}