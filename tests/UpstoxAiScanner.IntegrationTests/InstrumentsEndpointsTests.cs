using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.Instruments;
using Xunit;

namespace UpstoxAiScanner.IntegrationTests;

public class InstrumentsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public InstrumentsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetInstruments_EmptyOrSeeded_ReturnsSuccessAndPagedResult()
    {
        // Act
        var response = await _client.GetAsync("/api/instruments?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<InstrumentDto>>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Page.Should().Be(1);
        body.Data.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetBySymbol_NonExistent_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/instruments/NONEXISTENT_SYMBOL_XYZ");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<InstrumentDto>>();

        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
        body.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task SyncInstruments_AndVerifyConstituentsFlow()
    {
        // 1. Trigger synchronization of NIFTY500
        var syncResponse = await _client.PostAsync("/api/instruments/sync?universe=NIFTY500", null);

        syncResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var syncBody = await syncResponse.Content.ReadFromJsonAsync<ApiResponse<InstrumentSyncSummaryDto>>();

        syncBody.Should().NotBeNull();
        syncBody!.Success.Should().BeTrue();
        syncBody.Data.Should().NotBeNull();
        syncBody.Data!.Universe.Should().Be("NIFTY500");
        syncBody.Data.TotalConstituentsProcessed.Should().BeGreaterThan(400);
        syncBody.Data.MatchedCount.Should().BeGreaterThan(400);

        // 2. Query paginated instruments
        var listResponse = await _client.GetAsync("/api/instruments?universe=NIFTY500&page=1&pageSize=20");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<InstrumentDto>>>();

        listBody.Should().NotBeNull();
        listBody!.Data.Should().NotBeNull();
        listBody.Data!.Items.Should().NotBeEmpty();
        listBody.Data.TotalCount.Should().BeGreaterThan(400);

        // 3. Query specific well-known symbol: RELIANCE
        var symbolResponse = await _client.GetAsync("/api/instruments/RELIANCE");
        symbolResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var relianceBody = await symbolResponse.Content.ReadFromJsonAsync<ApiResponse<InstrumentDto>>();

        relianceBody.Should().NotBeNull();
        relianceBody!.Success.Should().BeTrue();
        relianceBody.Data.Should().NotBeNull();
        relianceBody.Data!.Symbol.Should().Be("RELIANCE");
        relianceBody.Data.InstrumentKey.Should().Contain("NSE_EQ|");
        relianceBody.Data.IsActive.Should().BeTrue();

        // 4. Update status: Deactivate RELIANCE
        var patchResponse = await _client.PatchAsync("/api/instruments/RELIANCE/status?isActive=false", null);
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify updated status
        var verifyResponse = await _client.GetAsync("/api/instruments/RELIANCE");
        var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<ApiResponse<InstrumentDto>>();
        verifyBody!.Data!.IsActive.Should().BeFalse();

        // Re-activate RELIANCE
        await _client.PatchAsync("/api/instruments/RELIANCE/status?isActive=true", null);
    }
}
