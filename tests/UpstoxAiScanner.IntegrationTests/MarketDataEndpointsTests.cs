using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.MarketData;
using Xunit;

namespace UpstoxAiScanner.IntegrationTests;

public class MarketDataEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MarketDataEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetQuotes_WithValidKeys_ReturnsSuccessAndQuotes()
    {
        var response = await _client.GetAsync("/api/market/quotes?instrumentKeys=NSE_EQ|INE002A01018,NSE_EQ|INE467B01029");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<Dictionary<string, MarketQuoteDto>>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Should().ContainKey("NSE_EQ|INE002A01018");
        body.Data!.Should().ContainKey("NSE_EQ|INE467B01029");

        var reliance = body.Data["NSE_EQ|INE002A01018"];
        reliance.Symbol.Should().Be("RELIANCE");
        reliance.LastPrice.Should().BeGreaterThan(0);
        reliance.Volume.Should().BeGreaterThan(0);
        reliance.Depth.Buy.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetQuotes_WithoutKeys_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/market/quotes?instrumentKeys=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<Dictionary<string, MarketQuoteDto>>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
        body.ErrorCode.Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task GetLtp_WithValidKey_ReturnsSuccessAndLtp()
    {
        var response = await _client.GetAsync("/api/market/ltp?instrumentKeys=NSE_EQ|INE002A01018");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<Dictionary<string, LtpQuoteDto>>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().ContainKey("NSE_EQ|INE002A01018");

        var ltp = body.Data!["NSE_EQ|INE002A01018"];
        ltp.LastPrice.Should().BeGreaterThan(0);
        ltp.Volume.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetOhlc_WithValidKey_ReturnsSuccessAndOhlc()
    {
        var response = await _client.GetAsync("/api/market/ohlc?instrumentKeys=NSE_EQ|INE009A01021&interval=1d");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<Dictionary<string, OhlcQuoteDto>>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().ContainKey("NSE_EQ|INE009A01021");

        var ohlc = body.Data!["NSE_EQ|INE009A01021"];
        ohlc.Open.Should().BeGreaterThan(0);
        ohlc.High.Should().BeGreaterThanOrEqualTo(ohlc.Low);
    }

    [Fact]
    public async Task GetHistorical_WithValidRange_ReturnsCandles()
    {
        var response = await _client.GetAsync("/api/market/historical?instrumentKey=NSE_EQ|INE002A01018&unit=days&interval=1&toDate=2026-03-31&fromDate=2026-03-01");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<CandleDto>>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetHistorical_WithInvalidDateRange_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/market/historical?instrumentKey=NSE_EQ|INE002A01018&unit=days&interval=1&toDate=2026-01-01&fromDate=2026-03-31");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<CandleDto>>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
        body.ErrorCode.Should().Be("INVALID_DATE_RANGE");
    }

    [Fact]
    public async Task GetIntraday_WithValidKey_ReturnsCandles()
    {
        var response = await _client.GetAsync("/api/market/intraday?instrumentKey=NSE_EQ|INE002A01018&unit=minutes&interval=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<CandleDto>>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetStatus_ReturnsValidStatusDto()
    {
        var response = await _client.GetAsync("/api/market/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<UpstoxStatusDto>();

        status.Should().NotBeNull();
        status!.BaseUrl.Should().Be("https://api.upstox.com");
        status.ApiVersion.Should().Be("v3");
        status.StatusMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SwaggerEndpoint_ReturnsOpenApiJson()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Upstox AI Stock Scanner API");
        content.Should().Contain("/api/market/quotes");
    }
}
