using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModbusAdsBridge.Services;
using Moq;
using Xunit;

namespace ModbusAdsBridge.Tests.Services;

public class DatabaseServiceTests
{
    private readonly Mock<ILogger<DatabaseService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IConfigurationSection> _mockSection;
    private readonly DatabaseService _databaseService;
    private readonly string _testDbPath;

    public DatabaseServiceTests()
    {
        _mockLogger = new Mock<ILogger<DatabaseService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockSection = new Mock<IConfigurationSection>();
        
        // Use a temporary database file for testing
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        
        _mockSection.Setup(x => x.Value).Returns(_testDbPath);
        _mockConfiguration.Setup(x => x.GetSection("TwinBridge:DatabasePath")).Returns(_mockSection.Object);
        
        _databaseService = new DatabaseService(_mockLogger.Object, _mockConfiguration.Object);
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateDatabase_Successfully()
    {
        // Act
        await _databaseService.InitializeAsync();
        
        // Assert
        Assert.True(File.Exists(_testDbPath));
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Database initialized successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        // Cleanup
        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);
    }

    [Fact]
    public async Task LogDataExchangeAsync_ShouldComplete_WithoutException()
    {
        // Arrange
        await _databaseService.InitializeAsync();
        var modbusData = new byte[] { 0x01, 0x02 };
        var adsData = new byte[] { 0x03, 0x04 };
        
        // Act & Assert
        await _databaseService.LogDataExchangeAsync(modbusData, adsData);
        
        // Cleanup
        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);
    }

    [Fact]
    public async Task LogDataExchangeAsync_WithNullData_ShouldHandleGracefully()
    {
        // Arrange
        await _databaseService.InitializeAsync();
        
        // Act & Assert - Should not throw exception
        await _databaseService.LogDataExchangeAsync(null!, null!);
        
        // Cleanup
        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);
    }
}