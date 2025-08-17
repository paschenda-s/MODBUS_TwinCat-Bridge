using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModbusAdsBridge.Services;
using Moq;
using Xunit;

namespace ModbusAdsBridge.Tests.Services;

public class AdsServiceTests
{
    private readonly Mock<ILogger<AdsService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IConfigurationSection> _mockSection;
    private readonly AdsService _adsService;

    public AdsServiceTests()
    {
        _mockLogger = new Mock<ILogger<AdsService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockSection = new Mock<IConfigurationSection>();
        
        // Setup configuration section mocking
        _mockSection.Setup(x => x.Value).Returns("");
        _mockConfiguration.Setup(x => x.GetSection("TwinBridge:AmsNetId")).Returns(_mockSection.Object);
        _mockConfiguration.Setup(x => x.GetSection("TwinBridge:AdsPort")).Returns(_mockSection.Object);
        
        _adsService = new AdsService(_mockLogger.Object, _mockConfiguration.Object);
    }

    [Fact]
    public async Task ConnectAsync_WithEmptyAmsNetId_ShouldLogWarning()
    {
        // Act
        await _adsService.ConnectAsync();
        
        // Assert - Verify warning log for empty AMS NetID
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("AMS NetID is empty")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_ShouldLogConnectionAttempt()
    {
        // Act
        await _adsService.ConnectAsync();
        
        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Connecting to TwinCAT ADS")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DisconnectAsync_ShouldComplete_WithoutException()
    {
        // Act & Assert
        await _adsService.DisconnectAsync();
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disconnecting from TwinCAT ADS")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ReadDataAsync_ShouldReturn_EmptyByteArray()
    {
        // Act
        var result = await _adsService.ReadDataAsync();
        
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
        await _adsService.WriteDataAsync(testData);
    }
}