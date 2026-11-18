using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.Ai;
using UpstoxAiScanner.Application.DTOs.Scanner;
using Xunit;

namespace UpstoxAiScanner.IntegrationTests;

public class AiExplanationEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AiExplanationEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetStockAiExplanation_ValidSymbol_ReturnsGroundedCommentary()
    {
        // Act
        var response = await _client.GetAsync("/api/scanner/stock/RELIANCE/ai-explain?scanType=Swing");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AiExplanationDto>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();

        var data = body.Data!;
        data.Symbol.Should().Be("RELIANCE");
        data.TechnicalScore.Should().BeInRange(0m, 100m);
        data.Signal.Should().NotBeNullOrWhiteSpace();
        data.ExecutiveSummary.Should().NotBeNullOrWhiteSpace();
        data.KeyDrivers.Should().NotBeNull();
        data.SupportResistanceLevels.Should().NotBeNullOrWhiteSpace();
        data.RiskFactors.Should().NotBeNullOrWhiteSpace();
        data.FullCommentary.Should().NotBeNullOrWhiteSpace();
        data.Provider.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetStockAiExplanation_InvalidSymbol_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/scanner/stock/NONEXISTENT999/ai-explain");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AiExplanationDto>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task PostAiExplain_ValidScanResult_ReturnsCommentary()
    {
        // Arrange
        var scanResult = new ScanResultDto
        {
            Symbol = "TCS",
            CompanyName = "Tata Consultancy Services Ltd.",
            ScanType = "Swing",
            TechnicalScore = 78m,
            Signal = "Bullish",
            CurrentPrice = 4120.00m,
            Ema20 = 4050m,
            Ema50 = 3980m,
            Ema200 = 3800m,
            Rsi = 58.5m,
            Macd = 15.2m,
            VolumeRatio = 1.6m,
            BreakoutStatus = "Breakout20",
            Explanation = "Solid uptrend above all major moving averages."
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/scanner/ai-explain", scanResult);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AiExplanationDto>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();

        var data = body.Data!;
        data.Symbol.Should().Be("TCS");
        data.TechnicalScore.Should().Be(78m);
        data.Signal.Should().Be("Bullish");
        data.ExecutiveSummary.Should().Contain("TCS");
        data.Provider.Should().Be("RuleBasedFinancialEngine");
    }

    [Fact]
    public async Task PostAiExplain_MissingSymbol_ReturnsBadRequest()
    {
        // Arrange
        var invalidPayload = new ScanResultDto
        {
            Symbol = "",
            TechnicalScore = 50m
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/scanner/ai-explain", invalidPayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AiExplanationDto>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
    }
}
