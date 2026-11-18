using Microsoft.AspNetCore.Mvc;
using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.MarketData;
using UpstoxAiScanner.Application.Interfaces;

namespace UpstoxAiScanner.Api.Controllers;

/// <summary>
/// Exposes endpoints for real-time and historical market data using Upstox APIs.
/// </summary>
[ApiController]
[Route("api/market")]
[Produces("application/json")]
public class MarketDataController : ControllerBase
{
    private readonly IUpstoxMarketDataService _marketDataService;
    private readonly ILogger<MarketDataController> _logger;

    public MarketDataController(
        IUpstoxMarketDataService marketDataService,
        ILogger<MarketDataController> logger)
    {
        _marketDataService = marketDataService;
        _logger = logger;
    }

    /// <summary>
    /// Fetches full market quotes including OHLC, market depth, volume, and session info.
    /// </summary>
    /// <param name="instrumentKeys">Comma-separated list of instrument keys (e.g. NSE_EQ|INE002A01018,NSE_EQ|INE467B01029).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns dictionary of market quotes keyed by instrument key.</response>
    /// <response code="400">If no instrument keys are provided.</response>
    [HttpGet("quotes")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyDictionary<string, MarketQuoteDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyDictionary<string, MarketQuoteDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetQuotes(
        [FromQuery] string? instrumentKeys,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(instrumentKeys))
        {
            return BadRequest(ApiResponse<IReadOnlyDictionary<string, MarketQuoteDto>>.Fail(
                "Please provide at least one instrument key in 'instrumentKeys' query parameter.", "INVALID_INPUT"));
        }

        var keys = instrumentKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var response = await _marketDataService.GetMarketQuotesAsync(keys, cancellationToken);

        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Fetches Last Traded Price (LTP) and day change for one or multiple instruments.
    /// </summary>
    /// <param name="instrumentKeys">Comma-separated list of instrument keys (e.g. NSE_EQ|INE002A01018).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns dictionary of LTP quotes keyed by instrument key.</response>
    /// <response code="400">If no instrument keys are provided.</response>
    [HttpGet("ltp")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyDictionary<string, LtpQuoteDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyDictionary<string, LtpQuoteDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetLtp(
        [FromQuery] string? instrumentKeys,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(instrumentKeys))
        {
            return BadRequest(ApiResponse<IReadOnlyDictionary<string, LtpQuoteDto>>.Fail(
                "Please provide at least one instrument key in 'instrumentKeys' query parameter.", "INVALID_INPUT"));
        }

        var keys = instrumentKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var response = await _marketDataService.GetLtpQuotesAsync(keys, cancellationToken);

        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Fetches OHLC (Open, High, Low, Close) market data and previous session OHLC.
    /// </summary>
    /// <param name="instrumentKeys">Comma-separated list of instrument keys.</param>
    /// <param name="interval">Interval format (e.g., "1d", "I1", "I30"). Default is "1d".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns dictionary of OHLC quotes keyed by instrument key.</response>
    /// <response code="400">If no instrument keys are provided.</response>
    [HttpGet("ohlc")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyDictionary<string, OhlcQuoteDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyDictionary<string, OhlcQuoteDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetOhlc(
        [FromQuery] string? instrumentKeys,
        [FromQuery] string interval = "1d",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instrumentKeys))
        {
            return BadRequest(ApiResponse<IReadOnlyDictionary<string, OhlcQuoteDto>>.Fail(
                "Please provide at least one instrument key in 'instrumentKeys' query parameter.", "INVALID_INPUT"));
        }

        var keys = instrumentKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var response = await _marketDataService.GetOhlcQuotesAsync(keys, interval, cancellationToken);

        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Fetches historical OHLCV candles for an instrument across custom time units and date ranges.
    /// </summary>
    /// <param name="instrumentKey">Instrument key (e.g. NSE_EQ|INE002A01018).</param>
    /// <param name="unit">Time unit: minutes, hours, days, weeks, months. Default is "days".</param>
    /// <param name="interval">Interval multiplier (e.g. 1 for days, 5 for minutes). Default is 1.</param>
    /// <param name="toDate">End date (YYYY-MM-DD). Defaults to today.</param>
    /// <param name="fromDate">Start date (YYYY-MM-DD). Defaults to 30 days ago.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns list of historical OHLCV candle bars.</response>
    /// <response code="400">If instrumentKey is invalid or date range is invalid.</response>
    [HttpGet("historical")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CandleDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CandleDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetHistorical(
        [FromQuery] string? instrumentKey,
        [FromQuery] string unit = "days",
        [FromQuery] int interval = 1,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] DateOnly? fromDate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instrumentKey))
        {
            return BadRequest(ApiResponse<IReadOnlyList<CandleDto>>.Fail(
                "Please provide an 'instrumentKey' parameter.", "INVALID_INPUT"));
        }

        var end = toDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = fromDate ?? end.AddDays(-30);

        if (end < start)
        {
            return BadRequest(ApiResponse<IReadOnlyList<CandleDto>>.Fail(
                "'toDate' must be greater than or equal to 'fromDate'.", "INVALID_DATE_RANGE"));
        }

        var response = await _marketDataService.GetHistoricalCandlesAsync(
            instrumentKey, unit, interval, end, start, cancellationToken);

        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Fetches intraday OHLCV candles for the current trading day.
    /// </summary>
    /// <param name="instrumentKey">Instrument key (e.g. NSE_EQ|INE002A01018).</param>
    /// <param name="unit">Time unit: minutes, hours. Default is "minutes".</param>
    /// <param name="interval">Interval multiplier (e.g. 1, 5, 15). Default is 5.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns list of intraday OHLCV candle bars.</response>
    /// <response code="400">If instrumentKey is invalid.</response>
    [HttpGet("intraday")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CandleDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CandleDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetIntraday(
        [FromQuery] string? instrumentKey,
        [FromQuery] string unit = "minutes",
        [FromQuery] int interval = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instrumentKey))
        {
            return BadRequest(ApiResponse<IReadOnlyList<CandleDto>>.Fail(
                "Please provide an 'instrumentKey' parameter.", "INVALID_INPUT"));
        }

        var response = await _marketDataService.GetIntradayCandlesAsync(
            instrumentKey, unit, interval, cancellationToken);

        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Retrieves the current Upstox API integration status, connectivity mode, and token availability.
    /// </summary>
    /// <response code="200">Integration status details.</response>
    [HttpGet("status")]
    [ProducesResponseType(typeof(UpstoxStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _marketDataService.GetStatusAsync(cancellationToken);
        return Ok(status);
    }
}
