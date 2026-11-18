using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UpstoxAiScanner.Application.Configuration;
using UpstoxAiScanner.Application.DTOs.Scanner;
using UpstoxAiScanner.Application.Interfaces;
using UpstoxAiScanner.Infrastructure.Services;
using Xunit;

namespace UpstoxAiScanner.UnitTests.Services;

public class AiExplanationServiceTests
{
    private readonly Mock<IScannerService> _mockScannerService;
    private readonly Mock<ILogger<AiExplanationService>> _mockLogger;
    private readonly Mock<IOptionsMonitor<AiOptions>> _mockOptionsMonitor;
    private readonly HttpClient _httpClient;

    public AiExplanationServiceTests()
    {
        _mockScannerService = new Mock<IScannerService>();
        _mockLogger = new Mock<ILogger<AiExplanationService>>();
        _mockOptionsMonitor = new Mock<IOptionsMonitor<AiOptions>>();
        _httpClient = new HttpClient();

        // Default: AI disabled or unconfigured -> deterministic engine fallback
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(new AiOptions
        {
            ApiKey = "",
            EnableAiExplanations = true
        });
    }

    [Fact]
    public async Task ExplainScanResultAsync_BullishSetup_ProducesGroundedCommentary()
    {
        // Arrange
        var service = new AiExplanationService(
            _mockScannerService.Object,
            _httpClient,
            _mockOptionsMonitor.Object,
            _mockLogger.Object);

        var scanResult = new ScanResultDto
        {
            Symbol = "RELIANCE",
            CompanyName = "Reliance Industries Ltd.",
            ScanType = "Swing",
            TechnicalScore = 85m,
            Signal = "Strong Bullish",
            CurrentPrice = 3020.50m,
            Ema20 = 2950m,
            Ema50 = 2890m,
            Ema200 = 2650m,
            Rsi = 65.4m,
            Macd = 24.5m,
            VolumeRatio = 2.45m,
            BreakoutStatus = "Breakout20",
            VolumeStatus = "Surge",
            Explanation = "Strong momentum breakout; 20 EMA above 50 EMA; High relative volume 2.45x"
        };

        // Act
        var result = await service.ExplainScanResultAsync(scanResult);

        // Assert
        result.Should().NotBeNull();
        result.Provider.Should().Be("RuleBasedFinancialEngine");
        result.Symbol.Should().Be("RELIANCE");
        result.CompanyName.Should().Be("Reliance Industries Ltd.");
        result.ScanType.Should().Be("Swing");
        result.TechnicalScore.Should().Be(85m);
        result.Signal.Should().Be("Strong Bullish");
        result.CurrentPrice.Should().Be(3020.50m);

        result.ExecutiveSummary.Should().Contain("RELIANCE");
        result.ExecutiveSummary.Should().ContainEquivalentOf("Strong Bullish");
        result.ExecutiveSummary.Should().Contain("85.0/100");

        result.KeyDrivers.Should().NotBeEmpty();
        result.KeyDrivers.Should().Contain(d => d.Contains("Breakout") || d.Contains("High"));
        result.KeyDrivers.Should().Contain(d => d.Contains("Volume") || d.Contains("surge") || d.Contains("2.45x"));

        result.SupportResistanceLevels.Should().NotBeNullOrWhiteSpace();
        result.SupportResistanceLevels.Should().Contain("2950"); // Dynamic EMA support

        result.RiskFactors.Should().NotBeNullOrWhiteSpace();
        result.FullCommentary.Should().Contain("RELIANCE");
    }

    [Fact]
    public async Task ExplainScanResultAsync_BearishSetup_ProducesRiskOrientedCommentary()
    {
        // Arrange
        var service = new AiExplanationService(
            _mockScannerService.Object,
            _httpClient,
            _mockOptionsMonitor.Object,
            _mockLogger.Object);

        var scanResult = new ScanResultDto
        {
            Symbol = "TCS",
            CompanyName = "Tata Consultancy Services Ltd.",
            ScanType = "Swing",
            TechnicalScore = 15m,
            Signal = "Strong Bearish",
            CurrentPrice = 3800m,
            Ema20 = 3920m,
            Ema50 = 4050m,
            Ema200 = 4100m,
            Rsi = 32.1m,
            Macd = -35.2m,
            VolumeRatio = 1.1m,
            Explanation = "Trend breakdown below key moving averages; Bearish EMA alignment"
        };

        // Act
        var result = await service.ExplainScanResultAsync(scanResult);

        // Assert
        result.Should().NotBeNull();
        result.Provider.Should().Be("RuleBasedFinancialEngine");
        result.Symbol.Should().Be("TCS");
        result.Signal.Should().Be("Strong Bearish");
        result.TechnicalScore.Should().Be(15m);
        result.ExecutiveSummary.Should().ContainEquivalentOf("Bearish");
        result.RiskFactors.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExplainStockAsync_WhenStockFound_ReturnsExplanation()
    {
        // Arrange
        var scanResult = new ScanResultDto
        {
            Symbol = "INFY",
            CompanyName = "Infosys Ltd.",
            ScanType = "Swing",
            TechnicalScore = 72m,
            Signal = "Bullish",
            CurrentPrice = 1850m
        };

        _mockScannerService
            .Setup(s => s.ScanSingleInstrumentAsync("INFY", "Swing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResult);

        var service = new AiExplanationService(
            _mockScannerService.Object,
            _httpClient,
            _mockOptionsMonitor.Object,
            _mockLogger.Object);

        // Act
        var result = await service.ExplainStockAsync("INFY", "Swing");

        // Assert
        result.Should().NotBeNull();
        result!.Symbol.Should().Be("INFY");
        result.TechnicalScore.Should().Be(72m);
        result.Signal.Should().Be("Bullish");
    }

    [Fact]
    public async Task ExplainStockAsync_WhenStockNotFound_ReturnsNull()
    {
        // Arrange
        _mockScannerService
            .Setup(s => s.ScanSingleInstrumentAsync("UNKNOWN", "Swing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScanResultDto?)null);

        var service = new AiExplanationService(
            _mockScannerService.Object,
            _httpClient,
            _mockOptionsMonitor.Object,
            _mockLogger.Object);

        // Act
        var result = await service.ExplainStockAsync("UNKNOWN", "Swing");

        // Assert
        result.Should().BeNull();
    }
}
