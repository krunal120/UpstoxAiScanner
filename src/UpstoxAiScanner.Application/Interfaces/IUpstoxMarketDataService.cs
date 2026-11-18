using UpstoxAiScanner.Application.Common;
using UpstoxAiScanner.Application.DTOs.MarketData;

namespace UpstoxAiScanner.Application.Interfaces;

/// <summary>
/// Service interface for interacting with Upstox Market Data APIs.
/// </summary>
public interface IUpstoxMarketDataService
{
    /// <summary>
    /// Fetches full market quotes for one or more instruments.
    /// </summary>
    Task<ApiResponse<IReadOnlyDictionary<string, MarketQuoteDto>>> GetMarketQuotesAsync(
        IEnumerable<string> instrumentKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches Last Traded Price (LTP) quotes for one or more instruments.
    /// </summary>
    Task<ApiResponse<IReadOnlyDictionary<string, LtpQuoteDto>>> GetLtpQuotesAsync(
        IEnumerable<string> instrumentKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches OHLC quotes for one or more instruments at the given interval (e.g., "1d", "I1", "I30").
    /// </summary>
    Task<ApiResponse<IReadOnlyDictionary<string, OhlcQuoteDto>>> GetOhlcQuotesAsync(
        IEnumerable<string> instrumentKeys,
        string interval = "1d",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches historical OHLCV candles for an instrument.
    /// </summary>
    Task<ApiResponse<IReadOnlyList<CandleDto>>> GetHistoricalCandlesAsync(
        string instrumentKey,
        string unit,
        int interval,
        DateOnly toDate,
        DateOnly fromDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches intraday OHLCV candles for an instrument for the current trading day.
    /// </summary>
    Task<ApiResponse<IReadOnlyList<CandleDto>>> GetIntradayCandlesAsync(
        string instrumentKey,
        string unit,
        int interval,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current configuration and operational status of the Upstox integration.
    /// </summary>
    Task<UpstoxStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
}
