using UpstoxAiScanner.Application.DTOs.TechnicalAnalysis;
using UpstoxAiScanner.Domain.Models;

namespace UpstoxAiScanner.Application.Interfaces;

/// <summary>
/// Service contract for computing, analyzing, and persisting technical indicators for stocks.
/// </summary>
public interface ITechnicalAnalysisService
{
    /// <summary>
    /// Analyzes an instrument by symbol (e.g. "RELIANCE") or key, retrieving required candles from Upstox API,
    /// computing RSI, MACD, EMAs, Breakouts, and Volume metrics, and optionally caching to the database.
    /// </summary>
    /// <param name="symbol">Stock symbol or Upstox instrument key.</param>
    /// <param name="interval">Interval: "1d" (daily) or "5m" (intraday 5-minute).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TechnicalAnalysisDto?> AnalyzeInstrumentAsync(
        string symbol,
        string interval = "1d",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes technical indicators directly from an in-memory candle series.
    /// </summary>
    TechnicalAnalysisDto AnalyzeCandles(
        string symbol,
        string instrumentKey,
        string interval,
        IReadOnlyList<Candle> candles);
}
