using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using UpstoxAiScanner.Api.Controllers;
using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.TechnicalAnalysis;
using UpstoxAiScanner.Domain.Models;
using Xunit;

namespace UpstoxAiScanner.IntegrationTests;

public class IndicatorsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public IndicatorsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetIndicators_ValidSymbol_ReturnsSuccessWithAllCalculations()
    {
        // Act
        var response = await _client.GetAsync("/api/indicators/RELIANCE?interval=1d");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TechnicalAnalysisDto>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Symbol.Should().Be("RELIANCE");
        body.Data.ClosePrice.Should().BeGreaterThan(0);

        // Verify RSI
        body.Data.Rsi.Should().NotBeNull();
        body.Data.Rsi!.Value.Should().BeInRange(0m, 100m);

        // Verify MACD
        body.Data.Macd.Should().NotBeNull();
        body.Data.Macd!.HistogramMomentum.Should().NotBeNullOrWhiteSpace();

        // Verify EMA
        body.Data.Ema.Should().NotBeNull();

        // Verify Breakout
        body.Data.Breakout.Should().NotBeNull();

        // Verify Volume
        body.Data.Volume.Should().NotBeNull();
        body.Data.Volume!.CurrentVolume.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetIndicators_IntradayInterval_Returns5mAnalysis()
    {
        // Act
        var response = await _client.GetAsync("/api/indicators/RELIANCE?interval=5m");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TechnicalAnalysisDto>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Interval.Should().Be("5m");
    }

    [Fact]
    public async Task CalculateIndicators_PostCustomCandles_ReturnsCorrectCalculations()
    {
        // Arrange
        var request = new CalculateIndicatorsRequest
        {
            Symbol = "CUSTOM_TEST",
            Interval = "1d",
            Candles = Enumerable.Range(0, 30).Select(i => new Candle
            {
                Timestamp = DateTimeOffset.UtcNow.AddDays(-30 + i),
                Open = 100m + i,
                High = 105m + i,
                Low = 95m + i,
                Close = 102m + i,
                Volume = 50000 + (i * 1000),
                OpenInterest = 0
            }).ToList()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/indicators/calculate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TechnicalAnalysisDto>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Symbol.Should().Be("CUSTOM_TEST");
        body.Data.Rsi.Should().NotBeNull();
        body.Data.Rsi!.Value.Should().BeGreaterThan(50m); // Consistently rising prices
    }
}
