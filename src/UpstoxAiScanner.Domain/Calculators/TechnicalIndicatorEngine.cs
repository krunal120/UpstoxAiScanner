using UpstoxAiScanner.Domain.Indicators;
using UpstoxAiScanner.Domain.Models;

namespace UpstoxAiScanner.Domain.Calculators;

/// <summary>
/// Composite technical indicator calculation engine aggregating RSI, MACD, EMAs, Breakouts, and Volume Analysis.
/// </summary>
public static class TechnicalIndicatorEngine
{
    /// <summary>
    /// Computes full quantitative technical analysis snapshot for the latest candle in the series.
    /// </summary>
    /// <param name="candles">Chronological series of candles (oldest first).</param>
    /// <param name="rsiPeriod">RSI period (default: 14).</param>
    /// <param name="volumeSurgeThreshold">Volume surge multiplier threshold (default: 1.5x).</param>
    public static TechnicalAnalysisSnapshot? ComputeSnapshot(
        IReadOnlyList<Candle> candles,
        int rsiPeriod = 14,
        decimal volumeSurgeThreshold = 1.5m)
    {
        if (candles == null || candles.Count == 0)
            return null;

        var latest = candles[^1];

        return new TechnicalAnalysisSnapshot
        {
            Timestamp = latest.Timestamp,
            ClosePrice = latest.Close,
            OpenPrice = latest.Open,
            HighPrice = latest.High,
            LowPrice = latest.Low,
            Atr = AtrCalculator.Analyze(candles)?.Value,
            Rsi = RsiCalculator.Analyze(candles, rsiPeriod),
            Macd = MacdCalculator.Analyze(candles),
            Ema = EmaCalculator.Analyze(candles),
            Breakout = BreakoutCalculator.Analyze(candles),
            Volume = VolumeCalculator.Analyze(candles, volumeSurgeThreshold)
        };
    }
}
