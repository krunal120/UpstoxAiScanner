using UpstoxAiScanner.Domain.Indicators;
using UpstoxAiScanner.Domain.Models;

namespace UpstoxAiScanner.Domain.Calculators;

/// <summary>
/// Average True Range (ATR) calculator using standard Wilder's smoothing method.
/// </summary>
public static class AtrCalculator
{
    /// <summary>
    /// Calculates the 14-period Average True Range for volatility estimation.
    /// </summary>
    /// <param name="candles">Chronological list of candles (oldest first).</param>
    /// <param name="period">ATR period (default: 14).</param>
    public static AtrAnalysis? Analyze(IReadOnlyList<Candle> candles, int period = 14)
    {
        if (candles == null || candles.Count < period + 1)
            return null;

        var trueRanges = new List<decimal>(candles.Count);

        for (int i = 0; i < candles.Count; i++)
        {
            var current = candles[i];
            if (i == 0)
            {
                trueRanges.Add(current.High - current.Low);
            }
            else
            {
                var prev = candles[i - 1];
                decimal tr = Math.Max(
                    current.High - current.Low,
                    Math.Max(
                        Math.Abs(current.High - prev.Close),
                        Math.Abs(current.Low - prev.Close)
                    )
                );
                trueRanges.Add(tr);
            }
        }

        // Wilder's initial ATR is simple average of first 'period' True Ranges
        decimal atr = 0m;
        for (int i = 0; i < period; i++)
        {
            atr += trueRanges[i];
        }
        atr /= period;

        // Wilder's smoothing: ATR = ((prior ATR * (period - 1)) + current TR) / period
        for (int i = period; i < trueRanges.Count; i++)
        {
            atr = ((atr * (period - 1)) + trueRanges[i]) / period;
        }

        var latest = candles[^1];
        return new AtrAnalysis
        {
            Value = Math.Round(atr, 2),
            ClosePrice = latest.Close
        };
    }
}
