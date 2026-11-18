using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.MarketData;
using UpstoxAiScanner.Application.DTOs.Scanner;
using Xunit;

namespace UpstoxAiScanner.IntegrationTests;

public class MarketHoursAndBackgroundTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MarketHoursAndBackgroundTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMarketStatus_ReturnsCorrectSessionInfoAndIstTime()
    {
        // Act
        var response = await _client.GetAsync("/api/scanner/market-status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MarketSessionStatus>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.SessionPhase.Should().BeOneOf("RegularOpen", "PreOpen", "PostClose", "Closed");
        body.Data.CurrentTimeIst.Offset.Should().Be(TimeSpan.FromHours(5.5)); // IST UTC+5:30
        body.Data.MarketOpenTimeTodayIst.Hour.Should().Be(9);
        body.Data.MarketOpenTimeTodayIst.Minute.Should().Be(15);
        body.Data.MarketCloseTimeTodayIst.Hour.Should().Be(15);
        body.Data.MarketCloseTimeTodayIst.Minute.Should().Be(30);
    }

    [Fact]
    public async Task GetBackgroundScannerStatus_ReturnsServiceStateAndMetrics()
    {
        // Act
        var response = await _client.GetAsync("/api/scanner/background/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BackgroundScannerStatusDto>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Universe.Should().Be("NIFTY500");
        body.Data.IntradayScanIntervalSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ToggleBackgroundScanning_UpdatesState()
    {
        // 1. Toggle to true
        var response1 = await _client.PostAsync("/api/scanner/background/toggle?enabled=true", null);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        var verify1 = await _client.GetAsync("/api/scanner/background/status");
        var body1 = await verify1.Content.ReadFromJsonAsync<ApiResponse<BackgroundScannerStatusDto>>();
        body1!.Data!.IsEnabled.Should().BeTrue();

        // 2. Toggle to false
        var response2 = await _client.PostAsync("/api/scanner/background/toggle?enabled=false", null);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var verify2 = await _client.GetAsync("/api/scanner/background/status");
        var body2 = await verify2.Content.ReadFromJsonAsync<ApiResponse<BackgroundScannerStatusDto>>();
        body2!.Data!.IsEnabled.Should().BeFalse();

        // Restore to true
        await _client.PostAsync("/api/scanner/background/toggle?enabled=true", null);
    }

    [Fact]
    public async Task SetIgnoreMarketHours_UpdatesState()
    {
        // 1. Set ignore to true
        var response = await _client.PostAsync("/api/scanner/background/ignore-market-hours?ignore=true", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var verify = await _client.GetAsync("/api/scanner/background/status");
        var body = await verify.Content.ReadFromJsonAsync<ApiResponse<BackgroundScannerStatusDto>>();
        body!.Data!.IgnoreMarketHours.Should().BeTrue();

        // Restore to false
        await _client.PostAsync("/api/scanner/background/ignore-market-hours?ignore=false", null);
    }

    [Fact]
    public async Task WebSocketSubscribe_UpdatesSubscriptionsList()
    {
        // 1. Subscribe to RELIANCE and TCS
        var subResponse = await _client.PostAsync("/api/scanner/websocket/subscribe?instrumentKeys=NSE_EQ|INE002A01018,NSE_EQ|INE467B01029&mode=full", null);
        subResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Query subscriptions
        var listResponse = await _client.GetAsync("/api/scanner/websocket/subscriptions");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResponse.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyCollection<string>>>();

        listBody.Should().NotBeNull();
        listBody!.Data.Should().Contain("NSE_EQ|INE002A01018");
        listBody.Data.Should().Contain("NSE_EQ|INE467B01029");
    }
}
