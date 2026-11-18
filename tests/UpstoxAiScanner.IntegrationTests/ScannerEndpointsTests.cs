using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.Scanner;
using Xunit;

namespace UpstoxAiScanner.IntegrationTests;

public class ScannerEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ScannerEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSwingOpportunities_ReturnsRankedResultsWithTechnicalScores()
    {
        // Act
        var response = await _client.GetAsync("/api/scanner/swing?limit=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<ScanResultDto>>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Should().NotBeEmpty();

        var first = body.Data[0];
        first.ScanType.Should().Be("Swing");
        first.TechnicalScore.Should().BeInRange(0m, 100m);
        first.Signal.Should().NotBeNullOrWhiteSpace();
        first.Explanation.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetIntradayOpportunities_ReturnsRanked5mResults()
    {
        // Act
        var response = await _client.GetAsync("/api/scanner/intraday?limit=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<ScanResultDto>>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Should().NotBeEmpty();

        var first = body.Data[0];
        first.ScanType.Should().Be("Intraday");
        first.TechnicalScore.Should().BeInRange(0m, 100m);
    }

    [Fact]
    public async Task ScanStock_SpecificSymbol_ReturnsCompleteScoreBreakdown()
    {
        // Act
        var response = await _client.GetAsync("/api/scanner/stock/RELIANCE?scanType=Swing");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ScanResultDto>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Symbol.Should().Be("RELIANCE");
        body.Data.TechnicalScore.Should().BeInRange(0m, 100m);
        body.Data.Signal.Should().BeOneOf("Strong Bullish", "Bullish", "Neutral", "Bearish", "Strong Bearish");
    }

    [Fact]
    public async Task GetScanHistory_ReturnsPersistedResults()
    {
        // Act
        var response = await _client.GetAsync("/api/scanner/history?scanType=Swing&limit=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<ScanResultDto>>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
    }
}
