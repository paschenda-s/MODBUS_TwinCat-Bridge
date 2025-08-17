using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModbusAdsBridge.Services;
using Moq;
using Xunit;

namespace ModbusAdsBridge.Tests.Services;

public class ModbusServiceTests
{
    private readonly Mock<ILogger<ModbusService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly ModbusService _modbusService;

    public ModbusServiceTests()
    {
        _mockLogger = new Mock<ILogger<ModbusService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _modbusService = new ModbusService(_mockLogger.Object, _mockConfiguration.Object);
    }

    [Fact]
    public async Task ConnectAsync_ShouldComplete_WithoutException()
    {
        // Act & Assert
        await _modbusService.ConnectAsync();
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Connecting to Modbus RTU")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DisconnectAsync_ShouldComplete_WithoutException()
    {
        // Act & Assert
        await _modbusService.DisconnectAsync();
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disconnecting from Modbus RTU")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ReadDataAsync_ShouldReturn_EmptyByteArray()
    {
        // Act
        var result = await _modbusService.ReadDataAsync();
        
        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task WriteDataAsync_ShouldComplete_WithoutException()
    {
        // Arrange
        var testData = new byte[] { 0x01, 0x02, 0x03 };
        
        // Act & Assert
        await _modbusService.WriteDataAsync(testData);
    }
}