using UpstoxAiScanner.Domain.Indicators;
using UpstoxAiScanner.Domain.Models;

namespace UpstoxAiScanner.Domain.Calculators;

/// <summary>
/// Volume indicator and volume surge analyzer.
/// </summary>
public static class VolumeCalculator
{
    /// <summary>
    /// Analyzes the trading volume of the latest candle against a 20-period historical baseline.
    /// </summary>
    /// <param name="candles">Chronological candles series.</param>
    /// <param name="surgeThreshold">Ratio multiplier above baseline considered a surge (default 1.5x).</param>
    public static VolumeAnalysis? Analyze(IReadOnlyList<Candle> candles, decimal surgeThreshold = 1.5m)
    {
        if (candles == null || candles.Count < 2)
            return null;

        var current = candles[^1];

        // Use up to 20 prior completed candles for the average volume baseline
        int lookback = Math.Min(20, candles.Count - 1);
        int startIndex = candles.Count - 1 - lookback;
        var baselineCandles = candles.Skip(startIndex).Take(lookback).ToList();

        decimal avgVolume = (decimal)baselineCandles.Average(c => c.Volume);
        decimal volumeRatio = avgVolume > 0m ? (decimal)current.Volume / avgVolume : 1m;
        bool isSurge = volumeRatio >= surgeThreshold;

        return new VolumeAnalysis
        {
            CurrentVolume = current.Volume,
            AverageVolume20 = Math.Round(avgVolume, 0),
            VolumeRatio = Math.Round(volumeRatio, 2),
            IsVolumeSurge = isSurge,
            SurgeThreshold = surgeThreshold
        };
    }
}
